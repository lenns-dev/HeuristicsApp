namespace HeuristicsApp.Core;

/// <summary>Agrupación de pedidos asignada a un único recorrido de picking.</summary>
public sealed record Batch(int Id, IReadOnlyList<Order> Orders)
{
    /// <summary>Todas las líneas a recoger en este lote.</summary>
    public IEnumerable<OrderLine> Lines => Orders.SelectMany(o => o.Lines);
}