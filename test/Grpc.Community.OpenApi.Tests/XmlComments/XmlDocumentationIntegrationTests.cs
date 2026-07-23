// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Greet;
using Grpc.Community.OpenApi.Tests.Infrastructure;
using Grpc.Community.OpenApi.Tests.Services;

namespace Grpc.Community.OpenApi.Tests.XmlComments;

public class XmlDocumentationIntegrationTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public XmlDocumentationIntegrationTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task ServiceDescription_ModelHasXmlDocs_UseXmlDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocServiceWithComments>(_testOutputHelper);

        // Assert
        Assert.Equal("XmlDoc", swagger.Tags.First().Name);
        Assert.Equal("XmlDocServiceWithComments XML comment!", swagger.Tags.First().Description);
    }

    [Fact]
    public async Task ServiceDescription_ModelDoesntHaveXmlDocs_UseProtoDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocService>(_testOutputHelper);

        // Assert
        Assert.Equal("XmlDoc", swagger.Tags.First().Name);
        Assert.Equal("XmlDoc!", swagger.Tags.First().Description);
    }

    [Fact]
    public async Task RouteParameter_UseProtoDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocServiceWithComments>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/greeter/{name}"];
        Assert.Equal("Name field!", path.Operations[HttpMethod.Get].Parameters[0].Description);
    }

    [Fact]
    public async Task MethodDescription_ModelHasXmlDocs_UseXmlDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocServiceWithComments>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/greeter/{name}"];
        Assert.Equal("BasicGet XML summary!", path.Operations[HttpMethod.Get].Summary);
        Assert.Equal("BasicGet XML remarks!", path.Operations[HttpMethod.Get].Description);
    }

    [Fact]
    public async Task MethodDescription_ModelDoesntHaveXmlDocs_UseProtoDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/greeter/{name}"];
        Assert.Equal("BasicGet!", path.Operations[HttpMethod.Get].Summary);
        Assert.Null(path.Operations[HttpMethod.Get].Description);
    }

    [Fact]
    public async Task RequestDescription_Root_ModelHasXmlDocs_UseXmlDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocServiceWithComments>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/greeter"];
        Assert.Equal("Request XML param!", path.Operations[HttpMethod.Post].RequestBody.Description);
    }

    [Fact]
    public async Task RequestDescription_Root_ModelDoesntHaveXmlDocs_Empty()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/greeter"];
        Assert.Null(path.Operations[HttpMethod.Post].RequestBody.Description);
    }

    [Fact]
    public async Task RequestDescription_Nested_ProtoFieldDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/greeter/{name}"];
        Assert.Equal("Detail field!", path.Operations[HttpMethod.Post].RequestBody.Description);
    }

    [Fact]
    public async Task Parameters_QueryParameters_ProtoFieldDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/greeter/query/{name}"];
        Assert.Collection(path.Operations[HttpMethod.Get].Parameters,
            p =>
            {
                Assert.Equal(ParameterLocation.Path, p.In);
                Assert.Equal("name", p.Name);
                Assert.Equal("Name field!", p.Description);
            },
            p =>
            {
                Assert.Equal(ParameterLocation.Query, p.In);
                Assert.Equal("detail.age", p.Name);
                Assert.Equal("Age field!", p.Description);
            });
    }

    [Fact]
    public async Task Message_UseProtoDocs()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<XmlDocServiceWithComments>(_testOutputHelper);

        // Assert
        var helloReplyMessage = swagger.Components.Schemas["StringReply"];
        Assert.Equal("StringReply!", helloReplyMessage.Description);
        Assert.Equal("Message field!", helloReplyMessage.Properties["message"].Description);
    }

    private class GreeterService : Greeter.GreeterBase
    {
    }
}
