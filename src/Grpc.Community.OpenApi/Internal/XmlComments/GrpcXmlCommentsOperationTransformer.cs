// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Xml.XPath;
using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;

namespace Grpc.Community.OpenApi.Internal.XmlComments;

/// <summary>
/// Applies method-level XML documentation (summary / remarks / response) as
/// the description of the corresponding OpenAPI operation for gRPC transcoded
/// endpoints.
/// </summary>
internal sealed class GrpcXmlCommentsOperationTransformer : IOpenApiOperationTransformer
{
    private readonly XPathNavigator _xmlNavigator;

    public GrpcXmlCommentsOperationTransformer(XPathDocument xmlDoc)
    {
        _xmlNavigator = xmlDoc.CreateNavigator();
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var grpcMetadata = context.Description.ActionDescriptor.EndpointMetadata.OfType<GrpcMethodMetadata>().FirstOrDefault();
        if (grpcMetadata == null)
        {
            return Task.CompletedTask;
        }

        var methodInfo = grpcMetadata.ServiceType.GetMethod(grpcMetadata.Method.Name);
        if (methodInfo == null)
        {
            return Task.CompletedTask;
        }

        // If method is from a constructed generic type, look for comments from the generic type method
        var targetMethod = methodInfo.DeclaringType!.IsConstructedGenericType
            ? GetUnderlyingGenericTypeMethod(methodInfo)
            : methodInfo;

        if (targetMethod == null)
        {
            return Task.CompletedTask;
        }

        // Base service never has response tags.
        ApplyServiceResponses(operation, targetMethod.DeclaringType!);

        // Parameter and request body descriptions come from the protobuf-generated
        // properties, so they are independent of which method the comments are found on.
        ApplyParameterDescriptions(operation, context);
        ApplyRequestBodyDescription(operation, context, targetMethod);

        if (TryApplyMethodComments(operation, targetMethod))
        {
            return Task.CompletedTask;
        }

        if (targetMethod.IsVirtual && targetMethod.GetBaseDefinition() is { } baseMethod)
        {
            TryApplyMethodComments(operation, baseMethod);
        }

        return Task.CompletedTask;
    }

    private void ApplyParameterDescriptions(OpenApiOperation operation, OpenApiOperationTransformerContext context)
    {
        if (operation.Parameters == null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            if (parameter is not OpenApiParameter concreteParameter || concreteParameter.Description != null)
            {
                continue;
            }

            var apiParameter = context.Description.ParameterDescriptions
                .FirstOrDefault(p => string.Equals(p.Name, concreteParameter.Name, StringComparison.Ordinal));

            if (apiParameter?.ModelMetadata is GrpcModelMetadata metadata &&
                metadata.ModelIdentity.PropertyInfo is { } propertyInfo &&
                TryGetPropertySummary(propertyInfo) is { } summary)
            {
                concreteParameter.Description = summary;
            }
        }
    }

    private void ApplyRequestBodyDescription(OpenApiOperation operation, OpenApiOperationTransformerContext context, MethodInfo targetMethod)
    {
        if (operation.RequestBody is not OpenApiRequestBody requestBody || requestBody.Description != null)
        {
            return;
        }

        var bodyParameter = context.Description.ParameterDescriptions
            .FirstOrDefault(p => p.Source == BindingSource.Body);

        if (bodyParameter == null)
        {
            return;
        }

        // A body bound to a message field documents itself through that field's property.
        if (bodyParameter.ModelMetadata is GrpcModelMetadata metadata &&
            metadata.ModelIdentity.PropertyInfo is { } propertyInfo)
        {
            requestBody.Description = TryGetPropertySummary(propertyInfo);
            return;
        }

        // A body bound to the whole request ("body: *") is documented by the <param> tag
        // of the service method it is passed to. That parameter is carried on the
        // descriptor rather than the metadata identity, which was built from the type.
        if (bodyParameter.ParameterDescriptor is ControllerParameterDescriptor { ParameterInfo: { } parameterInfo })
        {
            requestBody.Description = TryGetMethodParamDescription(targetMethod, parameterInfo.Name);
        }
    }

    private string? TryGetPropertySummary(PropertyInfo propertyInfo)
    {
        var memberName = XmlCommentsNodeNameHelper.GetMemberNameForProperty(propertyInfo);
        var summaryNode = _xmlNavigator.SelectSingleNode($"/doc/members/member[@name='{memberName}']/summary");
        return summaryNode != null ? XmlCommentsTextHelper.Humanize(summaryNode.InnerXml) : null;
    }

    private string? TryGetMethodParamDescription(MethodInfo methodInfo, string? parameterName)
    {
        if (parameterName == null)
        {
            return null;
        }

        // Deliberately does not fall back to the base definition the way summaries do:
        // the generated gRPC base class carries a boilerplate
        // <param name="request">The request received from the client.</param> that would
        // otherwise be surfaced as every request body's description.
        var memberName = XmlCommentsNodeNameHelper.GetMemberNameForMethod(methodInfo);
        var paramNode = _xmlNavigator.SelectSingleNode(
            $"/doc/members/member[@name='{memberName}']/param[@name='{parameterName}']");

        return paramNode != null ? XmlCommentsTextHelper.Humanize(paramNode.InnerXml) : null;
    }

    private void ApplyServiceResponses(OpenApiOperation operation, Type controllerType)
    {
        var typeMemberName = XmlCommentsNodeNameHelper.GetMemberNameForType(controllerType);
        var responseNodes = _xmlNavigator.Select($"/doc/members/member[@name='{typeMemberName}']/response");
        ApplyResponseNodes(operation, responseNodes);
    }

    private bool TryApplyMethodComments(OpenApiOperation operation, MethodInfo methodInfo)
    {
        var methodMemberName = XmlCommentsNodeNameHelper.GetMemberNameForMethod(methodInfo);
        var methodNode = _xmlNavigator.SelectSingleNode($"/doc/members/member[@name='{methodMemberName}']");

        if (methodNode == null)
        {
            return false;
        }

        var summaryNode = methodNode.SelectSingleNode("summary");
        if (summaryNode != null)
        {
            operation.Summary = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);
        }

        var remarksNode = methodNode.SelectSingleNode("remarks");
        if (remarksNode != null)
        {
            operation.Description = XmlCommentsTextHelper.Humanize(remarksNode.InnerXml);
        }

        var responseNodes = methodNode.Select("response");
        ApplyResponseNodes(operation, responseNodes);

        return true;
    }

    private static void ApplyResponseNodes(OpenApiOperation operation, XPathNodeIterator responseNodes)
    {
        operation.Responses ??= new OpenApiResponses();

        while (responseNodes.MoveNext())
        {
            var code = responseNodes.Current!.GetAttribute("code", "");
            if (!operation.Responses.TryGetValue(code, out var response) || response is not OpenApiResponse concreteResponse)
            {
                concreteResponse = new OpenApiResponse();
                operation.Responses[code] = concreteResponse;
            }

            concreteResponse.Description = XmlCommentsTextHelper.Humanize(responseNodes.Current.InnerXml);
        }
    }

    private static MethodInfo? GetUnderlyingGenericTypeMethod(MethodInfo constructedTypeMethod)
    {
        var constructedType = constructedTypeMethod.DeclaringType!;
        var genericTypeDefinition = constructedType.GetGenericTypeDefinition();

        // Retrieve the method matching by name and parameter positional generic type match (i.e. matching signature at compile time).
        return genericTypeDefinition.GetMethods().FirstOrDefault(m =>
            m.Name == constructedTypeMethod.Name && HasSameSignature(m, constructedTypeMethod));

        static bool HasSameSignature(MethodInfo candidate, MethodInfo target)
        {
            var candidateParameters = candidate.GetParameters();
            var targetParameters = target.GetParameters();
            if (candidateParameters.Length != targetParameters.Length)
            {
                return false;
            }
            for (var i = 0; i < candidateParameters.Length; i++)
            {
                var candidateType = candidateParameters[i].ParameterType;
                var targetType = targetParameters[i].ParameterType;
                if (candidateType == targetType)
                {
                    continue;
                }
                if (candidateType.IsGenericParameter && targetType.IsGenericParameter &&
                    candidateType.GenericParameterPosition == targetType.GenericParameterPosition)
                {
                    continue;
                }
                if (candidateType.IsGenericParameter || targetType.IsGenericParameter)
                {
                    continue;
                }
                return false;
            }
            return true;
        }
    }
}
