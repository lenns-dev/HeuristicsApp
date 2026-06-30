using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HeuristicsApp.Core.Instances;

/// <summary>Resumen de una instancia disponible, para poblar la UI.</summary>
public sealed record WarehouseInstanceInfo(Guid Id, string Name);

/// <summary>
/// Parsea los ficheros de instancia (formato benchmark Albareda) a
/// <see cref="WarehouseProblem"/> de dominio. Agnóstico del origen (stream/string).
/// </summary>
public static class WarehouseInstanceLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static WarehouseProblem Load(Stream json, string sourceName)
        => ToProblem(Deserialize(JsonSerializer.Deserialize<InstanceFileDto>(json, Options)), sourceName);

    public static WarehouseProblem Load(string json, string sourceName)
        => ToProblem(Deserialize(JsonSerializer.Deserialize<InstanceFileDto>(json, Options)), sourceName);

    /// <summary>Guid estable derivado del nombre de la instancia (id reproducible).</summary>
    public static Guid DeriveId(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));

    private static InstanceFileDto Deserialize(InstanceFileDto? dto)
        => dto ?? throw new InvalidDataException("Empty or invalid instance JSON.");

    private static WarehouseProblem ToProblem(InstanceFileDto dto, string sourceName)
    {
        var set = dto.InstanceSet?.FirstOrDefault() ?? throw new InvalidDataException("Missing InstanceSet.");
        var layoutDto = set.Layout?.FirstOrDefault() ?? throw new InvalidDataException("Missing Layout.");
        var ordersDto = set.Orders?.FirstOrDefault() ?? throw new InvalidDataException("Missing Orders.");

        var layout = BuildLayout(layoutDto, ordersDto);
        var orders = BuildOrders(ordersDto);

        return new WarehouseProblem(
            WarehouseId: DeriveId(sourceName),
            Layout: layout,
            Orders: orders,
            Capacity: new PickerCapacity((int)Math.Round(layoutDto.WorkerCapacity), layoutDto.WorkerCapacity),
            Pickers: 1);
    }

    private static WarehouseLayout BuildLayout(LayoutDto layout, OrdersDto orders)
    {
        // --- SUPUESTOS GEOMÉTRICOS (pendientes de confirmar) ---
        // X del corredor i = i * (shelfWidth + aisleWidth). Coincide con
        //   aisles[].distanceFromOriginRight del fichero (0; 7.16; 14.33; 21.5).
        //   Uso la fórmula (robusta) porque el array de aisles puede no traer
        //   todos los pasillos, pero hay productos con aisle = numberOfAisles-1.
        // Y = heightPosition (0..shelfLength). Depósito en (0,0) si depotPlacement == 0.
        // 'side' (0/1) NO afecta a X aquí: la distancia se mide por el corredor.
        var stride = layout.ShelfWidth + layout.AisleWidth;
        double AisleX(int i) => i * stride;

        var aisles = Enumerable.Range(0, layout.NumberOfAisles)
            .Select(i => new Aisle(i, AisleX(i), FrontY: 0, BackY: layout.ShelfLength))
            .ToList();

        // Catálogo de ubicaciones: un SKU por product.id (ubicación fija).
        var locations = new Dictionary<string, StorageLocation>();
        foreach (var p in (orders.OrderList ?? []).SelectMany(o => o.Products ?? []))
        {
            var sku = p.Id.ToString();
            if (!locations.ContainsKey(sku))
                locations[sku] = new StorageLocation(sku, p.Aisle, new Coordinate(AisleX(p.Aisle), p.HeightPosition));
        }

        return new WarehouseLayout(new Coordinate(0, 0), aisles, locations);
    }

    private static IReadOnlyList<Order> BuildOrders(OrdersDto orders)
        => (orders.OrderList ?? [])
            .Select(o => new Order(
                DeriveOrderId(o.Id),
                (o.Products ?? []).Select(p => new OrderLine(p.Id.ToString(), 1, p.Weight)).ToList()))
            .ToList();

    private static Guid DeriveOrderId(int id)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, id);
        return new Guid(bytes);
    }
}