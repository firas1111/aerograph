using AeroGraph.Api.Models;

namespace AeroGraph.Api.Graph;

/// <summary>
/// The flight network as a graph: airports are the nodes, routes are one-way edges.
/// Built once at startup and only read afterwards, so it is safe to share between requests.
/// </summary>
public class RouteGraph
{
    // Nodes: IATA code -> airport. Case-insensitive, so "tls" finds TLS.
    private readonly Dictionary<string, Airport> _airports = new(StringComparer.OrdinalIgnoreCase);

    // Edges (adjacency list): IATA code -> codes of the airports with a direct flight from it.
    // A HashSet because several airlines can fly the same route, and we only need the connection once.
    private readonly Dictionary<string, HashSet<string>> _destinations = new(StringComparer.OrdinalIgnoreCase);

    public RouteGraph(IEnumerable<Airport> airports, IEnumerable<FlightRoute> routes)
    {
        foreach (var airport in airports)
        {
            _airports[airport.Iata] = airport;
            _destinations[airport.Iata] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var route in routes)
        {
            // The loader already drops routes to unknown airports, but the graph checks it again rather than rely on it.
            if (_destinations.TryGetValue(route.From, out var destinations) && _airports.ContainsKey(route.To))
                destinations.Add(route.To);
        }

        ConnectionCount = _destinations.Values.Sum(destinations => destinations.Count);
    }

    public int AirportCount => _airports.Count;

    /// <summary>Distinct one-way connections: TLS to CDG counts once, however many airlines fly it.</summary>
    public int ConnectionCount { get; }

    public Airport? GetAirport(string code) => _airports.GetValueOrDefault(code);

    public IReadOnlyCollection<string> GetDestinations(string code) =>
        _destinations.TryGetValue(code, out var destinations) ? destinations : [];

    /// <summary>Airports whose code, name or city contains the text. An exact code match comes first.</summary>
    public IReadOnlyList<Airport> Search(string text, int limit = 10) =>
        _airports.Values
            .Where(airport => airport.Iata.Contains(text, StringComparison.OrdinalIgnoreCase)
                || airport.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || airport.City.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(airport => airport.Iata.Equals(text, StringComparison.OrdinalIgnoreCase))
            .ThenBy(airport => airport.Name)
            .Take(limit)
            .ToList();

    /// <summary>
    /// The trip with the fewest stops, as IATA codes including both ends: TLS, ORY, TUN.
    /// Returns [from] when both airports are the same, and null when an airport is unknown or no trip exists.
    /// </summary>
    public IReadOnlyList<string>? FindPath(string from, string to)
    {
        // Unknown airport: no trip. Using the stored airports also turns "tls" into "TLS".
        if (!_airports.TryGetValue(from, out var start) || !_airports.TryGetValue(to, out var end))
            return null;

        if (start.Iata == end.Iata)
            return [start.Iata];

        // Airports already reached. Without it we would loop forever: TLS -> CDG -> TLS -> CDG ...
        var visited = new HashSet<string> { start.Iata };

        // For each airport reached, the airport we flew from to get there. Used at the end to rebuild the trip.
        var cameFrom = new Dictionary<string, string>();

        // The current wave: every airport reached with the same number of flights. It starts with just the departure.
        var wave = new List<string> { start.Iata };

        while (wave.Count > 0)
        {
            var nextWave = new List<string>();

            foreach (var airport in wave)
            {
                foreach (var destination in _destinations[airport])
                {
                    // Already reached in this wave or an earlier one, so never with fewer flights: skip it.
                    if (!visited.Add(destination))
                        continue;

                    cameFrom[destination] = airport;

                    if (destination == end.Iata)
                        return BuildPath(cameFrom, destination);

                    nextWave.Add(destination);
                }
            }

            wave = nextWave;
        }

        // A wave found no new airport: everything reachable has been explored and the destination is not in it.
        return null;
    }

    // Walks back from the destination to the departure (the only airport with no cameFrom entry), then reverses.
    private static List<string> BuildPath(Dictionary<string, string> cameFrom, string destination)
    {
        var path = new List<string> { destination };
        var current = destination;

        while (cameFrom.TryGetValue(current, out var previous))
        {
            path.Add(previous);
            current = previous;
        }

        path.Reverse();
        return path;
    }
}
