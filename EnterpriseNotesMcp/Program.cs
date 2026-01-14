using System.Threading.RateLimiting;
using EnterpriseNotesMcp.Middleware;
using EnterpriseNotesMcp.Services;
using EnterpriseNotesMcp.Storage;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Serilog;

// =============================================================================
// Bootstrap Serilog for startup logging
// =============================================================================
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting EnterpriseNotesMcp server...");

    var builder = WebApplication.CreateBuilder(args);

    // =============================================================================
    // Configure Serilog
    // =============================================================================
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "EnterpriseNotesMcp")
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

    // =============================================================================
    // Configure Services
    // =============================================================================
    
    // Storage
    builder.Services.AddSingleton<INoteRepository, JsonFileNoteRepository>();
    
    // Business Logic
    builder.Services.AddScoped<INoteService, NoteService>();
    
    // MCP Handler
    builder.Services.AddScoped<IMcpHandler, McpHandler>();

    // Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

    // =============================================================================
    // Configure OpenAPI / Swagger
    // =============================================================================
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Enterprise Notes MCP Server",
            Version = "v1",
            Description = """
                An enterprise-grade MCP (Model Context Protocol) server for note management.
                
                This server exposes note management capabilities as MCP tools that can be
                invoked by LLM orchestration systems.
                
                ## MCP Protocol
                
                All requests to `/mcp` use JSON-RPC 2.0 format:
                
                ```json
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "tools/list",
                  "params": {}
                }
                ```
                
                ## Available Tools
                
                - `notes_create`: Create a new note
                - `notes_search`: Search notes by keyword
                - `notes_summarize`: Generate note summaries
                """,
            Contact = new OpenApiContact
            {
                Name = "Enterprise AI Platform Team",
                Email = "ai-platform@company.com"
            }
        });

        // Add API key authentication to Swagger
        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-API-Key",
            Description = "API key for authentication"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // =============================================================================
    // Configure Health Checks
    // =============================================================================
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
        .AddCheck<StorageHealthCheck>("storage");

    // =============================================================================
    // Configure Rate Limiting
    // =============================================================================
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        
        options.AddFixedWindowLimiter("mcp", limiterOptions =>
        {
            limiterOptions.PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimit", 100);
            limiterOptions.Window = TimeSpan.FromSeconds(
                builder.Configuration.GetValue<int>("RateLimiting:WindowSeconds", 60));
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = builder.Configuration.GetValue<int>("RateLimiting:QueueLimit", 10);
        });
    });

    // =============================================================================
    // Configure CORS
    // =============================================================================
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowInternal", policy =>
        {
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? ["http://localhost:*"];
            
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // =============================================================================
    // Configure Middleware Pipeline
    // =============================================================================

    // Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // Swagger (all environments for demo purposes)
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Enterprise Notes MCP v1");
        options.RoutePrefix = "swagger";
    });

    // CORS
    app.UseCors("AllowInternal");

    // Rate limiting
    app.UseRateLimiter();

    // Custom MCP authentication
    app.UseMcpAuth();

    // Health checks
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false // Just returns healthy if the app is running
    });

    // Controllers
    app.MapControllers();

    // Root redirect to Swagger
    app.MapGet("/", () => Results.Redirect("/swagger"));

    Log.Information("EnterpriseNotesMcp server started on {Urls}", string.Join(", ", app.Urls));
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// =============================================================================
// Health Check for Storage
// =============================================================================
public class StorageHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly INoteRepository _repository;

    public StorageHealthCheck(INoteRepository repository)
    {
        _repository = repository;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to read from storage
            await _repository.GetAllAsync(cancellationToken);
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Storage is accessible");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                "Storage is not accessible", 
                ex);
        }
    }
}
