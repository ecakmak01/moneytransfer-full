namespace MoneyTransferService.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        string requestBody = "";
        if (context.Request.ContentLength > 0)
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        var originalBody = context.Response.Body;
        using var newBody = new MemoryStream();
        context.Response.Body = newBody;

        await _next(context);

        newBody.Position = 0;
        var responseBody = await new StreamReader(newBody).ReadToEndAsync();
        newBody.Position = 0;

        await newBody.CopyToAsync(originalBody);

        _logger.LogInformation("HTTP LOG => {log}", new
        {
            Path = context.Request.Path,
            Method = context.Request.Method,
            Query = context.Request.QueryString.ToString(),
            StatusCode = context.Response.StatusCode,
            RequestBody = requestBody,
            ResponseBody = responseBody,
            CorrelationId = context.Response.Headers["X-Correlation-ID"].ToString()
        });
    }
}
