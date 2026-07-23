// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Shared;
using Messages;
using Community.Grpc.SwaggerGen.Internal;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Community.Grpc.SwaggerGen.Tests;

public class SchemaGeneratorIntegrationTests
{
    // In OpenAPI.NET v2 a "$ref" is no longer a property on the schema — it is a
    // distinct OpenApiSchemaReference implementation of IOpenApiSchema.
    private static string ReferenceId(IOpenApiSchema schema) =>
        Assert.IsType<OpenApiSchemaReference>(schema).Reference.Id;

    private (IOpenApiSchema Schema, SchemaRepository SchemaRepository) GenerateSchema(System.Type type, IDescriptor descriptor)
    {
        var descriptorRegistry = new DescriptorRegistry();
        descriptorRegistry.RegisterFileDescriptor(descriptor.File);

        var dataContractResolver = new GrpcDataContractResolver(new JsonSerializerDataContractResolver(new JsonSerializerOptions()), descriptorRegistry);
        var schemaGenerator = new SchemaGenerator(new SchemaGeneratorOptions(), dataContractResolver);
        var schemaRepository = new SchemaRepository();

        var schema = schemaGenerator.GenerateSchema(type, schemaRepository);

        return (schema, schemaRepository);
    }

    [Fact]
    public void GenerateSchema_EnumValue_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(EnumMessage), EnumMessage.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Single(schema.Properties);

        var enumSchema = repository.Schemas[ReferenceId(schema.Properties["enumValue"])];
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
    public void GenerateSchema_EnumWithoutMessage_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(EnumWithoutMessage), MessagesReflection.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
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
    public void GenerateSchema_BasicMessage_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(HelloReply), HelloReply.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Equal(2, schema.Properties.Count);
        Assert.Equal(JsonSchemaType.String, schema.Properties["message"].Type);
        var valuesSchema = schema.Properties["values"];
        Assert.Equal(JsonSchemaType.Array, valuesSchema.Type);
        Assert.NotNull(valuesSchema.Items);
        Assert.Equal(JsonSchemaType.String, valuesSchema.Items.Type);
    }

    [Fact]
    public void GenerateSchema_RecursiveMessage_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(RecursiveMessage), RecursiveMessage.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Single(schema.Properties);
        Assert.Equal("RecursiveMessage", ReferenceId(schema.Properties["child"]));
    }

    [Fact]
    public void GenerateSchema_BytesMessage_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(BytesMessage), BytesMessage.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Equal(2, schema.Properties.Count);
        Assert.Equal(JsonSchemaType.String, schema.Properties["bytesValue"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["bytesNullableValue"].Type);
    }

    [Fact]
    public void GenerateSchema_ListValues_ReturnSchema()
    {
        // Arrange & Act
        var (schema, _) = GenerateSchema(typeof(ListValue), ListValue.Descriptor);

        // Assert
        Assert.Equal(JsonSchemaType.Array, schema.Type);
        Assert.NotNull(schema.Items);
        Assert.Null(schema.Items.Type);
    }

    [Fact]
    public void GenerateSchema_Struct_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(Struct), Struct.Descriptor);

        _ = repository.Schemas.Count;

        // Assert
        Assert.Equal("Struct", ReferenceId(schema));

        var resolvedSchema = repository.Schemas[ReferenceId(schema)];

        Assert.Equal(JsonSchemaType.Object, resolvedSchema.Type);
        Assert.Empty(resolvedSchema.Properties);
        Assert.NotNull(resolvedSchema.AdditionalProperties);
        Assert.Null(resolvedSchema.AdditionalProperties.Type);
    }

    [Fact]
    public void GenerateSchema_Any_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(Any), Any.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.NotNull(schema.AdditionalProperties);
        Assert.Null(schema.AdditionalProperties.Type);
        Assert.Single(schema.Properties);
        Assert.Equal(JsonSchemaType.String, schema.Properties["@type"].Type);
    }

    [Fact]
    public void GenerateSchema_OneOf_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(OneOfMessage), OneOfMessage.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Equal(4, schema.Properties.Count);
        Assert.Equal(JsonSchemaType.String, schema.Properties["firstOne"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["firstTwo"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["secondOne"].Type);
        Assert.Equal(JsonSchemaType.String, schema.Properties["secondTwo"].Type);
        Assert.Null(schema.AdditionalProperties);
    }

    [Fact]
    public void GenerateSchema_Map_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(MapMessage), MapMessage.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Single(schema.Properties);
        Assert.Equal(JsonSchemaType.Object, schema.Properties["mapValue"].Type);
        Assert.Equal(JsonSchemaType.Number, schema.Properties["mapValue"].AdditionalProperties.Type);
        Assert.Equal("double", schema.Properties["mapValue"].AdditionalProperties.Format);
    }

    [Fact]
    public void GenerateSchema_FieldMask_ReturnSchema()
    {
        // Arrange & Act
        var (schema, repository) = GenerateSchema(typeof(FieldMaskMessage), FieldMaskMessage.Descriptor);

        // Assert
        schema = repository.Schemas[ReferenceId(schema)];
        Assert.Equal(JsonSchemaType.Object, schema.Type);
        Assert.Single(schema.Properties);
        Assert.Equal(JsonSchemaType.String, schema.Properties["fieldMaskValue"].Type);
    }
}
