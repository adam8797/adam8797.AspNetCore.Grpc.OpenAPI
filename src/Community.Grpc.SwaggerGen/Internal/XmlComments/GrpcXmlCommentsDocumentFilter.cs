// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Linq;
using System.Xml.XPath;
using Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Community.Grpc.SwaggerGen.Internal.XmlComments;

internal sealed class GrpcXmlCommentsDocumentFilter : IDocumentFilter
{
    private const string MemberXPath = "/doc/members/member[@name='{0}']";
    private const string SummaryTag = "summary";

    private readonly XPathNavigator _xmlNavigator;

    public GrpcXmlCommentsDocumentFilter(XPathDocument xmlDoc)
    {
        _xmlNavigator = xmlDoc.CreateNavigator();
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Get unique services
        var nameAndServiceDescriptor = context.ApiDescriptions
            .Select(apiDesc => apiDesc.ActionDescriptor)
            .Where(actionDesc => actionDesc != null && (actionDesc.EndpointMetadata?.Any(m => m is GrpcMethodMetadata) ?? false))
            .GroupBy(actionDesc => actionDesc.RouteValues["controller"]!)
            .Select(group => new KeyValuePair<string, ActionDescriptor>(group.Key, group.First()));

        foreach (var nameAndType in nameAndServiceDescriptor)
        {
            var grpcMethodMetadata = nameAndType.Value.EndpointMetadata.OfType<GrpcMethodMetadata>().First();
            if (TryAdd(swaggerDoc, nameAndType, grpcMethodMetadata.ServiceType))
            {
                continue;
            }

            if (grpcMethodMetadata.ServiceType.BaseType?.DeclaringType is { } staticService)
            {
                if (TryAdd(swaggerDoc, nameAndType, staticService))
                {
                    continue;
                }
            }
        }
    }

    private bool TryAdd(OpenApiDocument swaggerDoc, KeyValuePair<string, ActionDescriptor> nameAndType, Type type)
    {
        var memberName = XmlCommentsNodeNameHelper.GetMemberNameForType(type);
        var typeNode = _xmlNavigator.SelectSingleNode(string.Format(CultureInfo.InvariantCulture, MemberXPath, memberName));

        if (typeNode != null)
        {
            var summaryNode = typeNode.SelectSingleNode(SummaryTag);
            if (summaryNode != null)
            {
                var description = XmlCommentsTextHelper.Humanize(summaryNode.InnerXml);

                swaggerDoc.Tags ??= new HashSet<OpenApiTag>();

                // The tag is usually already present, created from the operations' tags.
                // Tags is a name-keyed set, so Add would be silently rejected and the
                // description lost - update the existing tag instead.
                var existingTag = swaggerDoc.Tags.FirstOrDefault(t => t.Name == nameAndType.Key);
                if (existingTag != null)
                {
                    existingTag.Description = description;
                }
                else
                {
                    swaggerDoc.Tags.Add(new OpenApiTag
                    {
                        Name = nameAndType.Key,
                        Description = description
                    });
                }
            }
            return true;
        }

        return false;
    }
}
