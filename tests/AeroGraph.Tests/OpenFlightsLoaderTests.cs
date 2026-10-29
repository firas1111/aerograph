using System.Globalization;
using AeroGraph.Api.Import;

namespace AeroGraph.Tests;

public class OpenFlightsLoaderTests
{
    // Two real rows from airports.dat. Evenes has a comma inside its quoted name.
    private const string ValidAirports = """
        1273,"Toulouse-Blagnac Airport","Toulouse","France","TLS","LFBO",43.629101,1.36382,499,1,"E","Europe/Paris","airport","OurAirports"
        641,"Harstad/Narvik Airport, Evenes","Harstad/Narvik","Norway","EVE","ENEV",68.491302490234,16.678100585938,84,1,"E","Europe/Oslo","airport","OurAirports"
        """;

    [Fact]
    public void LoadAirports_ReadsValidRows()
    {
        var result = OpenFlightsLoader.LoadAirports(new StringReader(ValidAirports));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(0, result.Skipped);

        var toulouse = result.Items[0];
        Assert.Equal("TLS", toulouse.Iata);
        Assert.Equal("Toulouse-Blagnac Airport", toulouse.Name);
        Assert.Equal("Toulouse", toulouse.City);
        Assert.Equal("France", toulouse.Country);
        Assert.Equal(43.629101, toulouse.Latitude);
        Assert.Equal(1.36382, toulouse.Longitude);
    }

    [Fact]
    public void LoadAirports_KeepsCommasInsideQuotedFields()
    {
        // Splitting the line on ',' would cut this name in two and shift every column after it.
        var result = OpenFlightsLoader.LoadAirports(new StringReader(ValidAirports));

        Assert.Equal("Harstad/Narvik Airport, Evenes", result.Items[1].Name);
        Assert.Equal("EVE", result.Items[1].Iata);
    }

    [Fact]
    public void LoadAirports_SkipsAndCountsUnusableRows()
    {
        var data = """
            1273,"Toulouse-Blagnac Airport","Toulouse","France","TLS","LFBO",43.629101,1.36382,499,1,"E","Europe/Paris","airport","OurAirports"
            5000,"Small Airfield","Nowhere","France",\N,"LFXX",45.0,2.0,100,1,"E","Europe/Paris","airport","OurAirports"
            5001,"Bad Latitude Airport","Nowhere","France","BAD","LFXY",abc,2.0,100,1,"E","Europe/Paris","airport","OurAirports"
            5002,"Off The Map Airport","Nowhere","France","OFF","LFXZ",123.0,2.0,100,1,"E","Europe/Paris","airport","OurAirports"
            5003,"Short Row Airport"
            5004,"Toulouse Again","Toulouse","France","TLS","LFBO",43.6,1.3,499,1,"E","Europe/Paris","airport","OurAirports"
            """;

        var result = OpenFlightsLoader.LoadAirports(new StringReader(data));

        // Kept: Toulouse. Skipped: no IATA code (\N), latitude not a number, latitude above 90,
        // too few columns, and a second TLS.
        Assert.Single(result.Items);
        Assert.Equal("Toulouse-Blagnac Airport", result.Items[0].Name);
        Assert.Equal(5, result.Skipped);
    }

    [Fact]
    public void LoadAirports_ParsesCoordinatesWithAFrenchCulture()
    {
        // In French, "43.629101" is not a valid number (it would be "43,629101").
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            var result = OpenFlightsLoader.LoadAirports(new StringReader(ValidAirports));

            Assert.Equal(43.629101, result.Items[0].Latitude);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void LoadRoutes_KeepsOnlyRoutesBetweenKnownAirports()
    {
        var knownAirports = new HashSet<string> { "TLS", "CDG", "TUN" };
        var data = """
            AF,137,TLS,1273,CDG,1382,,0,320
            TU,5376,CDG,1382,TUN,287,,0,320 321
            XX,999,TLS,1273,ZZZ,9999,,0,320
            \N,999,TLS,1273,TUN,287,,0,320
            AF,137,TLS
            """;

        var result = OpenFlightsLoader.LoadRoutes(new StringReader(data), knownAirports);

        // Skipped: unknown destination ZZZ, missing airline (\N), and a row with too few columns.
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(("AF", "TLS", "CDG"), (result.Items[0].Airline, result.Items[0].From, result.Items[0].To));
        Assert.Equal(("TU", "CDG", "TUN"), (result.Items[1].Airline, result.Items[1].From, result.Items[1].To));
    }
}
