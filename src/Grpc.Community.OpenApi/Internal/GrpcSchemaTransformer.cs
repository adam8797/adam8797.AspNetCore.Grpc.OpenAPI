// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Grpc.Shared;
using Microsoft.AspNetCore.OpenApi;
using Type = System.Type;

namespace Grpc.Community.OpenApi.Internal;

/// <summary>
/// Rewrites OpenAPI schemas that correspond to protobuf-generated CLR types
/// so that they match the shape produced by the gRPC JSON transcoding
/// serializer rather than the reflection-derived CLR shape.
/// </summary>
internal sealed class GrpcSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly ConditionalWeakTable<OpenApiDocument, ConcurrentDictionary<Type, int>> MessageVisits = new();

    private readonly DescriptorRegistry _descriptorRegistry;

    public GrpcSchemaTransformer(DescriptorRegistry descriptorRegistry)
    {
        _descriptorRegistry = descriptorRegistry;
    }

    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        var descriptor = _descriptorRegistry.FindDescriptorByType(type);

        if (descriptor is MessageDescriptor messageDescriptor)
        {
            GuardAgainstRunawayRecursion(context, messageDescriptor);
            ApplyMessage(schema, messageDescriptor);
        }
        else if (descriptor is EnumDescriptor enumDescriptor)
        {
            ApplyEnum(schema, enumDescriptor);
        }
        else if (TryGetProtobufCollection(type, out var collectionKind, out var elementType))
        {
            // A repeated or map field can itself be the root of a schema when it is bound
            // as the request body or as a query parameter. There is no descriptor for the
            // CLR collection type, so the shape has to be rebuilt from the element type -
            // otherwise the serializer's default representation leaks into the document.
            await ApplyCollectionAsync(schema, collectionKind, elementType, context, cancellationToken);
        }
    }

    private enum ProtobufCollectionKind
    {
        Repeated,
        Map,
    }

    private static bool TryGetProtobufCollection(Type type, out ProtobufCollectionKind kind, out Type elementType)
    {
        if (type.IsGenericType)
        {
            // Repeated and map fields reach this point either as the protobuf collection
            // type (when bound from a property) or as the interface that
            // MessageDescriptorHelpers.ResolveFieldType produces (when bound as a parameter).
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(RepeatedField<>) || definition == typeof(IList<>))
            {
                kind = ProtobufCollectionKind.Repeated;
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            if (definition == typeof(MapField<,>) || definition == typeof(IDictionary<,>))
            {
                // Proto JSON always encodes map keys as strings, so only the value matters.
                kind = ProtobufCollectionKind.Map;
                elementType = type.GetGenericArguments()[1];
                return true;
            }
        }

        kind = default;
        elementType = null!;
        return false;
    }

    private async Task ApplyCollectionAsync(
        OpenApiSchema schema,
        ProtobufCollectionKind kind,
        Type elementType,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var elementSchema = await BuildClrTypeSchemaAsync(elementType, context, cancellationToken);

        ResetSchema(schema);
        if (kind == ProtobufCollectionKind.Repeated)
        {
            schema.Type = JsonSchemaType.Array;
            schema.Items = elementSchema;
        }
        else
        {
            schema.Type = JsonSchemaType.Object;
            schema.AdditionalPropertiesAllowed = true;
            schema.AdditionalProperties = elementSchema;
        }
    }

    /// <summary>
    /// Builds the proto-JSON schema for a CLR type, referencing it as a component when
    /// it corresponds to a (non well-known) protobuf message or enum.
    /// </summary>
    private async Task<IOpenApiSchema> BuildClrTypeSchemaAsync(
        Type clrType,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        switch (_descriptorRegistry.FindDescriptorByType(clrType))
        {
            case MessageDescriptor message when ServiceDescriptorHelpers.IsWellKnownType(message):
                var wellKnown = new OpenApiSchema();
                TryApplyWellKnown(wellKnown, message);
                return wellKnown;

            // Safe to resolve here: a collection is only the root of a schema when it is
            // bound as a request body or parameter, so this is the outermost schema rather
            // than a property of a type the pipeline is already building.
            case MessageDescriptor or EnumDescriptor:
                return await context.GetOrCreateSchemaAsync(clrType, cancellationToken: cancellationToken);

            default:
                var scalar = new OpenApiSchema();
                ApplyScalarType(scalar, Nullable.GetUnderlyingType(clrType) ?? clrType);
                return scalar;
        }
    }

    private static void ApplyEnum(OpenApiSchema schema, EnumDescriptor enumDescriptor)
    {
        ResetSchema(schema);
        schema.Type = JsonSchemaType.String;
        schema.Enum = enumDescriptor.Values.Select(v => (JsonNode)v.Name).ToList();
    }

    private static void ApplyMessage(OpenApiSchema schema, MessageDescriptor descriptor)
    {
        if (TryApplyWellKnown(schema, descriptor))
        {
            return;
        }


        // The schema the framework generated already holds a correct reference for every
        // message- and enum-typed property, including the back-reference that makes a
        // self-referencing message terminate. Those are reused rather than re-resolved:
        // asking the pipeline for the schema of a type it is already building recurses
        // until the stack overflows.
        var generated = schema.Properties;

        // Built before resetting, so that the sub-schemas survive the reset. Only proto
        // fields are carried over, which drops the serializer's extras - "hasXxx"
        // presence flags and the "xxxCase" discriminators synthesised for oneofs.
        var properties = new Dictionary<string, IOpenApiSchema>();
        foreach (var field in descriptor.Fields.InFieldNumberOrder())
        {
            properties[field.JsonName] = BuildFieldSchema(field, FindGeneratedProperty(generated, field));
        }

        ResetSchema(schema);
        schema.Type = JsonSchemaType.Object;
        // A protobuf message has a closed set of fields, so no unknown properties.
        schema.AdditionalPropertiesAllowed = false;
        schema.Properties = properties;
    }

    /// <summary>
    /// Guards against the unbounded re-entry that a self-referencing message triggers.
    /// </summary>
    /// <remarks>
    /// When a message takes part in a "$ref" cycle, the OpenAPI pipeline walks back into
    /// that schema and hands this transformer a fresh instance every time, without end.
    /// Nothing inside a transformer can distinguish that from legitimate work, so the only
    /// available defence is to stop after an implausible number of visits. Without this the
    /// process dies with a StackOverflowException, which cannot be caught or diagnosed.
    /// </remarks>
    private static void GuardAgainstRunawayRecursion(OpenApiSchemaTransformerContext context, MessageDescriptor descriptor)
    {
        const int VisitLimit = 64;

        var visits = MessageVisits.GetOrCreateValue(context.Document);
        if (visits.AddOrUpdate(descriptor.ClrType, 1, static (_, count) => count + 1) <= VisitLimit)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Cannot generate an OpenAPI schema for protobuf message '{descriptor.FullName}'. " +
            "It references itself, directly or through a cycle, and the OpenAPI schema pipeline " +
            "re-enters the gRPC schema transformer for such a type without bound. " +
            "Remove the self-reference, or exclude this message from the OpenAPI document.");
    }

    /// <summary>
    /// Finds the schema the framework generated for <paramref name="field"/>.
    /// </summary>
    /// <remarks>
    /// The framework keys properties by the serializer's name for the generated CLR
    /// property, which is normally identical to the protobuf JSON name. The extra lookups
    /// cover the cases where protoc has had to mangle the C# name.
    /// </remarks>
    private static IOpenApiSchema? FindGeneratedProperty(IDictionary<string, IOpenApiSchema>? generated, FieldDescriptor field)
    {
        if (generated == null || generated.Count == 0)
        {
            return null;
        }

        if (generated.TryGetValue(field.JsonName, out var match))
        {
            return match;
        }

        var clrName = ServiceDescriptorHelpers.FormatUnderscoreName(field.Name, pascalCase: true, preservePeriod: false);
        if (generated.TryGetValue(clrName, out match))
        {
            return match;
        }

        foreach (var candidate in generated)
        {
            if (string.Equals(candidate.Key, field.JsonName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Key, clrName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate.Value;
            }
        }

        return null;
    }

    private static bool TryApplyWellKnown(OpenApiSchema schema, MessageDescriptor descriptor)
    {
        if (!ServiceDescriptorHelpers.IsWellKnownType(descriptor))
        {
            return false;
        }

        if (ServiceDescriptorHelpers.IsWrapperType(descriptor))
        {
            var valueField = descriptor.Fields[Int32Value.ValueFieldNumber];
            ResetSchema(schema);
            ApplyScalarType(schema, MessageDescriptorHelpers.ResolveFieldType(valueField));
            return true;
        }

        if (descriptor.FullName == Timestamp.Descriptor.FullName ||
            descriptor.FullName == Duration.Descriptor.FullName ||
            descriptor.FullName == FieldMask.Descriptor.FullName)
        {
            ResetSchema(schema);
            schema.Type = JsonSchemaType.String;
            return true;
        }

        if (descriptor.FullName == Struct.Descriptor.FullName)
        {
            ResetSchema(schema);
            schema.Type = JsonSchemaType.Object;
            schema.AdditionalProperties = new OpenApiSchema(); // any JSON value
            schema.AdditionalPropertiesAllowed = true;
            return true;
        }

        if (descriptor.FullName == ListValue.Descriptor.FullName)
        {
            ResetSchema(schema);
            schema.Type = JsonSchemaType.Array;
            schema.Items = new OpenApiSchema();
            return true;
        }

        if (descriptor.FullName == Value.Descriptor.FullName)
        {
            // A Value is any JSON — represented as an unconstrained schema.
            ResetSchema(schema);
            return true;
        }

        if (descriptor.FullName == Any.Descriptor.FullName)
        {
            ResetSchema(schema);
            schema.Type = JsonSchemaType.Object;
            schema.Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["@type"] = new OpenApiSchema { Type = JsonSchemaType.String },
            };
            schema.Required = new HashSet<string> { "@type" };
            schema.AdditionalProperties = new OpenApiSchema();
            schema.AdditionalPropertiesAllowed = true;
            return true;
        }

        return false;
    }

    private static IOpenApiSchema BuildFieldSchema(FieldDescriptor field, IOpenApiSchema? generated)
    {
        if (field.IsMap)
        {
            var valueField = field.MessageType.Fields.InFieldNumberOrder()[1];
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalPropertiesAllowed = true,
                AdditionalProperties = BuildLeafFieldSchema(valueField, (generated as OpenApiSchema)?.AdditionalProperties),
            };
        }

        if (field.IsRepeated)
        {
            return new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = BuildLeafFieldSchema(field, (generated as OpenApiSchema)?.Items),
            };
        }

        return BuildLeafFieldSchema(field, generated);
    }

    private static IOpenApiSchema BuildLeafFieldSchema(FieldDescriptor field, IOpenApiSchema? generated)
    {
        switch (field.FieldType)
        {
            case FieldType.Message:
                // Well-known types are inlined so that consumers of the document don't have
                // to also carry component schemas for internal Google types.
                if (ServiceDescriptorHelpers.IsWellKnownType(field.MessageType))
                {
                    var wellKnown = new OpenApiSchema();
                    TryApplyWellKnown(wellKnown, field.MessageType);
                    return wellKnown;
                }

                return generated ?? new OpenApiSchema();

            case FieldType.Enum:
                // The referenced component is rewritten into a string enum when the pipeline
                // transforms it in its own right.
                return generated ?? new OpenApiSchema();

            default:
                return BuildScalarSchema(field.FieldType);
        }
    }

    private static OpenApiSchema BuildScalarSchema(FieldType fieldType)
    {
        var schema = new OpenApiSchema();
        ApplyScalarType(schema, TypeForFieldType(fieldType));
        return schema;
    }

    private static void ApplyScalarType(OpenApiSchema schema, Type clrType)
    {
        // Protobuf JSON encodes 64-bit integers as strings, so express them as strings
        // (with an int64 format) rather than JSON numbers.
        if (clrType == typeof(long) || clrType == typeof(ulong))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = clrType == typeof(long) ? "int64" : "uint64";
            return;
        }

        if (clrType == typeof(int))
        {
            schema.Type = JsonSchemaType.Integer;
            schema.Format = "int32";
            return;
        }

        if (clrType == typeof(uint))
        {
            schema.Type = JsonSchemaType.Integer;
            schema.Format = "uint32";
            return;
        }

        if (clrType == typeof(bool))
        {
            schema.Type = JsonSchemaType.Boolean;
            return;
        }

        if (clrType == typeof(float))
        {
            schema.Type = JsonSchemaType.Number;
            schema.Format = "float";
            return;
        }

        if (clrType == typeof(double))
        {
            schema.Type = JsonSchemaType.Number;
            schema.Format = "double";
            return;
        }

        if (clrType == typeof(string))
        {
            schema.Type = JsonSchemaType.String;
            return;
        }

        // Fallback — should not happen for the enumerated scalar cases above.
        schema.Type = JsonSchemaType.String;
    }

    private static Type TypeForFieldType(FieldType fieldType) => fieldType switch
    {
        FieldType.Double => typeof(double),
        FieldType.Float => typeof(float),
        FieldType.Int64 or FieldType.Fixed64 or FieldType.SFixed64 or FieldType.SInt64 => typeof(long),
        FieldType.UInt64 => typeof(ulong),
        FieldType.Int32 or FieldType.Fixed32 or FieldType.SFixed32 or FieldType.SInt32 => typeof(int),
        FieldType.UInt32 => typeof(uint),
        FieldType.Bool => typeof(bool),
        FieldType.String or FieldType.Bytes => typeof(string),
        _ => throw new InvalidOperationException("Unexpected scalar field type: " + fieldType),
    };

    private static void ResetSchema(OpenApiSchema schema)
    {
        schema.Type = null;
        schema.Format = null;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.Enum?.Clear();
        schema.AdditionalProperties = null;
        schema.AdditionalPropertiesAllowed = true;
        schema.Items = null;
        schema.AllOf?.Clear();
        schema.OneOf?.Clear();
        schema.AnyOf?.Clear();
    }
}
