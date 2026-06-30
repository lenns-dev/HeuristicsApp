namespace HeuristicsApp.Core;

/// <summary>
/// Recorrido resuelto para un lote: la secuencia ordenada de paradas
/// (incluido depósito de inicio y fin) y su distancia total.
/// </summary>
public sealed record PickRoute(Batch Batch, IReadOnlyList<Coordinate> Stops, double Distance);