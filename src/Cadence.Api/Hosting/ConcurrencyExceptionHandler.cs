using Cadence.Domain.Issues;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Api.Hosting;

/// <summary>
/// Turns a lost optimistic-concurrency race (a row changed between loading and saving it, detected
/// through PostgreSQL's xmin) into a 409 problem, so clients refresh instead of seeing a 500.
/// </summary>
internal sealed class ConcurrencyExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = IssueErrors.Conflict.Description,
                Extensions = { ["code"] = IssueErrors.Conflict.Code },
            },
        });
    }
}
