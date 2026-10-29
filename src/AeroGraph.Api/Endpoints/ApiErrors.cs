using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AeroGraph.Api.Endpoints;

/// <summary>
/// Input checks and error responses shared by the endpoints.
/// 400 = the request itself is wrong (bad format). 404 = the request is fine, but what it asks for doesn't exist.
/// </summary>
internal static class ApiErrors
{
    // [NotNullWhen(true)] tells the compiler that when this returns true, the code is not null.
    public static bool IsIataCode([NotNullWhen(true)] string? code) =>
        code is { Length: 3 } && code.All(char.IsAsciiLetter);

    public static ValidationProblem InvalidCode(string parameter) =>
        Invalid(parameter, "An IATA airport code has 3 letters, like TLS.");

    public static ValidationProblem Invalid(string parameter, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [parameter] = [message] });

    public static ProblemHttpResult UnknownAirport(string code) =>
        NotFound("Airport not found", $"No airport with the code {code.ToUpperInvariant()}.");

    public static ProblemHttpResult NotFound(string title, string detail) =>
        TypedResults.Problem(title: title, detail: detail, statusCode: StatusCodes.Status404NotFound);
}
