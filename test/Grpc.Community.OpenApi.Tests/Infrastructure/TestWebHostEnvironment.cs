// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Grpc.Community.OpenApi.Tests.Infrastructure;

internal class TestWebHostEnvironment : IWebHostEnvironment
{
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    public string ApplicationName { get; set; } = typeof(TestWebHostEnvironment).Assembly.GetName().Name!;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public string EnvironmentName { get; set; } = "Development";
}
