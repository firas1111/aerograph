using System.Diagnostics;
using AeroGraph.Api.Endpoints;
using AeroGraph.Api.Graph;
using AeroGraph.Api.Import;

var builder = WebApplication.CreateBuilder(args);

// Load the OpenFlights files once at startup. The .csproj copies the data folder next to the compiled app.
var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
var stopwatch = Stopwatch.StartNew();

var airports = OpenFlightsLoader.LoadAirports(Path.Combine(dataDirectory, "airports.dat"));
var knownCodes = airports.Items.Select(airport => airport.Iata).ToHashSet();
var routes = OpenFlightsLoader.LoadRoutes(Path.Combine(dataDirectory, "routes.dat"), knownCodes);
var graph = new RouteGraph(airports.Items, routes.Items);

var report = new ImportReport(
    airports.Items.Count, airports.Skipped, routes.Items.Count, routes.Skipped,
    graph.ConnectionCount, stopwatch.ElapsedMilliseconds);

// Singletons: one graph for the whole app, built once above and never modified, shared by every request.
builder.Services.AddSingleton(graph);
builder.Services.AddSingleton(report);

builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.Logger.LogInformation(
    "Loaded {Airports} airports ({SkippedAirports} skipped), {Routes} routes ({SkippedRoutes} skipped), " +
    "{Connections} connections in {Elapsed} ms",
    report.Airports, report.SkippedAirports, report.Routes, report.SkippedRoutes, report.Connections,
    report.LoadMilliseconds);

// Errors come back as JSON "problem details" instead of an empty body or an HTML page:
// unhandled exceptions become a 500, and unknown URLs a 404.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/stats", (ImportReport importReport) => importReport);
app.MapAirportEndpoints();
app.MapRouteEndpoints();

app.Run();
