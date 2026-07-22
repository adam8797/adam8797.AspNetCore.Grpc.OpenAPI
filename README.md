# Grpc.Community.OpenApi

In [this commit](https://github.com/dotnet/aspnetcore/commit/39e1ac975cf138c56fff984dd824ab651a448367) Microsoft removed the `Microsoft.AspNetCore.Grpc.Swagger` package. Some of us used it, and others of us had plans to use it.

This repo is meant to continue the life of that package, and even port it to OpenAPI as well. 

The initial commit will be the extracted code, verbatim. You can follow through the commit history to see whats been modified.

# Status

I've freshly copied the code and made the needed modifications to build. There's a few kinks to iron out, but I plan on trying to get the Swagger-based package published as `Grpc.Community.SwaggerGen` soon™

After that, I'll see about porting it to OpenApi, and then we'll publish `Grpc.Community.OpenApi` as a separate package. 
