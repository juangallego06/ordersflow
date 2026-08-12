using OrdersApi.Api.Common;

namespace OrdersApi.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepcion no controlada procesando {Path}", context.Request.Path);

            var (statusCode, message) = MapException(ex);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var response = ApiResponse<object>.Fail(statusCode, message, ex.Message);
            await context.Response.WriteAsJsonAsync(response);
        }
    }

    private static (int StatusCode, string Message) MapException(Exception ex) => ex switch
    {
        ArgumentException => (StatusCodes.Status400BadRequest, "Los datos enviados no son validos."),
        InvalidOperationException => (StatusCodes.Status409Conflict, "La operacion no es valida en el estado actual."),
        _ => (StatusCodes.Status500InternalServerError, "Ocurrio un error inesperado.")
    };
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}