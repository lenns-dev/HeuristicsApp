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

builder.Services.AddSingleton<IBatchingStrategy, SeedBatching>();
builder.Services.AddSingleton<IBatchingStrategy, SavingsBatching>();
builder.Services.AddSingleton<IBatchingStrategy, RandomBatching>();
builder.Services.AddSingleton<IBatchingStrategy, BasicConstructiveBatching>();

builder.Services.AddSingleton<IRoutingStrategy, SShapeRouting>();
builder.Services.AddSingleton<IRoutingStrategy, LargestGapRouting>();

builder.Services.AddSingleton<IReadOnlyDictionary<string, IBatchingStrategy>>(sp =>
    sp.GetServices<IBatchingStrategy>().ToDictionary(s => s.Key));
builder.Services.AddSingleton<IReadOnlyDictionary<string, IRoutingStrategy>>(sp =>
    sp.GetServices<IRoutingStrategy>().ToDictionary(s => s.Key));

builder.Services.AddSingleton<OperationPlanner>();
builder.Services.AddSingleton<RouteSimulator>();
builder.Services.AddSingleton<IWarehouseProvider, FileWarehouseProvider>();

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

app.MapGet("/warehouses", (IWarehouseProvider warehouses) =>
    warehouses.List().Select(w => new WarehouseSummary(w.Id, w.Name)).ToList())
.WithName("GetWarehouses");

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

    var loaded = warehouses.Find(req.WarehouseId);
    if (loaded is null)
        return TypedResults.Problem($"Warehouse '{req.WarehouseId}' not found.", statusCode: StatusCodes.Status404NotFound);

    // Prioridad del JSON: la capacidad viene de la instancia (workerCapacity).
    // Options es opcional y solo cubre lo que la instancia deja sin definir (pickers: null).
    var problem = loaded with { Pickers = req.Options?.Pickers ?? loaded.Pickers };

    var plan = planner.Build(problem, req.BatchingKey, req.RoutingKey, ct);
    return TypedResults.Ok(plan.ToSimulationResult(problem.Layout, simulator));
})
.WithName("CreatePlan");

app.MapDefaultEndpoints();

app.Run();
