// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Grpc.Community.OpenApi.Tests.Infrastructure;
using Grpc.Community.OpenApi.Tests.Services;

namespace Grpc.Community.OpenApi.Tests;

/// <summary>
/// Covers the proto-JSON shapes that <c>GrpcSchemaTransformer</c> produces, asserted
/// against the components of a real generated document.
/// </summary>
/// <remarks>
/// The Swashbuckle suite drove <c>SchemaGenerator</c> directly with a CLR type. The
/// ASP.NET Core equivalent only runs inside the document pipeline, so the fixtures from
/// messages.proto are exposed through the Messages service instead.
/// </remarks>
public class SchemaTransformerIntegrationTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public SchemaTransformerIntegrationTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private Task<OpenApiDocument> GetDocumentAsync()
        => OpenApiTestHelpers.GetOpenApiDocumentAsync<MessagesService>(_testOutputHelper);

    [Fact]
    public async Task EnumValue_ReturnsStringEnumSchema()
    {
        var document = await GetDocumentAsync();

        var schema = document.Components.Schemas["EnumMessage"];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Single(schema.Properties);

        var enumSchema = document.ResolveSchema(schema.Properties["enumValue"]);
        Assert.Equal(JsonSchemaType.String, enumSchema.Type);
        Assert.Equal(5, enumSchema.Enum.Count);

        var enumValues = enumSchema.Enum.Select(e => e!.GetValue<string>()).OrderBy(s => s).ToList();
        Assert.Collection(enumValues,
            v => Assert.Equal("BAR", v),
            v => Assert.Equal("BAZ", v),
            v => Assert.Equal("FOO", v),
            v => Assert.Equal("NEG", v),
            v => Assert.Equal("NESTED_ENUM_UNSPECIFIED", v));
    }

    [Fact]
    public async Task EnumWithoutMessage_ReturnsStringEnumSchema()
    {
        var document = await GetDocumentAsync();

        var schema = document.Components.Schemas["EnumWithoutMessage"];
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal(5, schema.Enum.Count);

        var enumValues = schema.Enum.Select(e => e!.GetValue<string>()).OrderBy(s => s).ToList();
        Assert.Collection(enumValues,
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_BAR", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_BAZ", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_FOO", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_NEG", v),
            v => Assert.Equal("ENUM_WITHOUT_MESSAGE_UNSPECIFIED", v));
    }

    [Fact]
    public async Task BasicMessage_ReturnsObjectSchema()
    {
        var document = await GetDocumentAsync();

        var schema = document.Components.Schemas["HelloReply"];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Equal(2, schema.Properties.Count);
        Assert.Equal(JsonSchemaType.String, schema.Properties["message"].Type);

        var valuesSchema = schema.Properties["values"];
        Assert.Equal(JsonSchemaType.Array, valuesSchema.Type);
        Assert.NotNull(valuesSchema.Items);
        Assert.Equal(JsonSchemaType.String, valuesSchema.Items.Type);
    }

    [Fact]
    public async Task BytesMessage_ReturnsStringProperties()
    {
        var document = await GetDocumentAsync();

        var schema = document.Components.Schemas["BytesMessage"];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Equal(2, schema.Properties.Count);
        Assert.Equal(JsonSchemaType.String, schema.Properties["bytesValue"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["bytesNullableValue"].Type);
    }

    [Fact]
    public async Task OneOf_FlattensIntoProperties()
    {
        var document = await GetDocumentAsync();

        var schema = document.Components.Schemas["OneOfMessage"];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Equal(4, schema.Properties.Count);
        Assert.Equal(JsonSchemaType.String, schema.Properties["firstOne"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["firstTwo"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["secondOne"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["secondTwo"].Type);
        Assert.False(schema.AdditionalPropertiesAllowed);
    }

    [Fact]
    public async Task Map_ReturnsObjectWithAdditionalProperties()
    {
        var document = await GetDocumentAsync();

        var schema = document.Components.Schemas["MapMessage"];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Single(schema.Properties);

        var mapSchema = schema.Properties["mapValue"];
        Assert.Equal(JsonSchemaType.Object, mapSchema.Type);
        Assert.Equal(JsonSchemaType.Number, mapSchema.AdditionalProperties.Type);
        Assert.Equal("double", mapSchema.AdditionalProperties.Format);
    }

    [Fact]
    public async Task FieldMask_ReturnsStringProperty()
    {
        var document = await GetDocumentAsync();

        var schema = document.Components.Schemas["FieldMaskMessage"];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Single(schema.Properties);
        Assert.Equal(JsonSchemaType.String, schema.Properties["fieldMaskValue"].Type);
    }

    [Fact]
    public async Task Any_InlinesWithTypeDiscriminator()
    {
        var document = await GetDocumentAsync();

        var holder = document.Components.Schemas["WellKnownHolder"];
        var anySchema = holder.Properties["anyValue"];

        Assert.Equal(JsonSchemaType.Object, anySchema.Type);
        Assert.NotNull(anySchema.AdditionalProperties);
        Assert.Null(anySchema.AdditionalProperties.Type);
        Assert.Single(anySchema.Properties);
        Assert.Equal(JsonSchemaType.String, anySchema.Properties["@type"].Type);
    }

    [Fact]
    public async Task Struct_InlinesAsFreeFormObject()
    {
        var document = await GetDocumentAsync();

        var holder = document.Components.Schemas["WellKnownHolder"];
        var structSchema = holder.Properties["structValue"];

        Assert.Equal(JsonSchemaType.Object, structSchema.Type);
        Assert.Empty(structSchema.Properties ?? new Dictionary<string, IOpenApiSchema>());
        Assert.NotNull(structSchema.AdditionalProperties);
        Assert.Null(structSchema.AdditionalProperties.Type);
    }

    [Fact]
    public async Task ListValue_InlinesAsUnconstrainedArray()
    {
        var document = await GetDocumentAsync();

        var holder = document.Components.Schemas["WellKnownHolder"];
        var listSchema = holder.Properties["listValue"];

        Assert.Equal(JsonSchemaType.Array, listSchema.Type);
        Assert.NotNull(listSchema.Items);
        Assert.Null(listSchema.Items.Type);
    }

    [Fact]
    public async Task StringWrapper_InlinesAsUnderlyingScalar()
    {
        var document = await GetDocumentAsync();

        var holder = document.Components.Schemas["WellKnownHolder"];
        Assert.Equal(JsonSchemaType.String, holder.Properties["stringValue"].Type);
    }
}
