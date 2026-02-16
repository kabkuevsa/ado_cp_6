using System.Net;
using System.Text.Json;
using task6.Models;

namespace task6.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Произошло необработанное исключение: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            var response = context.Response;

            var errorResponse = new ApiErrorResponse
            {
                Timestamp = DateTime.UtcNow,
                RequestId = context.TraceIdentifier
            };

            switch (exception)
            {
                case Microsoft.Data.Sqlite.SqliteException sqlEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Ошибка базы данных";
                    errorResponse.ErrorType = "DatabaseError";
                    errorResponse.Details.Add("SqliteErrorCode", sqlEx.SqliteErrorCode.ToString());
                    errorResponse.Details.Add("DatabaseError", sqlEx.Message);
                    break;

                case ArgumentException argEx:
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorResponse.Message = "Неверные параметры запроса";
                    errorResponse.ErrorType = "ValidationError";
                    errorResponse.Details.Add("ParameterError", argEx.Message);
                    break;

                case KeyNotFoundException notFoundEx:
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    errorResponse.Message = "Запрашиваемый ресурс не найден";
                    errorResponse.ErrorType = "NotFound";
                    errorResponse.Details.Add("Details", notFoundEx.Message);
                    break;

                default:
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorResponse.Message = _env.IsDevelopment()
                        ? exception.Message
                        : "Произошла внутренняя ошибка сервера";
                    errorResponse.ErrorType = "InternalServerError";

                    if (_env.IsDevelopment())
                    {
                        errorResponse.Details.Add("StackTrace", exception.StackTrace);
                        errorResponse.Details.Add("ExceptionType", exception.GetType().FullName);
                    }
                    break;
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonResponse = JsonSerializer.Serialize(errorResponse, options);

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}