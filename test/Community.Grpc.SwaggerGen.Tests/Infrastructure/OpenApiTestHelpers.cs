// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Community.Grpc.SwaggerGen.Tests.Infrastructure;

internal static class OpenApiTestHelpers
{
    public static OpenApiDocument GetOpenApiDocument<TService>(ITestOutputHelper testOutputHelper) where TService : class
    {
        var services = new ServiceCollection();
        services.AddGrpcSwagger();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

            var filePath = Path.Combine(System.AppContext.BaseDirectory, "Community.Grpc.SwaggerGen.Tests.xml");
            c.IncludeXmlComments(filePath);
            c.IncludeGrpcXmlComments(filePath, includeControllerXmlComments: true);
        });
        services.AddRouting();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment, TestWebHostEnvironment>();
        var serviceProvider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(serviceProvider);

        app.UseRouting();
        app.UseEndpoints(c =>
        {
            c.MapGrpcService<TService>();
        });

        var swaggerGenerator = serviceProvider.GetRequiredService<ISwaggerProvider>();
        var swagger = swaggerGenerator.GetSwagger("v1");

        using var outputString = new StringWriter();
        swagger.SerializeAsV3(new OpenApiJsonWriter(outputString));
        testOutputHelper.WriteLine(outputString.ToString());

        return swagger;
    }

    /// <summary>
    /// Follows a schema "$ref" to the component it names.
    /// </summary>
    /// <remarks>
    /// Replaces the OpenAPI.NET v1 <c>OpenApiDocument.ResolveReference</c> API. The v2
    /// <c>OpenApiSchemaReference.Target</c> property can't be used here because
    /// Swashbuckle creates its references during schema generation, before the document
    /// exists, so they carry no host document to resolve against.
    /// </remarks>
    public static IOpenApiSchema ResolveSchema(this OpenApiDocument document, IOpenApiSchema schema)
    {
        var reference = Assert.IsType<OpenApiSchemaReference>(schema);
        return document.Components.Schemas[reference.Reference.Id];
    }
}
