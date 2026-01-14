namespace EnterpriseNotesMcp.Middleware;

/// <summary>
/// Authentication middleware for MCP requests.
/// Validates API keys and extracts user identity.
/// 
/// In production, replace with proper authentication:
/// - Azure AD / Entra ID integration
/// - JWT validation
/// - mTLS certificate validation
/// </summary>
public class McpAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpAuthMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public McpAuthMiddleware(
        RequestDelegate next,
        ILogger<McpAuthMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for health checks and OpenAPI
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/health") || 
            path.StartsWith("/swagger") || 
            path.StartsWith("/metrics") ||
            path == "/mcp/info")
        {
            await _next(context);
            return;
        }

        // Check for API key in header
        var authEnabled = _configuration.GetValue<bool>("Authentication:Enabled");
        
        if (authEnabled)
        {
            if (!context.Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
            {
                _logger.LogWarning("Missing API key in request to {Path}", path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Unauthorized",
                    message = "Missing X-API-Key header"
                });
                return;
            }

            var apiKey = apiKeyHeader.ToString();
            var validApiKeys = _configuration.GetSection("Authentication:ApiKeys").Get<List<ApiKeyConfig>>() ?? [];

            var matchedKey = validApiKeys.FirstOrDefault(k => k.Key == apiKey);
            if (matchedKey == null)
            {
                _logger.LogWarning("Invalid API key attempted for {Path}", path);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Unauthorized",
                    message = "Invalid API key"
                });
                return;
            }

            // Set user identity from API key
            context.Items["UserId"] = matchedKey.UserId;
            context.Items["UserRole"] = matchedKey.Role;
            
            _logger.LogDebug(
                "Authenticated request: UserId={UserId}, Role={Role}", 
                matchedKey.UserId, 
                matchedKey.Role);
        }

        await _next(context);
    }
}

/// <summary>
/// Configuration for API key authentication.
/// </summary>
public class ApiKeyConfig
{
    public required string Key { get; init; }
    public required string UserId { get; init; }
    public string Role { get; init; } = "user";
}

/// <summary>
/// Extension methods for adding MCP auth middleware.
/// </summary>
public static class McpAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseMcpAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<McpAuthMiddleware>();
    }
}
