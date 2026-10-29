using AeroGraph.Api.Graph;
using AeroGraph.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AeroGraph.Api.Endpoints;

public static class RouteEndpoints
{
    public static void MapRouteEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/routes", FindTrip);

    // GET /routes?from=TLS&to=TUN&maxStops=1   (maxStops is optional)
    private static Results<Ok<TripResponse>, ProblemHttpResult, ValidationProblem> FindTrip(
        RouteGraph graph, string? from, string? to, int? maxStops)
    {
        if (!ApiErrors.IsIataCode(from))
            return ApiErrors.InvalidCode("from");

        if (!ApiErrors.IsIataCode(to))
            return ApiErrors.InvalidCode("to");

        if (maxStops < 0)
            return ApiErrors.Invalid("maxStops", "Must be 0 or more.");

        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();

        if (graph.GetAirport(from) is null)
            return ApiErrors.UnknownAirport(from);

        if (graph.GetAirport(to) is null)
            return ApiErrors.UnknownAirport(to);

        var path = graph.FindPath(from, to);
        if (path is null)
            return ApiErrors.NotFound("No trip found", $"No trip from {from} to {to} in the data.");

        // A trip through N airports takes N - 1 flights and makes N - 2 stops.
        // FindPath already returns the fewest stops, so if even that is too many, no trip fits the limit.
        var flights = path.Count - 1;
        var stops = Math.Max(0, path.Count - 2);
        if (maxStops is not null && stops > maxStops)
            return ApiErrors.NotFound(
                "No trip found", $"No trip from {from} to {to} with at most {maxStops} stop(s). The shortest has {stops}.");

        var airports = path.Select(code => graph.GetAirport(code)!).ToList();
        return TypedResults.Ok(new TripResponse(flights, stops, airports));
    }
}

public record TripResponse(int Flights, int Stops, IReadOnlyList<Airport> Path);
