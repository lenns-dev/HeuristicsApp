using HeuristicsApp.ApiService.Mapping;
using HeuristicsApp.ApiService.Simulation;
using HeuristicsApp.ApiService.Warehouses;
using HeuristicsApp.Core;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IBatchingStrategy, RandomBatching>();
builder.Services.AddSingleton<IBatchingStrategy, BasicConstructiveBatching>();
builder.Services.AddSingleton<IBatchingStrategy, CWSavingConstructiveBatching>();

builder.Services.AddSingleton<IRoutingStrategy, SShapeRouting>();
builder.Services.AddSingleton<IRoutingStrategy, CombinedRouting>();
builder.Services.AddSingleton<IRoutingStrategy, LargestGapRouting>();

builder.Services.AddSingleton<IReadOnlyDictionary<string, IBatchingStrategy>>(sp =>
    sp.GetServices<IBatchingStrategy>().ToDictionary(s => s.Key));
builder.Services.AddSingleton<IReadOnlyDictionary<string, IRoutingStrategy>>(sp =>
    sp.GetServices<IRoutingStrategy>().ToDictionary(s => s.Key));

builder.Services.AddSingleton<OperationPlanner>();
builder.Services.AddSingleton<RouteSimulator>();
builder.Services.Configure<WarehouseDatasetOptions>(builder.Configuration.GetSection(WarehouseDatasetOptions.Section));
builder.Services.AddSingleton<IWarehouseProvider, FileWarehouseProvider>();
builder.Services.AddSingleton<DatasetSyncService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/strategies", (
    IReadOnlyDictionary<string, IBatchingStrategy> batching,
    IReadOnlyDictionary<string, IRoutingStrategy> routing) =>
    new StrategyCatalog(
        batching.Values.Select(s => new StrategyInfo(s.Key, s.DisplayName)).ToList(),
        routing.Values.Select(s => new StrategyInfo(s.Key, s.DisplayName)).ToList()))
.WithName("GetStrategies");

app.MapGet("/warehouse-folders", (IWarehouseProvider warehouses) => warehouses.Folders())
.WithName("GetWarehouseFolders");

app.MapGet("/warehouses", Results<Ok<IReadOnlyList<WarehouseSummary>>, ProblemHttpResult> (
    string? folder,
    IWarehouseProvider warehouses) =>
    warehouses.List(folder) is { } list
        ? TypedResults.Ok(list)
        : TypedResults.Problem($"Warehouse folder '{folder}' not found.", statusCode: StatusCodes.Status404NotFound))
.WithName("GetWarehouses");

// Sincronización de la carpeta de instancias con el repositorio remoto (ver WarehouseDatasetOptions).
app.MapGet("/dataset/status", (DatasetSyncService sync) => sync.Status)
.WithName("GetDatasetStatus");

app.MapPost("/dataset/sync", Results<Accepted<DatasetStatus>, ProblemHttpResult> (bool? dryRun, DatasetSyncService sync) =>
{
    if (!sync.Status.Configured)
        return TypedResults.Problem("The warehouse dataset repository is not configured (WarehouseDataset:Owner/Repo).",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    if (!sync.TryStart(dryRun ?? false))
        return TypedResults.Problem("A dataset synchronization is already running.", statusCode: StatusCodes.Status409Conflict);

    return TypedResults.Accepted("/dataset/status", sync.Status);
})
.WithName("SyncDataset");

app.MapPost("/plan", Results<Ok<SimulationResult>, ProblemHttpResult> (
    PlanRequest req,
    OperationPlanner planner,
    IWarehouseProvider warehouses,
    RouteSimulator simulator,
    IReadOnlyDictionary<string, IBatchingStrategy> batching,
    IReadOnlyDictionary<string, IRoutingStrategy> routing,
    CancellationToken ct) =>
{
    if (!batching.ContainsKey(req.BatchingKey))
        return TypedResults.Problem($"Unknown batching strategy '{req.BatchingKey}'.", statusCode: StatusCodes.Status400BadRequest);
    if (!routing.ContainsKey(req.RoutingKey))
        return TypedResults.Problem($"Unknown routing strategy '{req.RoutingKey}'.", statusCode: StatusCodes.Status400BadRequest);

    WarehouseProblem? loaded;
    try
    {
        loaded = warehouses.Find(req.WarehouseId);
    }
    catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or IOException)
    {
        return TypedResults.Problem($"Warehouse '{req.WarehouseId}' could not be loaded: {ex.Message}",
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }
    if (loaded is null)
        return TypedResults.Problem($"Warehouse '{req.WarehouseId}' not found.", statusCode: StatusCodes.Status404NotFound);

    if (req.Options?.Pickers is < 1)
        return TypedResults.Problem("Pickers must be at least 1.", statusCode: StatusCodes.Status400BadRequest);

    // Prioridad del JSON: la capacidad viene de la instancia (workerCapacity).
    // Options es opcional y solo cubre lo que la instancia deja sin definir (pickers: null).
    var problem = loaded with { Pickers = req.Options?.Pickers ?? loaded.Pickers };

    var plan = planner.Build(problem, req.BatchingKey, req.RoutingKey, ct);
    return TypedResults.Ok(plan.ToSimulationResult(problem.Layout, simulator));
})
.WithName("CreatePlan");

app.MapDefaultEndpoints();

app.Run();
