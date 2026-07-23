// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Grpc.Community.OpenApi.Tests.Infrastructure;
using Grpc.Community.OpenApi.Tests.Services;

namespace Grpc.Community.OpenApi.Tests.Binding;

public class BodyTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public BodyTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task PostRepeated()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<BodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/body1"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.RequestBody.Content["application/json"].Schema;
        Assert.IsNotType<OpenApiSchemaReference>(bodySchema);
        Assert.Equal(JsonSchemaType.Array, bodySchema.Type);

        var itemsReference = Assert.IsType<OpenApiSchemaReference>(bodySchema.Items);
        Assert.Equal("RequestBody", itemsReference.Reference.Id);
        Assert.NotNull(swagger.ResolveSchema(bodySchema.Items));
    }

    [Fact]
    public async Task PostMap()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<BodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/body2"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.RequestBody.Content["application/json"].Schema;
        Assert.IsNotType<OpenApiSchemaReference>(bodySchema);
        Assert.Equal(JsonSchemaType.Object, bodySchema.Type);
        Assert.Equal(JsonSchemaType.Integer, bodySchema.AdditionalProperties.Type);
    }

    [Fact]
    public async Task PostMessage()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<BodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/body3"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.RequestBody.Content["application/json"].Schema;
        Assert.Equal("RequestBody", Assert.IsType<OpenApiSchemaReference>(bodySchema).Reference.Id);
    }

    [Fact]
    public async Task PostRoot()
    {
        // Arrange & Act
        var swagger = await OpenApiTestHelpers.GetOpenApiDocumentAsync<BodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/body4"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.RequestBody.Content["application/json"].Schema;
        Assert.Equal("RequestOne", Assert.IsType<OpenApiSchemaReference>(bodySchema).Reference.Id);
    }
}
