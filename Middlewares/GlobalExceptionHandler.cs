using Amazon.Runtime.Endpoints;
using Smart_Financial_Management_SFM_BE.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

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
              AppException appException => appException.StatusCode, _ => StatusCodes.Status500InternalServerError
            };

            var errorCode = exception switch
            {
                AppException appException => appException.ErrorCode,
                _ => "INTERNAL_SERVER_ERROR"
            };

            var response = new
            {
                success = false,
                statusCode,
                errorCode,
            
                message = exception is AppException ? exception.Message : "An unexpected error occurred.", 
                timestamp = DateTime.UtcNow,
                path = httpContext.Request.Path.ToString(),
            };

            httpContext.Response.StatusCode = statusCode;

            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
