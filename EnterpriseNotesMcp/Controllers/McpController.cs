using System.Text.Json;
using EnterpriseNotesMcp.Models;
using EnterpriseNotesMcp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EnterpriseNotesMcp.Controllers;

/// <summary>
/// HTTP endpoint for MCP protocol.
/// Receives JSON-RPC requests and returns JSON-RPC responses.
/// </summary>
[ApiController]
[Route("mcp")]
[EnableRateLimiting("mcp")]
public class McpController : ControllerBase
{
    private readonly IMcpHandler _handler;
    private readonly ILogger<McpController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

    public McpController(IMcpHandler handler, ILogger<McpController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <summary>
    /// Main MCP endpoint. Accepts JSON-RPC 2.0 requests.
    /// </summary>
    /// <remarks>
    /// This endpoint handles all MCP protocol methods:
    /// - initialize: Initialize the MCP session
    /// - tools/list: List available tools
    /// - tools/call: Execute a tool
    /// - resources/list: List available resources
    /// - resources/read: Read a specific resource
    /// </remarks>
    /// <param name="request">JSON-RPC 2.0 request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON-RPC 2.0 response</returns>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType(typeof(JsonRpcResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(JsonRpcErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HandleRequest(
        [FromBody] JsonRpcRequest request,
        CancellationToken cancellationToken)
    {
        // Log incoming request (sanitized)
        _logger.LogInformation(
            "MCP request received: Method={Method}, Id={Id}",
            request.Method,
            request.Id);

        // Validate JSON-RPC version
        if (request.JsonRpc != "2.0")
        {
            return BadRequest(new JsonRpcErrorResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = JsonRpcErrorCodes.InvalidRequest,
                    Message = "Invalid JSON-RPC version. Expected 2.0"
                }
            });
        }

        // Handle the request
        var response = await _handler.HandleRequestAsync(request, cancellationToken);

        // Log response type
        if (response is JsonRpcErrorResponse errorResponse)
        {
            _logger.LogWarning(
                "MCP request failed: Method={Method}, Error={Error}",
                request.Method,
                errorResponse.Error.Message);
        }
        else
        {
            _logger.LogInformation(
                "MCP request completed: Method={Method}",
                request.Method);
        }

        return Ok(response);
    }

    /// <summary>
    /// Batch endpoint for multiple MCP requests.
    /// Processes multiple JSON-RPC requests in a single HTTP call.
    /// </summary>
    [HttpPost("batch")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> HandleBatchRequest(
        [FromBody] List<JsonRpcRequest> requests,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP batch request received: Count={Count}", requests.Count);

        var responses = new List<object>();

        foreach (var request in requests)
        {
            var response = await _handler.HandleRequestAsync(request, cancellationToken);
            responses.Add(response);
        }

        return Ok(responses);
    }

    /// <summary>
    /// Get server info (non-MCP endpoint for discovery/monitoring).
    /// </summary>
    [HttpGet("info")]
    [Produces("application/json")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            name = "EnterpriseNotesMcp",
            version = "1.0.0",
            protocol = "MCP",
            protocolVersion = "2024-11-05",
            transport = "HTTP",
            endpoints = new
            {
                mcp = "/mcp",
                batch = "/mcp/batch",
                health = "/health",
                metrics = "/metrics"
            }
        });
    }
}
