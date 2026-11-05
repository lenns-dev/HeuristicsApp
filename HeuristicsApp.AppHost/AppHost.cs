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

// Exportar a docker-compose (Aspire 13). Con el CLI de Aspire (dotnet tool install -g Aspire.Cli):
//
//   aspire publish                -> solo docker-compose.yaml + .env (sin imágenes)
//   aspire do prepare-compose     -> además construye las imágenes y rellena .env.<entorno>
//   (por defecto escriben en ./aspire-output)
//
// Sin el CLI, los mismos pasos lanzando el AppHost directamente:
//
//   dotnet run --project HeuristicsApp.AppHost -- --operation publish --step publish --output-path ./artifacts
//   dotnet run --project HeuristicsApp.AppHost -- --operation publish --step prepare-compose --output-path ./artifacts
//
// Después (prepare-compose deja los valores en .env.<entorno>, p.ej. .env.Development):
//
//   docker compose -f ./artifacts/docker-compose.yaml --env-file ./artifacts/.env.Development up -d
//
// Web en http://localhost:8081 y dashboard en http://localhost:8080 (este puerto debe estar libre).
// El antiguo "--publisher docker-compose" ya no existe: ejecuta todos los pasos del pipeline y falla.

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

        // prepare-compose construye la imagen como "api:latest" pero escribe en el .env una etiqueta
        // aspire-deploy-<fecha> que no aplica: se fija el nombre real para que "docker compose up" la encuentre.
        service.Image = "api:latest";
    });

// Token del repositorio de instancias (WarehouseDataset:Token) para el contenedor: queda como
// DATASET_TOKEN en el .env generado. Solo al publicar, para no pisar los user-secrets en local.
if (builder.ExecutionContext.IsPublishMode)
{
    var datasetToken = builder.AddParameter("dataset-token", "", secret: true);
    api.WithEnvironment("WarehouseDataset__Token", datasetToken);
}

var web = builder.AddProject<Projects.HeuristicsApp_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WaitFor(api)
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Name = "web";
        service.Image = "web:latest"; // ver nota en "api"

        // Puerto fijo en el host (sin él Docker asigna uno aleatorio en cada arranque).
        service.Ports.Clear();
        service.Ports.Add("8081:${WEB_PORT}");
    });

#endregion

builder.Build().Run();
