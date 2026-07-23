using Grpc.Core;
using HelloTranscoding;

var builder = WebApplication.CreateBuilder(args);

// AddGrpcSwagger() wires up gRPC + JSON transcoding and configures SwaggerGen
// to describe the transcoded endpoints.
//
// Then AddSwaggerGen() is the normal Swashbuckle call you'd make in any Web API.
builder.Services.AddGrpcSwagger();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Serve the generated OpenAPI document + interactive UI at /swagger.
app.UseSwagger();
app.UseSwaggerUI();

// Host the gRPC service (also exposes the REST route from the .proto annotation).
app.MapGrpcService<GreeterService>();

app.MapGet("/", () => "Open /swagger to explore the API, or try GET /v1/greeter/World");

app.Run();


public class GreeterService : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        return Task.FromResult(new HelloReply
        {
            Message = $"Hello {request.Name}"
        });
    }
}
