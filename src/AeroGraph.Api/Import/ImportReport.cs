namespace AeroGraph.Api.Import;

/// <summary>Summary of the startup import, returned by GET /stats.</summary>
public record ImportReport(
    int Airports,
    int SkippedAirports,
    int Routes,
    int SkippedRoutes,
    int Connections,
    long LoadMilliseconds);
