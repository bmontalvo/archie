namespace Archie.Core.Planning;

/// <summary>
/// Decides whether a question can be answered in a single retrieval pass
/// or needs decomposition into sub-queries.
/// </summary>
public interface IQueryPlanner
{
    Task<QueryPlan> PlanQueryAsync(string question, CancellationToken cancellationToken = default);
}
