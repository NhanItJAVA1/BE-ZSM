using Amazon.Runtime.Endpoints;
using BE_ZSM.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Smart_Financial_Management_SFM_BE.Middlewares
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }   

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "An unhandled exception occurred.");

            var statusCode = exception switch
            {
                AppException appException => appException.StatusCode,
                DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            var errorCode = exception switch
            {
                AppException appException => appException.ErrorCode,
                DbUpdateConcurrencyException => "TODO_CONCURRENCY_CONFLICT",
                _ => "INTERNAL_SERVER_ERROR"
            };

            var message = exception switch
            {
                AppException => exception.Message,
                DbUpdateConcurrencyException => "One or more todos were modified or deleted by another request",
                _ => "An unexpected error occurred."
            };

            var response = new
            {
                success = false,
                statusCode,
                errorCode,
                message,
                timestamp = DateTime.UtcNow,
                path = httpContext.Request.Path.ToString(),
            };

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
