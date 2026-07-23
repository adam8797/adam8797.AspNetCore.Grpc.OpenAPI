// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Count;
using Greet;
using Microsoft.AspNetCore.Builder;
using Grpc.Community.OpenApi.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Grpc.Community.OpenApi.Tests;

public class GrpcOpenApiServiceExtensionsTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public GrpcOpenApiServiceExtensionsTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task AddGrpcOpenApi_GrpcServiceRegistered_ReturnDocumentWithGrpcOperation()
    {
        // Arrange & Act
        var serviceProvider = OpenApiTestHelpers.BuildServiceProvider<GreeterService>("v1");
        var document = await OpenApiTestHelpers.GetDocumentAsync(serviceProvider, "v1", _testOutputHelper);

        // Assert
        Assert.NotNull(document);
        Assert.Single(document.Paths);

        var path = document.Paths["/v1/greeter/{name}"];
        Assert.True(path.Operations.TryGetValue(HttpMethod.Get, out var operation));
        Assert.Equal("OK", operation.Responses["200"].Description);
        Assert.Equal("Error", operation.Responses["default"].Description);
    }

    [Fact]
    public async Task AddGrpcOpenApi_GrpcServiceWithGroupName_FilteredByGroup()
    {
        // Arrange & Act
        var serviceProvider = OpenApiTestHelpers.BuildServiceProvider(
            endpoints =>
            {
                endpoints.MapGrpcService<GreeterService>();
                endpoints.MapGrpcService<CounterService>();
            },
            "v1",
            "v2");

        // Assert 1 - the counter service is grouped into "v2", so it is excluded from "v1".
        var v1Document = await OpenApiTestHelpers.GetDocumentAsync(serviceProvider, "v1", _testOutputHelper);
        Assert.Single(v1Document.Paths);
        Assert.True(v1Document.Paths["/v1/greeter/{name}"].Operations.ContainsKey(HttpMethod.Get));

        // Assert 2 - the greeter service has no group, so it appears in every document.
        var v2Document = await OpenApiTestHelpers.GetDocumentAsync(serviceProvider, "v2", _testOutputHelper);
        Assert.Equal(2, v2Document.Paths.Count);
        Assert.True(v2Document.Paths["/v1/greeter/{name}"].Operations.ContainsKey(HttpMethod.Get));
        Assert.True(v2Document.Paths["/v1/add/{value1}/{value2}"].Operations.ContainsKey(HttpMethod.Get));
    }

    private class GreeterService : Greeter.GreeterBase
    {
    }

    [ApiExplorerSettings(GroupName = "v2")]
    private class CounterService : Counter.CounterBase
    {
    }
}
