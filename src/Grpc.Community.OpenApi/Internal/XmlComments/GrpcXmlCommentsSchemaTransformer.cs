// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Xml.XPath;
using Microsoft.AspNetCore.OpenApi;

namespace Grpc.Community.OpenApi.Internal.XmlComments;

/// <summary>
/// Applies XML documentation summaries as descriptions on the schemas generated for
/// protobuf messages and on their individual properties.
/// </summary>
/// <remarks>
/// This runs after <see cref="GrpcSchemaTransformer"/>, which rewrites a message's
/// properties so that they are keyed by protobuf JSON name rather than CLR name.
/// </remarks>
internal sealed class GrpcXmlCommentsSchemaTransformer : IOpenApiSchemaTransformer
{
    private const string MemberXPath = "/doc/members/member[@name='{0}']/summary";

    private readonly XPathNavigator _xmlNavigator;

    public GrpcXmlCommentsSchemaTransformer(XPathDocument xmlDoc)
    {
        _xmlNavigator = xmlDoc.CreateNavigator();
    }

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        schema.Description ??= TryGetSummary(XmlCommentsNodeNameHelper.GetMemberNameForType(type));

        if (schema.Properties is { Count: > 0 })
        {
            foreach (var (jsonName, propertySchema) in schema.Properties)
            {
                if (propertySchema is not OpenApiSchema concreteSchema || concreteSchema.Description != null)
                {
                    continue;
                }

                if (FindPropertyByJsonName(type, jsonName) is { } propertyInfo)
                {
                    concreteSchema.Description = TryGetSummary(XmlCommentsNodeNameHelper.GetMemberNameForProperty(propertyInfo));
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps a protobuf JSON name back to the generated CLR property. Protobuf JSON names are
    /// the lower camel case form of the field name and the generated property is the upper
    /// camel case form, so they differ only by the case of the first character.
    /// </summary>
    private static PropertyInfo? FindPropertyByJsonName(Type type, string jsonName)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (string.Equals(property.Name, jsonName, StringComparison.OrdinalIgnoreCase))
            {
                return property;
            }
        }

        return null;
    }

    private string? TryGetSummary(string memberName)
    {
        var summaryNode = _xmlNavigator.SelectSingleNode(string.Format(
            System.Globalization.CultureInfo.InvariantCulture, MemberXPath, memberName));

        return summaryNode != null ? XmlCommentsTextHelper.Humanize(summaryNode.InnerXml) : null;
    }
}
