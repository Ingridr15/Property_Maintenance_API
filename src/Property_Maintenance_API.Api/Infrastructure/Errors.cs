using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Property_Maintenance_API.Domain;

namespace Property_Maintenance_API.Api.Infrastructure;

public static class ErrorResults
{
    // Error de negocio -> respuesta HTTP con el código correcto.
    public static IResult ToProblem(this Error error)
    {
        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound   => StatusCodes.Status404NotFound,
            _                    => StatusCodes.Status409Conflict,
        };

        var problem = error.Fields is { Count: > 0 }
            ? new HttpValidationProblemDetails(error.Fields.ToDictionary(f => f.Key, f => f.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = error.Kind.ToString();
        problem.Detail = error.Message;
        problem.Extensions["code"] = error.Code;

        return Results.Problem(problem);
    }
}

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, code, detail) = exception switch
        {
            BadHttpRequestException bad => (StatusCodes.Status400BadRequest, "BAD_REQUEST", bad.Message),
            SqlException sql when sql.Number is 50001 or 2627 or 2601 or 547 =>
                (StatusCodes.Status409Conflict, "CONSTRAINT_VIOLATION", sql.Message),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Ocurrio un error inesperado."),
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Error no controlado en {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Detail = detail, Extensions = { ["code"] = code } },
        });
    }
}