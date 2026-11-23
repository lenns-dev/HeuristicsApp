using System.Net.Http.Json;

namespace HeuristicsApp.Web;

/// <summary>Cliente tipado del ApiService para el catálogo de planes y su ejecución.</summary>
public sealed class PlanningApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<WarehouseFolder>> GetWarehouseFoldersAsync(CancellationToken ct = default)
        => await httpClient.GetFromJsonAsync<IReadOnlyList<WarehouseFolder>>("/warehouse-folders", ct) ?? [];

    public async Task<IReadOnlyList<WarehouseSummary>> GetWarehousesAsync(string folder, CancellationToken ct = default)
        => await httpClient.GetFromJsonAsync<IReadOnlyList<WarehouseSummary>>(
            $"/warehouses?folder={Uri.EscapeDataString(folder)}", ct) ?? [];

    public async Task<StrategyCatalog?> GetStrategiesAsync(CancellationToken ct = default)
        => await httpClient.GetFromJsonAsync<StrategyCatalog>("/strategies", ct);

    public async Task<DatasetStatus?> GetDatasetStatusAsync(CancellationToken ct = default)
        => await httpClient.GetFromJsonAsync<DatasetStatus>("/dataset/status", ct);

    /// <summary>Lanza la sincronización de instancias (o solo la comprobación); devuelve el error de la API si la rechaza.</summary>
    public async Task<(DatasetStatus? Status, string? Error)> StartDatasetSyncAsync(bool dryRun, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync($"/dataset/sync?dryRun={(dryRun ? "true" : "false")}", null, ct);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<DatasetStatus>(ct), null);

        var problem = await response.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>(ct);
        return (null, problem?.Detail ?? response.ReasonPhrase);
    }

    public async Task<SimulationResult?> CreatePlanAsync(PlanRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("/plan", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SimulationResult>(ct);
    }
}