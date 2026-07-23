// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.XPath;
using Grpc.Community.OpenApi.Internal.XmlComments;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Extension methods that surface XML documentation on gRPC service types and
/// methods through the generated OpenAPI document.
/// </summary>
public static class GrpcOpenApiOptionsExtensions
{
    /// <summary>
    /// Inject human-friendly descriptions for operations and responses based on the given XML documentation.
    /// </summary>
    /// <param name="options">The <see cref="OpenApiOptions"/> to configure.</param>
    /// <param name="xmlDocFactory">A factory method that returns XML comments as an <see cref="XPathDocument"/>.</param>
    public static OpenApiOptions IncludeGrpcXmlComments(
        this OpenApiOptions options,
        Func<XPathDocument> xmlDocFactory)
        => options.IncludeGrpcXmlComments(xmlDocFactory, includeControllerXmlComments: false);

    /// <summary>
    /// Inject human-friendly descriptions for operations, responses, and — optionally — service tags based on the given XML documentation.
    /// </summary>
    /// <param name="options">The <see cref="OpenApiOptions"/> to configure.</param>
    /// <param name="xmlDocFactory">A factory method that returns XML comments as an <see cref="XPathDocument"/>.</param>
    /// <param name="includeControllerXmlComments">
    /// If <see langword="true"/>, service (controller) XML summaries are used to populate tag descriptions.
    /// </param>
    public static OpenApiOptions IncludeGrpcXmlComments(
        this OpenApiOptions options,
        Func<XPathDocument> xmlDocFactory,
        bool includeControllerXmlComments)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(xmlDocFactory);

        var xmlDoc = xmlDocFactory();
        options.AddOperationTransformer(new GrpcXmlCommentsOperationTransformer(xmlDoc));
        options.AddSchemaTransformer(new GrpcXmlCommentsSchemaTransformer(xmlDoc));

        if (includeControllerXmlComments)
        {
            options.AddDocumentTransformer(new GrpcXmlCommentsDocumentTransformer(xmlDoc));
        }

        return options;
    }

    /// <summary>
    /// Inject human-friendly descriptions for operations and responses based on the XML documentation at the given path.
    /// </summary>
    /// <param name="options">The <see cref="OpenApiOptions"/> to configure.</param>
    /// <param name="filePath">An absolute path to the file that contains XML comments.</param>
    public static OpenApiOptions IncludeGrpcXmlComments(
        this OpenApiOptions options,
        string filePath)
        => options.IncludeGrpcXmlComments(() => new XPathDocument(filePath));

    /// <summary>
    /// Inject human-friendly descriptions for operations, responses, and — optionally — service tags based on the XML documentation at the given path.
    /// </summary>
    /// <param name="options">The <see cref="OpenApiOptions"/> to configure.</param>
    /// <param name="filePath">An absolute path to the file that contains XML comments.</param>
    /// <param name="includeControllerXmlComments">
    /// If <see langword="true"/>, service (controller) XML summaries are used to populate tag descriptions.
    /// </param>
    public static OpenApiOptions IncludeGrpcXmlComments(
        this OpenApiOptions options,
        string filePath,
        bool includeControllerXmlComments)
        => options.IncludeGrpcXmlComments(() => new XPathDocument(filePath), includeControllerXmlComments);
}
