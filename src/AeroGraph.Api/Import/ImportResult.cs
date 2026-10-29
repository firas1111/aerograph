namespace AeroGraph.Api.Import;

/// <summary>What an import produced: the rows we could use, and how many rows we skipped.</summary>
public record ImportResult<T>(IReadOnlyList<T> Items, int Skipped);
