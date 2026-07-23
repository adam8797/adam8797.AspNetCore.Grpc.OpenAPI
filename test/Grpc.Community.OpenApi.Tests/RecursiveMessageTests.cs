// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Grpc.Community.OpenApi.Tests.Infrastructure;
using Grpc.Community.OpenApi.Tests.Services;

namespace Grpc.Community.OpenApi.Tests;

/// <summary>
/// A protobuf message that references itself cannot currently be represented.
/// </summary>
/// <remarks>
/// The OpenAPI schema pipeline re-enters a schema transformer with a fresh schema every
/// time it walks a "$ref" cycle, so the rewrite never terminates. Nothing inside a
/// transformer can detect this, and left alone it kills the process with a
/// StackOverflowException. The transformer therefore gives up with a diagnosable error
/// instead. See BUG-REPORT-aspnetcore-openapi-schema-recursion.md in the repository root.
/// </remarks>
public class RecursiveMessageTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public RecursiveMessageTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task SelfReferencingMessage_ThrowsInsteadOfOverflowingTheStack()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => OpenApiTestHelpers.GetOpenApiDocumentAsync<RecursiveMessagesService>(_testOutputHelper));

        Assert.Contains("messages.RecursiveMessage", exception.Message, StringComparison.Ordinal);
        Assert.Contains("references itself", exception.Message, StringComparison.Ordinal);
    }
}
