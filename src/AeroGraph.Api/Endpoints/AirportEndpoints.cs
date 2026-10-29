using AeroGraph.Api.Graph;
using AeroGraph.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AeroGraph.Api.Endpoints;

public static class AirportEndpoints
{
    public static void MapAirportEndpoints(this IEndpointRouteBuilder app)
    {
        var airports = app.MapGroup("/airports");

        airports.MapGet("/", SearchAirports);
        airports.MapGet("/{code}", GetAirport);
        airports.MapGet("/{code}/destinations", GetDestinations);
    }

    // GET /airports?search=paris&limit=5
    // The graph comes from dependency injection (the singleton); search and limit come from the query string.
    private static Results<Ok<IReadOnlyList<Airport>>, ValidationProblem> SearchAirports(
        RouteGraph graph, string? search, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
            return ApiErrors.Invalid("search", "Type at least 2 characters.");

        if (limit is < 1 or > 50)
            return ApiErrors.Invalid("limit", "Must be between 1 and 50.");

        return TypedResults.Ok(graph.Search(search.Trim(), limit));
    }

    // GET /airports/TLS
    private static Results<Ok<Airport>, ProblemHttpResult, ValidationProblem> GetAirport(string code, RouteGraph graph)
    {
        if (!ApiErrors.IsIataCode(code))
            return ApiErrors.InvalidCode("code");

        var airport = graph.GetAirport(code);
        if (airport is null)
            return ApiErrors.UnknownAirport(code);

        return TypedResults.Ok(airport);
    }

    // GET /airports/TLS/destinations
    private static Results<Ok<DestinationsResponse>, ProblemHttpResult, ValidationProblem> GetDestinations(
        string code, RouteGraph graph)
    {
        if (!ApiErrors.IsIataCode(code))
            return ApiErrors.InvalidCode("code");

        var airport = graph.GetAirport(code);
        if (airport is null)
            return ApiErrors.UnknownAirport(code);

        // The graph only links known airports, so GetAirport never returns null here (hence the !).
        var destinations = graph.GetDestinations(airport.Iata)
            .Select(destination => graph.GetAirport(destination)!)
            .OrderBy(destination => destination.Iata)
            .ToList();

        return TypedResults.Ok(new DestinationsResponse(airport, destinations.Count, destinations));
    }
}

public record DestinationsResponse(Airport From, int Count, IReadOnlyList<Airport> Destinations);
