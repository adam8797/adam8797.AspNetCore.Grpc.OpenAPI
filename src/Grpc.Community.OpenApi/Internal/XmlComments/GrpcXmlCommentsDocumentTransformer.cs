// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Xml.XPath;
using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.OpenApi;

namespace Grpc.Community.OpenApi.Internal.XmlComments;

/// <summary>
/// Applies type-level XML documentation summaries as descriptions of the
/// OpenAPI tags that gRPC transcoded endpoints get grouped under.
/// </summary>
internal sealed class GrpcXmlCommentsDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string MemberXPath = "/doc/members/member[@name='{0}']";
    private const string SummaryTag = "summary";

    private readonly XPathNavigator _xmlNavigator;

    public GrpcXmlCommentsDocumentTransformer(XPathDocument xmlDoc)
    {
        _xmlNavigator = xmlDoc.CreateNavigator();
    }

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // Get unique services — one entry per controller/service name.
        var nameAndServiceDescriptor = context.DescriptionGroups
            .SelectMany(g => g.Items)
            .Select(apiDesc => apiDesc.ActionDescriptor)
            .Where(actionDesc => actionDesc != null && (actionDesc.EndpointMetadata?.Any(m => m is GrpcMethodMetadata) ?? false))
            .GroupBy(actionDesc => actionDesc.RouteValues["controller"]!)
            .Select(group => new KeyValuePair<string, ActionDescriptor>(group.Key, group.First()));

        foreach (var nameAndType in nameAndServiceDescriptor)
        {
            var grpcMethodMetadata = nameAndType.Value.EndpointMetadata.OfType<GrpcMethodMetadata>().First();
            if (TryAddTag(document, nameAndType, grpcMethodMetadata.ServiceType))
            {
                continue;
            }

            if (grpcMethodMetadata.ServiceType.BaseType?.DeclaringType is { } staticService)
            {
                TryAddTag(document, nameAndType, staticService);
            }
        }

        return Task.CompletedTask;
    }

    private bool TryAddTag(OpenApiDocument document, KeyValuePair<string, ActionDescriptor> nameAndType, Type type)
    {
        var memberName = XmlCommentsNodeNameHelper.GetMemberNameForType(type);
        var typeNode = _xmlNavigator.SelectSingleNode(string.Format(CultureInfo.InvariantCulture, MemberXPath, memberName));

        if (typeNode == null)
        {
            return false;
        }

        var summaryNode = typeNode.SelectSingleNode(SummaryTag);
        if (summaryNode != null)
        {
            var description = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

            document.Tags ??= new HashSet<OpenApiTag>();

            // The tag is usually already present, created from the operations' tags.
            // Tags is a name-keyed set, so Add would be silently rejected and the
            // description lost - update the existing tag instead.
            var existingTag = document.Tags.FirstOrDefault(t => t.Name == nameAndType.Key);
            if (existingTag != null)
            {
                existingTag.Description = description;
            }
            else
            {
                document.Tags.Add(new OpenApiTag
                {
                    Name = nameAndType.Key,
                    Description = description,
                });
            }
        }

        return true;
    }
}
