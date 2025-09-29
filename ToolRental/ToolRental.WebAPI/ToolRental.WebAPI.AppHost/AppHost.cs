var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.ToolRental_WebAPI_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.ToolRental_WebAPI_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
