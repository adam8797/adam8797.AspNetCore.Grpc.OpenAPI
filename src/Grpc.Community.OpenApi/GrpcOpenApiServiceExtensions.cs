// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Grpc.Shared;
using Grpc.Community.OpenApi.Internal;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for wiring gRPC JSON transcoding into ASP.NET Core's built-in OpenAPI generator.
/// </summary>
public static class GrpcOpenApiServiceExtensions
{
    /// <summary>
    /// Adds gRPC JSON transcoding services and hooks the generated endpoints into
    /// <c>Microsoft.AspNetCore.OpenApi</c>'s document generation for the default document.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddGrpcOpenApi(this IServiceCollection services)
        => services.AddGrpcOpenApi(documentName: "v1");

    /// <summary>
    /// Adds gRPC JSON transcoding services and hooks the generated endpoints into
    /// <c>Microsoft.AspNetCore.OpenApi</c>'s document generation for the specified document.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
    /// <param name="documentName">The name of the OpenAPI document the gRPC schema transformer should apply to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddGrpcOpenApi(this IServiceCollection services, string documentName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(documentName);

        services.AddGrpc().AddJsonTranscoding();

        services.TryAddEnumerable(ServiceDescriptor.Transient<IApiDescriptionProvider, GrpcJsonTranscodingDescriptionProvider>());
        services.TryAddSingleton<DescriptorRegistry>();

        // Register default description provider in case MVC is not registered
        services.TryAddSingleton<IApiDescriptionGroupCollectionProvider>(serviceProvider =>
        {
            var actionDescriptorCollectionProvider = serviceProvider.GetService<IActionDescriptorCollectionProvider>();
            var apiDescriptionProvider = serviceProvider.GetServices<IApiDescriptionProvider>();

            return new ApiDescriptionGroupCollectionProvider(
                actionDescriptorCollectionProvider ?? new EmptyActionDescriptorCollectionProvider(),
                apiDescriptionProvider);
        });

        // Register the schema transformer that rewrites protobuf message / enum
        // schemas into their proto-JSON-shaped equivalents.
        services.TryAddSingleton<GrpcSchemaTransformer>();

        services.AddOpenApi(documentName);
        services.AddOptions<OpenApiOptions>(documentName).Configure<GrpcSchemaTransformer>((options, transformer) =>
        {
            options.AddSchemaTransformer(transformer);
        });

        return services;
    }

    // Dummy type that is only used if MVC is not registered in the app
    private sealed class EmptyActionDescriptorCollectionProvider : IActionDescriptorCollectionProvider
    {
        public ActionDescriptorCollection ActionDescriptors { get; } = new ActionDescriptorCollection(new List<ActionDescriptor>(), 1);
    }
}
