# Grpc.Community.OpenApi

In [this commit](https://github.com/dotnet/aspnetcore/commit/39e1ac975cf138c56fff984dd824ab651a448367) Microsoft removed the `Microsoft.AspNetCore.Grpc.Swagger` package. Some of us used it, and others of us had plans to use it.

This repo is meant to continue the life of that package, and even port it to OpenAPI as well. 

The initial commit will be the extracted code, verbatim. You can follow through the commit history to see whats been modified.

# Notes
Best I can tell, the source libraries were targeting `Microsoft.OpenApi 1.0`. I've made the choice to start with .NET 10 support, so I bumped `Microsoft.OpenApi` to the latest 2.x and made the relevant change to support that.

When .NET 11 ships, I'll multitarget and support `Microsoft.OpenApi 3.0`, but that may just be for the OpenApi version of the package, we'll see when we get there.

# Examples

You'll find examples in the `examples/` directory, this library is generally a one-liner to get added.

# Status

I've freshly copied the code and made the needed modifications to build. There's a few kinks to iron out, but I plan on trying to get the Swagger-based package published as `Grpc.Community.SwaggerGen` soon™

After that, I'll see about porting it to OpenApi, and then we'll publish `Grpc.Community.OpenApi` as a separate package. 
