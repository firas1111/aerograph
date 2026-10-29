using System.Globalization;
using AeroGraph.Api.Models;
using CsvHelper;
using CsvHelper.Configuration;

namespace AeroGraph.Api.Import;

/// <summary>
/// Reads the OpenFlights airports.dat and routes.dat files: CSV with no header row, \N for missing values.
/// A row we can't use is skipped and counted, so one bad line never stops the whole import.
/// </summary>
public static class OpenFlightsLoader
{
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = false,
        // A row with too few columns returns null fields instead of throwing; the checks below then skip it.
        MissingFieldFound = null,
    };

    public static ImportResult<Airport> LoadAirports(string path)
    {
        using var reader = File.OpenText(path);
        return LoadAirports(reader);
    }

    public static ImportResult<Airport> LoadAirports(TextReader reader)
    {
        var airports = new List<Airport>();
        var seenCodes = new HashSet<string>();
        var skipped = 0;

        using var csv = new CsvReader(reader, Config);
        while (csv.Read())
        {
            var airport = ParseAirport(csv);

            // The IATA code is our key, so a duplicate code would be ambiguous: keep the first one.
            if (airport is null || !seenCodes.Add(airport.Iata))
            {
                skipped++;
                continue;
            }

            airports.Add(airport);
        }

        return new ImportResult<Airport>(airports, skipped);
    }

    public static ImportResult<FlightRoute> LoadRoutes(string path, IReadOnlySet<string> knownAirports)
    {
        using var reader = File.OpenText(path);
        return LoadRoutes(reader, knownAirports);
    }

    // Columns: 0 airline, 1 airline id, 2 source airport, 3 source id, 4 destination airport, 5 destination id, ...
    public static ImportResult<FlightRoute> LoadRoutes(TextReader reader, IReadOnlySet<string> knownAirports)
    {
        var routes = new List<FlightRoute>();
        var skipped = 0;

        using var csv = new CsvReader(reader, Config);
        while (csv.Read())
        {
            var airline = Clean(csv.GetField(0));
            var from = Clean(csv.GetField(2));
            var to = Clean(csv.GetField(4));

            // A route can point to an airport we skipped (no IATA code), so keep only routes between known airports.
            if (airline is null || from is null || to is null
                || !knownAirports.Contains(from) || !knownAirports.Contains(to))
            {
                skipped++;
                continue;
            }

            routes.Add(new FlightRoute(airline, from, to));
        }

        return new ImportResult<FlightRoute>(routes, skipped);
    }

    // Columns: 0 id, 1 name, 2 city, 3 country, 4 IATA, 5 ICAO, 6 latitude, 7 longitude, ...
    private static Airport? ParseAirport(CsvReader csv)
    {
        var iata = Clean(csv.GetField(4));
        var name = Clean(csv.GetField(1));
        if (iata is null || iata.Length != 3 || name is null)
            return null;

        if (!TryParseNumber(csv.GetField(6), out var latitude) || !TryParseNumber(csv.GetField(7), out var longitude))
            return null;

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return null;

        return new Airport(
            iata,
            name,
            Clean(csv.GetField(2)) ?? "",
            Clean(csv.GetField(3)) ?? "",
            latitude,
            longitude);
    }

    // The file always writes 43.629101 with a dot. InvariantCulture keeps it working on a French machine,
    // where the default culture expects a comma (43,629101).
    private static bool TryParseNumber(string? value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    // OpenFlights writes a missing value as \N.
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == @"\N" ? null : value.Trim();
}
