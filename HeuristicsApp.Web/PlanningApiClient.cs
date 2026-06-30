using System.Net.Http.Json;

namespace HeuristicsApp.Web;

/// <summary>Cliente tipado del ApiService para el catálogo de planes y su ejecución.</summary>
public sealed class PlanningApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<WarehouseSummary>> GetWarehousesAsync(CancellationToken ct = default)
        => await httpClient.GetFromJsonAsync<IReadOnlyList<WarehouseSummary>>("/warehouses", ct) ?? [];

    public async Task<StrategyCatalog?> GetStrategiesAsync(CancellationToken ct = default)
        => await httpClient.GetFromJsonAsync<StrategyCatalog>("/strategies", ct);

    public async Task<SimulationResult?> CreatePlanAsync(PlanRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("/plan", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SimulationResult>(ct);
    }
}