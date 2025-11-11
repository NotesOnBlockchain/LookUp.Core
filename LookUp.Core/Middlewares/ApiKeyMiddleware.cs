namespace LookUp.Scanner.Middlewares
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private const string HeaderName = "Authorization";
        private readonly string _expectedKey;

        public ApiKeyMiddleware(RequestDelegate next, Config.Config config)
        {
            _next = next;
            _expectedKey = config.APIKey ?? throw new Exception("API key not configured");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("API key missing");
                return;
            }

            var token = providedKey.ToString().Replace("ApiKey ", "", StringComparison.OrdinalIgnoreCase).Trim();

            if (token != _expectedKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }

            await _next(context);
        }
    }
}
