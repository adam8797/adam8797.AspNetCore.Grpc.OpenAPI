# HelloTranscoding

The smallest possible app that uses **Community.Grpc.SwaggerGen**: one gRPC
service whose `google.api.http` annotation turns it into a REST endpoint, with a
Swagger UI generated from the `.proto`.

## Run it

```bash
# From the repo root:
dotnet build
dotnet run --project examples/HelloTranscoding
```

Then navigate to [http://localhost:65414/swagger](http://localhost:65414/swagger) to see the Swagger UI.

## Try it

```bash
curl http://localhost:65414/v1/greeter/world
# {"message":"Hello world"}
```

That REST route doesn't exist in any controller — it comes from the`option (google.api.http) = { get: "/v1/greeter/{name}" }` in [`Protos/greet.proto`](Protos/greet.proto).

## The whole wiring

`Program.cs` is essentially three lines:

```csharp
builder.Services.AddGrpcSwagger();   // gRPC + JSON transcoding + Swagger integration
builder.Services.AddSwaggerGen();    // normal Swashbuckle
...
app.UseSwagger();
app.UseSwaggerUI();
app.MapGrpcService<GreeterService>();
```
