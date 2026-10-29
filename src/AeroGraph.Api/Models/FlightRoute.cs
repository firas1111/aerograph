namespace AeroGraph.Api.Models;

/// <summary>A one-way flight between two airports (IATA codes), operated by one airline.</summary>
/// <remarks>Not called Route: ASP.NET Core already has a Microsoft.AspNetCore.Routing.Route type.</remarks>
public record FlightRoute(string Airline, string From, string To);
