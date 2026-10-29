using AeroGraph.Api.Graph;
using AeroGraph.Api.Models;

namespace AeroGraph.Tests;

public class RouteGraphTests
{
    // A small hand-made network (the same one as the BFS demo), plus Nice with no flights at all.
    //
    //   TLS -> CDG, ORY, AMS, LYS      CDG -> TUN, FRA, TLS      ORY -> TUN, TLS
    //   AMS -> FRA     LYS -> MRS     MRS -> TUN     FRA -> TUN     TUN -> ORY
    private static readonly RouteGraph Graph = new(
        [
            Airport("TLS", "Toulouse-Blagnac Airport", "Toulouse"),
            Airport("CDG", "Charles de Gaulle International Airport", "Paris"),
            Airport("ORY", "Paris-Orly Airport", "Paris"),
            Airport("AMS", "Amsterdam Airport Schiphol", "Amsterdam"),
            Airport("LYS", "Lyon Saint-Exupéry Airport", "Lyon"),
            Airport("MRS", "Marseille Provence Airport", "Marseille"),
            Airport("FRA", "Frankfurt am Main Airport", "Frankfurt"),
            Airport("TUN", "Tunis Carthage International Airport", "Tunis"),
            Airport("NCE", "Nice-Côte d'Azur Airport", "Nice"),
        ],
        [
            new("AF", "TLS", "CDG"), new("U2", "TLS", "CDG"), new("AF", "TLS", "ORY"),
            new("KL", "TLS", "AMS"), new("AF", "TLS", "LYS"),
            new("TU", "CDG", "TUN"), new("LH", "CDG", "FRA"), new("AF", "CDG", "TLS"),
            new("TU", "ORY", "TUN"), new("AF", "ORY", "TLS"),
            new("KL", "AMS", "FRA"), new("AF", "LYS", "MRS"), new("TU", "MRS", "TUN"),
            new("LH", "FRA", "TUN"), new("TU", "TUN", "ORY"),
        ]);

    private static Airport Airport(string iata, string name, string city) => new(iata, name, city, "", 0, 0);

    [Fact]
    public void Constructor_CountsEachConnectionOnce()
    {
        // 15 routes, but AF and U2 both fly TLS -> CDG, so 14 distinct connections.
        Assert.Equal(9, Graph.AirportCount);
        Assert.Equal(14, Graph.ConnectionCount);
    }

    [Fact]
    public void GetAirport_IgnoresCase()
    {
        Assert.Equal("Toulouse", Graph.GetAirport("tls")?.City);
        Assert.Null(Graph.GetAirport("XXX"));
    }

    [Fact]
    public void GetDestinations_ReturnsDirectFlightsOnly()
    {
        Assert.Equal(["AMS", "CDG", "LYS", "ORY"], Graph.GetDestinations("TLS").Order());
        Assert.Empty(Graph.GetDestinations("NCE"));
        Assert.Empty(Graph.GetDestinations("XXX"));
    }

    [Fact]
    public void Search_MatchesCityAndPutsTheExactCodeFirst()
    {
        Assert.Equal(["CDG", "ORY"], Graph.Search("paris").Select(airport => airport.Iata).Order());

        // "nce" is Nice's code and also appears in "Marseille Provence Airport".
        // NCE comes first even though "Marseille" sorts before "Nice".
        Assert.Equal(["NCE", "MRS"], Graph.Search("nce").Select(airport => airport.Iata));
        Assert.Single(Graph.Search("a", limit: 1));
    }

    // ---- FindPath: these fail until the BFS is written ----

    [Fact]
    public void FindPath_DirectFlight()
    {
        Assert.Equal(["TLS", "CDG"], Graph.FindPath("TLS", "CDG"));
    }

    [Fact]
    public void FindPath_ReturnsTheFewestStops()
    {
        // TLS -> LYS -> MRS -> TUN also works, but it has 2 stops. The answer must have 1 (via CDG or ORY).
        var path = Graph.FindPath("TLS", "TUN");

        Assert.NotNull(path);
        Assert.Equal(3, path.Count);
        Assert.Equal("TLS", path[0]);
        Assert.Contains(path[1], new[] { "CDG", "ORY" });
        Assert.Equal("TUN", path[2]);
    }

    [Fact]
    public void FindPath_FollowsOneWayRoutes()
    {
        // TLS -> AMS is direct, but there is no AMS -> TLS flight, so the way back is longer.
        Assert.Equal(["TLS", "AMS"], Graph.FindPath("TLS", "AMS"));
        Assert.Equal(["AMS", "FRA", "TUN", "ORY", "TLS"], Graph.FindPath("AMS", "TLS"));
    }

    [Fact]
    public void FindPath_SameAirport_ReturnsJustThatAirport()
    {
        Assert.Equal(["TLS"], Graph.FindPath("TLS", "TLS"));
    }

    [Fact]
    public void FindPath_NoRoute_ReturnsNull()
    {
        // Nobody flies to Nice.
        Assert.Null(Graph.FindPath("TLS", "NCE"));
    }

    [Fact]
    public void FindPath_UnknownAirport_ReturnsNull()
    {
        Assert.Null(Graph.FindPath("TLS", "XXX"));
        Assert.Null(Graph.FindPath("XXX", "TLS"));
    }
}
