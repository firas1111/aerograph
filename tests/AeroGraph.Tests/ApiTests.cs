using System.Net;
using System.Net.Http.Json;
using AeroGraph.Api.Endpoints;
using AeroGraph.Api.Import;
using AeroGraph.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AeroGraph.Tests;

/// <summary>
/// Integration tests: WebApplicationFactory starts the real API in memory (Program.cs, real data files,
/// dependency injection, routing, JSON) and the tests call it over HTTP, like a client would.
/// IClassFixture shares one running API between all the tests of this class, so the data loads once.
/// </summary>
public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Stats_ReportsTheImport()
    {
        var stats = await _client.GetFromJsonAsync<ImportReport>("/stats");

        Assert.NotNull(stats);
        Assert.Equal(6072, stats.Airports);
        Assert.Equal(1626, stats.SkippedAirports);
        Assert.Equal(37042, stats.Connections);
    }

    [Fact]
    public async Task GetAirport_ReturnsTheAirport_WhateverTheCase()
    {
        var airport = await _client.GetFromJsonAsync<Airport>("/airports/tls");

        Assert.NotNull(airport);
        Assert.Equal("TLS", airport.Iata);
        Assert.Equal("Toulouse", airport.City);
    }

    [Fact]
    public async Task GetAirport_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/airports/ZZZ");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Airport not found", problem?.Title);
    }

    [Fact]
    public async Task GetAirport_BadlyFormedCode_Returns400()
    {
        var response = await _client.GetAsync("/airports/TOULOUSE");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.True(problem?.Errors.ContainsKey("code"));
    }

    [Fact]
    public async Task SearchAirports_FindsTheParisAirports()
    {
        var airports = await _client.GetFromJsonAsync<List<Airport>>("/airports?search=paris");

        Assert.NotNull(airports);
        Assert.Contains(airports, airport => airport.Iata == "CDG");
        Assert.Contains(airports, airport => airport.Iata == "ORY");
    }

    [Fact]
    public async Task SearchAirports_TooShort_Returns400()
    {
        var response = await _client.GetAsync("/airports?search=p");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDestinations_ListsTheDirectFlights()
    {
        var result = await _client.GetFromJsonAsync<DestinationsResponse>("/airports/TLS/destinations");

        Assert.NotNull(result);
        Assert.Equal("TLS", result.From.Iata);
        Assert.Equal(result.Count, result.Destinations.Count);
        Assert.Contains(result.Destinations, airport => airport.Iata == "TUN");
    }

    [Fact]
    public async Task FindTrip_DirectFlight()
    {
        var trip = await _client.GetFromJsonAsync<TripResponse>("/routes?from=TLS&to=TUN");

        Assert.NotNull(trip);
        Assert.Equal(1, trip.Flights);
        Assert.Equal(0, trip.Stops);
    }

    [Fact]
    public async Task FindTrip_WithStops()
    {
        var trip = await _client.GetFromJsonAsync<TripResponse>("/routes?from=TLS&to=NOU");

        // Toulouse to Nouméa: no shorter trip than 2 stops exists in the data.
        Assert.NotNull(trip);
        Assert.Equal(2, trip.Stops);
        Assert.Equal(4, trip.Path.Count);
        Assert.Equal("TLS", trip.Path[0].Iata);
        Assert.Equal("NOU", trip.Path[^1].Iata);
    }

    [Fact]
    public async Task FindTrip_StopLimitTooLow_Returns404WithTheShortestStopCount()
    {
        var response = await _client.GetAsync("/routes?from=TLS&to=NOU&maxStops=1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("The shortest has 2", problem?.Detail);
    }

    [Fact]
    public async Task FindTrip_MissingDestination_Returns400()
    {
        var response = await _client.GetAsync("/routes?from=TLS");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownUrl_Returns404AsProblemDetails()
    {
        var response = await _client.GetAsync("/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
