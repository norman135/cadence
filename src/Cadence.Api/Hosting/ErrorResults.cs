using Cadence.Domain.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cadence.Api.Hosting;

/// <summary>Maps expected failures (<see cref="Error"/>) to RFC 9457 problem responses in one place.</summary>
internal static class ErrorResults
{
    /// <summary>
    /// Returns the problem response for an error. The stable error code is included as <c>code</c>
    /// so clients can react to specific failures without parsing messages.
    /// </summary>
    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Bad Request"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Bad Request"),
        };

        return TypedResults.Problem(
            statusCode: status,
            title: title,
            detail: error.Description,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
