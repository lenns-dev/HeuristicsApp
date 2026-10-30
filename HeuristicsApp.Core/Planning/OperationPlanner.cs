using System.Diagnostics;

namespace HeuristicsApp.Core;

public sealed class OperationPlanner(
    IReadOnlyDictionary<string, IBatchingStrategy> batching,
    IReadOnlyDictionary<string, IRoutingStrategy> routing)
{
    public OperationPlan Build(
        WarehouseProblem problem,
        string batchingKey,
        string routingKey,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var router  = routing[routingKey];
        var batches = batching[batchingKey].CreateBatches(problem, router, ct);
        var routes  = batches.Select(b => router.Route(b, problem.Layout, ct)).ToList();
        sw.Stop();

        return OperationPlan.From(routes, problem.Pickers, problem.Timing, computeTime: sw.Elapsed);
    }
}