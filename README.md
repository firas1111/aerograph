# AeroGraph

A small ASP.NET Core API that loads public flight data into a graph and answers questions like
*"how do I fly from Toulouse to Nouméa with the fewest stops?"*

It imports the OpenFlights airport and route files, cleans them, builds an in-memory graph
(airports are nodes, one-way routes are edges) and serves search and route-finding endpoints.

**Stack:** C# / .NET 10, ASP.NET Core minimal APIs, CsvHelper, xUnit, GitHub Actions.

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/AeroGraph.Api
```

Then open <http://localhost:5243/routes?from=TLS&to=NOU>. The data loads at startup:

```
Loaded 6072 airports (1626 skipped), 66934 routes (729 skipped), 37042 connections in 109 ms
```

Run the tests:

```bash
dotnet test
```

## Endpoints

| Request | Returns |
|---|---|
| `GET /airports/TLS` | One airport (code lookup ignores case) |
| `GET /airports?search=paris&limit=5` | Airports whose code, name or city contains the text, exact code first |
| `GET /airports/TLS/destinations` | Airports with a direct flight from TLS (46) |
| `GET /routes?from=TLS&to=NOU` | The trip with the fewest stops |
| `GET /routes?from=TLS&to=NOU&maxStops=1` | The same, or a 404 when even the shortest trip has more stops |
| `GET /stats` | What the import kept and skipped |

Example: `GET /routes?from=TLS&to=NOU`

```json
{
  "flights": 3,
  "stops": 2,
  "path": [
    { "iata": "TLS", "name": "Toulouse-Blagnac Airport", "city": "Toulouse", "country": "France", ... },
    { "iata": "LHR", "name": "London Heathrow Airport", "city": "London", ... },
    { "iata": "NRT", "name": "Narita International Airport", "city": "Tokyo", ... },
    { "iata": "NOU", ... }
  ]
}
```

Errors use the standard problem details format (RFC 9457):

- **400** when the request is malformed: `/airports/TOULOUSE` (an IATA code has 3 letters), `?search=p`, `?limit=500`.
- **404** when the request is valid but the thing doesn't exist: `/airports/ZZZ`, no trip within `maxStops`, unknown URL.

The `src/AeroGraph.Api/AeroGraph.Api.http` file has every request ready to send from VS Code (REST Client) or Rider.

## How it works

```
data/*.dat  ->  Import/OpenFlightsLoader  ->  Models (Airport, FlightRoute)  ->  Graph/RouteGraph  ->  Endpoints
 raw CSV        parse, clean, skip & count     our own format                   nodes + edges        JSON over HTTP
```

1. **Import.** The OpenFlights files are CSV with no header and `\N` for missing values. The loader reads them with
   CsvHelper (names like `"Harstad/Narvik Airport, Evenes"` contain commas, so splitting on `,` would break),
   keeps airports that have an IATA code and valid coordinates, and keeps routes whose two airports are known.
   A bad row is skipped and counted instead of stopping the import. Numbers are parsed with the invariant culture,
   so it behaves the same on a French machine and on a Linux server.
2. **Graph.** `RouteGraph` holds two dictionaries: IATA code -> airport (nodes), and IATA code -> set of direct
   destinations (adjacency list). Several airlines flying the same route count as one connection, which is why
   66,934 routes become 37,042 connections.
3. **Route finding.** Breadth-first search, level by level: first every airport one flight away, then two flights,
   and so on. The first time the destination appears, no trip with fewer stops can exist. A visited set prevents
   loops and repeated work, and a came-from map rebuilds the path. Cost: O(V + E), each airport and each connection
   is processed at most once.
4. **API.** The graph is built once at startup and registered as a singleton. It is never modified afterwards, and
   each search uses its own local collections, so concurrent requests can share it safely.

The import layer is the only code that knows the OpenFlights format. A new source (another file, a database,
a partner API) means a new loader that returns the same `Airport` and `FlightRoute` records; the graph and the
endpoints don't change.

## Tests

27 xUnit tests, run on every push by GitHub Actions:

- **Loader** (5): valid rows, commas inside quoted fields, skipped rows counted, French culture, routes to unknown airports.
- **Graph** (10): lookups, search ranking, and route finding on a small hand-made network (direct flight,
  fewest stops, one-way routes, same airport, no route, unknown airport).
- **API** (12): the real app started in memory with `WebApplicationFactory` and called over HTTP, including the
  400 and 404 cases.

## Project structure

```
src/AeroGraph.Api/
  Program.cs            startup: load data, build the graph, register services, map endpoints
  Models/               Airport, FlightRoute (records)
  Import/               OpenFlightsLoader, ImportResult, ImportReport
  Graph/                RouteGraph (nodes, adjacency list, search, BFS)
  Endpoints/            AirportEndpoints, RouteEndpoints, ApiErrors
  data/                 airports.dat, routes.dat (OpenFlights)
tests/AeroGraph.Tests/  OpenFlightsLoaderTests, RouteGraphTests, ApiTests
```

## Limits and next steps

- **The routes are a June 2014 snapshot** (the source stopped updating then). Fine for a demo, not for real travel.
- **1,626 airports without an IATA code are skipped**, and so are the 729 routes whose departure or arrival
  is not one of the kept airports.
- **Search is a simple "contains"**: `paris` also matches *Alpine Casparis*. Next step: rank city matches and
  word starts first.
- **Shortest distance instead of fewest stops** would use Dijkstra's algorithm, with each flight weighted by the
  great-circle distance between the two airports and a `PriorityQueue` instead of plain waves.
- **Persistence.** The graph lives in memory and reloads in 0.1 to 0.2 s. For larger data it could be stored in a
  key-value store such as RocksDB, with keys like `airport:TLS` and `route:TLS:CDG`, so the destinations of an
  airport are one prefix scan away.
- **Immutability.** `FrozenDictionary` / `FrozenSet` would make the read-only graph explicit and faster to read.

## Data and license

Airport and route data from [OpenFlights](https://openflights.org/data.php), made available under the
[Open Database License (ODbL)](https://opendatacommons.org/licenses/odbl/1-0/), with contents under the
Database Contents License. The data is historical, not suitable for navigation, and is used here for a
non-commercial demo.
