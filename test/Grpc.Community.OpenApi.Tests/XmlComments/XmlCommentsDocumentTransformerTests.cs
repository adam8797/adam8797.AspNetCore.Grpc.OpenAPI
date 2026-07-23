// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.XPath;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Grpc.Community.OpenApi.Internal.XmlComments;
using Grpc.Community.OpenApi.Tests.Infrastructure;
using Grpc.Community.OpenApi.Tests.Services;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Grpc.Community.OpenApi.Tests.XmlComments;

public class XmlCommentsDocumentTransformerTests
{
    private class TestMethod : IMethod
    {
        public MethodType Type { get; }
        public string ServiceName { get; } = "TestServiceName";
        public string Name { get; } = "TestName";
        public string FullName => ServiceName + "." + Name;
    }

    [Theory]
    [InlineData(typeof(XmlDocService), "XmlDoc!")]
    [InlineData(typeof(XmlDocServiceWithComments), "XmlDocServiceWithComments XML comment!")]
    public async Task Transform_SetsTagDescription_FromControllerSummaryTags(Type serviceType, string expectedDescription)
    {
        // Arrange
        var document = new OpenApiDocument();
        var context = new OpenApiDocumentTransformerContext
        {
            DocumentName = OpenApiTestHelpers.DocumentName,
            DescriptionGroups =
            [
                new ApiDescriptionGroup(
                    groupName: null,
                    items:
                    [
                        CreateApiDescription(serviceType),
                        CreateApiDescription(serviceType)
                    ])
            ],
            ApplicationServices = new ServiceCollection().BuildServiceProvider(),
        };

        // Act
        await Subject().TransformAsync(document, context, CancellationToken.None);

        // Assert
        Assert.Single(document.Tags);
        Assert.Equal(expectedDescription, document.Tags.First().Description);

        static ApiDescription CreateApiDescription(Type serviceType)
        {
            return new ApiDescription
            {
                ActionDescriptor = new ActionDescriptor
                {
                    RouteValues =
                    {
                        ["controller"] = "greet.Greeter"
                    },
                    EndpointMetadata = new List<object>
                    {
                        new GrpcMethodMetadata(serviceType, new TestMethod())
                    }
                }
            };
        }
    }

    private static GrpcXmlCommentsDocumentTransformer Subject()
    {
        using var xmlComments = File.OpenText($"{typeof(GreeterService).Assembly.GetName().Name}.xml");
        return new GrpcXmlCommentsDocumentTransformer(new XPathDocument(xmlComments));
    }
}
