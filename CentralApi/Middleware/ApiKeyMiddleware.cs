namespace CentralApi.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        public ApiKeyMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip API key validation for health check endpoint
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
            {
                _logger.LogWarning("API key was not provided. Request from: {IpAddress}",
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
                return;
            }

            var apiKey = _configuration["ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("API key is not configured");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "API key is not configured" });
                return;
            }

            if (!apiKey.Equals(extractedApiKey))
            {
                _logger.LogWarning("Invalid API key provided. Request from: {IpAddress}",
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
                return;
            }

            await _next(context);
        }
    }
}
