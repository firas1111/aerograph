namespace AeroGraph.Api.Models;

/// <summary>An airport, identified by its 3-letter IATA code (e.g. TLS for Toulouse).</summary>
public record Airport(
    string Iata,
    string Name,
    string City,
    string Country,
    double Latitude,
    double Longitude);
