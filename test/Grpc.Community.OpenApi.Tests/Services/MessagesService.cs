// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Messages;

namespace Grpc.Community.OpenApi.Tests.Services;

/// <summary>
/// Exposes the schema fixtures from messages.proto. The method bodies are never
/// invoked - only the generated endpoint metadata is used to build a document.
/// </summary>
public class MessagesService : Messages.Messages.MessagesBase
{
}
