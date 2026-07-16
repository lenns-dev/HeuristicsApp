using System.IO;
using System.Net.Sockets;

var builder = DistributedApplication.CreateBuilder(args);

#region Windows

//var apiService = builder.AddProject<Projects.HeuristicsApp_ApiService>("apiservice")
//    .WithHttpHealthCheck("/health");

//builder.AddProject<Projects.HeuristicsApp_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithHttpHealthCheck("/health")
//    .WithReference(apiService)
//    .WaitFor(apiService);

#endregion

#region Docker

// dotnet run --project HeuristicsApp.AppHost -- --publisher docker-compose --output-path ./artifacts

var compose = builder.AddDockerComposeEnvironment("compose")
    .WithDashboard(dashboard =>
    {
        dashboard.WithHostPort(8080)
                 .WithForwardedHeaders(enabled: true);
    });

var api = builder.AddProject<Projects.HeuristicsApp_ApiService>("api")
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Name = "api";
    });

var web = builder.AddProject<Projects.HeuristicsApp_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Name = "web";
    });

#endregion

builder.Build().Run();
