// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Grpc.Swagger.Tests.Infrastructure;
using Microsoft.AspNetCore.Grpc.Swagger.Tests.Services;

namespace Microsoft.AspNetCore.Grpc.Swagger.Tests.Binding;

public class ResponseBodyTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public ResponseBodyTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ResponseBodyString()
    {
        // Arrange & Act
        var swagger = OpenApiTestHelpers.GetOpenApiDocument<ResponseBodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/responsebody1"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.Responses["200"].Content["application/json"].Schema;
        Assert.Equal(JsonSchemaType.String, bodySchema.Type);
    }

    [Fact]
    public void ResponseBodyRepeatedString()
    {
        // Arrange & Act
        var swagger = OpenApiTestHelpers.GetOpenApiDocument<ResponseBodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/responsebody2"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.Responses["200"].Content["application/json"].Schema;
        Assert.IsNotType<OpenApiSchemaReference>(bodySchema);
        Assert.Equal(JsonSchemaType.Array, bodySchema.Type);
        Assert.Equal(JsonSchemaType.String, bodySchema.Items.Type);
    }

    [Fact]
    public void ResponseBodyEnum()
    {
        // Arrange & Act
        var swagger = OpenApiTestHelpers.GetOpenApiDocument<ResponseBodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/responsebody3"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.Responses["200"].Content["application/json"].Schema;

        var enumSchema = swagger.ResolveSchema(bodySchema);
        Assert.Equal(JsonSchemaType.String, enumSchema.Type);
        Assert.Equal(5, enumSchema.Enum.Count);

        var enumValues = enumSchema.Enum.Select(e => e!.GetValue<string>()).OrderBy(s => s).ToList();
        Assert.Collection(enumValues,
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_BAR", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_BAZ", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_FOO", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_NEG", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_UNSPECIFIED", v));
    }

    [Fact]
    public void ResponseBodyMessage()
    {
        // Arrange & Act
        var swagger = OpenApiTestHelpers.GetOpenApiDocument<ResponseBodyService>(_testOutputHelper);

        // Assert
        var path = swagger.Paths["/v1/responsebody4"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Post, out var operation));

        var bodySchema = operation.Responses["200"].Content["application/json"].Schema;

        var messageSchema = swagger.ResolveSchema(bodySchema);
        Assert.Equal(JsonSchemaType.Object, messageSchema.Type);
        Assert.False(messageSchema.AdditionalPropertiesAllowed);
    }
}
