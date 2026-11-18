using BE_ZSM.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BE_ZSM.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        private static readonly Dictionary<string, (string Code, string Message)>
            ConstraintMappings = new(StringComparer.OrdinalIgnoreCase)
            {
                ["IX_Roles_NameNormalized"] = ("ROLE_NAME_ALREADY_EXISTS", "A role with this name already exists"),

                ["IX_Roles_Name"] = ("ROLE_NAME_ALREADY_EXISTS", "A role with this name already exists")
            };

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "An unhandled exception occurred.");
            var sqlException = exception.InnerException as SqlException;
            var constraint = sqlException is { Number: 2601 or 2627 }
                ? ConstraintMappings.FirstOrDefault(x =>
                    sqlException.Message.Contains(
                        $"'{x.Key}'",
                        StringComparison.OrdinalIgnoreCase))
                : default;

            var hasMapping = constraint.Key is not null;

            var statusCode = exception switch
            {
                AppException appException => appException.StatusCode,
                DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
                DbUpdateException when hasMapping => StatusCodes.Status409Conflict,
                DbUpdateException when sqlException is { Number: 2601 or 2627 } => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            var errorCode = exception switch
            {
                AppException appException => appException.ErrorCode,
                DbUpdateConcurrencyException => "TODO_CONCURRENCY_CONFLICT",
                DbUpdateException when hasMapping => constraint.Value.Code,
                DbUpdateException when sqlException is { Number: 2601 or 2627 } => "DATABASE_UNIQUE_CONFLICT",
                _ => "INTERNAL_SERVER_ERROR"
            };

            var message = exception switch
            {
                AppException => exception.Message,
                DbUpdateConcurrencyException => "One or more todos were modified or deleted by another request",
                DbUpdateException when hasMapping => constraint.Value.Message,
                DbUpdateException when sqlException is
                { Number: 2601 or 2627 } => "A unique database constraint was violated",
                _ => "An unexpected error occurred."
            };

            var response = new
            {
                success = false,
                statusCode,
                errorCode,
                message,
                timestamp = DateTime.UtcNow,
                path = httpContext.Request.Path.ToString()
            };

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response.WriteAsJsonAsync(response,  cancellationToken);

            return true;
        }
    }
}