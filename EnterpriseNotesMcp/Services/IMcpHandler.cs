using EnterpriseNotesMcp.Models;

namespace EnterpriseNotesMcp.Services;

/// <summary>
/// Interface for handling MCP protocol requests.
/// </summary>
public interface IMcpHandler
{
    /// <summary>
    /// Process a JSON-RPC request and return the appropriate response.
    /// </summary>
    Task<object> HandleRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken = default);
}
