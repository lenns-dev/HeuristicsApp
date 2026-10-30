namespace HeuristicsApp.Core;

/// <summary>Línea de pedido: qué SKU recoger, cuántas unidades y su peso unitario.</summary>
public sealed record OrderLine(string Sku, int Quantity, double UnitWeight);

/// <summary>Pedido de cliente compuesto por una o más líneas.</summary>
public sealed record Order(Guid Id, IReadOnlyList<OrderLine> Lines);

/// <summary>Capacidad operativa de cada recolector (picker).</summary>
public sealed record PickerCapacity(int MaxItems, double MaxWeight);

/// <summary>
/// Instancia completa del problema: el layout, los pedidos a servir
/// y las restricciones operativas. Es la única entrada de las heurísticas.
/// </summary>
public sealed record WarehouseProblem(
    Guid WarehouseId,
    WarehouseLayout Layout,
    IReadOnlyList<Order> Orders,
    PickerCapacity Capacity,
    int Pickers,
    PickerTiming Timing);