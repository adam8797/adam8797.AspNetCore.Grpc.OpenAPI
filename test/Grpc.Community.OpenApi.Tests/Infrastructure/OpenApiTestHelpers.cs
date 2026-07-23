// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Grpc.Community.OpenApi.Tests.Infrastructure;

internal static class OpenApiTestHelpers
{
    public const string DocumentName = "v1";

    public static Task<OpenApiDocument> GetOpenApiDocumentAsync<TService>(ITestOutputHelper testOutputHelper)
        where TService : class
    {
        var serviceProvider = BuildServiceProvider<TService>(DocumentName);
        return GetDocumentAsync(serviceProvider, DocumentName, testOutputHelper);
    }

    /// <summary>
    /// Builds a service provider with gRPC JSON transcoding wired into ASP.NET Core's
    /// OpenAPI generation, and maps <typeparamref name="TService"/> as an endpoint.
    /// </summary>
    public static IServiceProvider BuildServiceProvider<TService>(params string[] documentNames)
        where TService : class
        => BuildServiceProvider(endpoints => endpoints.MapGrpcService<TService>(), documentNames);

    public static IServiceProvider BuildServiceProvider(Action<IEndpointRouteBuilder> mapEndpoints, params string[] documentNames)
    {
        var services = new ServiceCollection();

        foreach (var documentName in documentNames)
        {
            services.AddGrpcOpenApi(documentName);
            services.Configure<OpenApiOptions>(documentName, options =>
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "Grpc.Community.OpenApi.Tests.xml");
                options.IncludeGrpcXmlComments(filePath, includeControllerXmlComments: true);
            });
        }

        services.AddRouting();
        services.AddLogging();

        var hostEnvironment = new TestWebHostEnvironment();
        services.AddSingleton<IWebHostEnvironment>(hostEnvironment);
        services.AddSingleton<IHostEnvironment>(hostEnvironment);

        var serviceProvider = services.BuildServiceProvider();

        // Building the endpoint pipeline is what makes the gRPC transcoding endpoints
        // visible to the API explorer, which is where OpenAPI generation reads from.
        var app = new ApplicationBuilder(serviceProvider);
        app.UseRouting();
        app.UseEndpoints(mapEndpoints);

        return serviceProvider;
    }

    public static async Task<OpenApiDocument> GetDocumentAsync(
        IServiceProvider serviceProvider,
        string documentName,
        ITestOutputHelper testOutputHelper)
    {
        var documentProvider = serviceProvider.GetRequiredKeyedService<IOpenApiDocumentProvider>(documentName);
        var document = await documentProvider.GetOpenApiDocumentAsync();

        using var outputString = new StringWriter();
        document.SerializeAsV3(new OpenApiJsonWriter(outputString));
        testOutputHelper.WriteLine(outputString.ToString());

        return document;
    }

    /// <summary>
    /// Follows a schema "$ref" to the component it names.
    /// </summary>
    /// <remarks>
    /// Replaces the OpenAPI.NET v1 <c>OpenApiDocument.ResolveReference</c> API. The v2
    /// <c>OpenApiSchemaReference.Target</c> property isn't used here because references
    /// are created during schema generation, before the document exists, so they carry
    /// no host document to resolve against.
    /// </remarks>
    public static IOpenApiSchema ResolveSchema(this OpenApiDocument document, IOpenApiSchema schema)
    {
        var reference = Assert.IsType<OpenApiSchemaReference>(schema);
        return document.Components.Schemas[reference.Reference.Id];
    }
}
