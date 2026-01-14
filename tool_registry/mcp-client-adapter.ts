/**
 * MCP Client Adapter for Tool Gateway
 * 
 * This module shows how the Lattice Tool Gateway would call MCP servers.
 * It acts as the MCP client layer within the Tool Gateway.
 * 
 * Integration point: This would be used by the Tool Gateway's execute.ts
 * when a tool has an MCP binding instead of a local handler.
 */

import type { ToolDefinition } from './types';

// =============================================================================
// Types
// =============================================================================

interface McpServerConfig {
  name: string;
  endpoint: string;
  auth?: {
    type: 'api_key' | 'oauth' | 'mtls';
    header?: string;
    secret_ref?: string;
  };
}

interface McpBinding {
  server: string;
  serverConfig: McpServerConfig;
  method: string;
  tool_name: string;
}

interface JsonRpcRequest {
  jsonrpc: '2.0';
  id: number;
  method: string;
  params: unknown;
}

interface JsonRpcResponse<T = unknown> {
  jsonrpc: '2.0';
  id: number;
  result?: T;
  error?: {
    code: number;
    message: string;
    data?: unknown;
  };
}

interface McpCallToolResult {
  content: Array<{ type: string; text: string }>;
  isError?: boolean;
}

// =============================================================================
// MCP Client
// =============================================================================

export class McpClientAdapter {
  private requestId = 0;
  private secretsResolver: (secretRef: string) => Promise<string>;

  constructor(secretsResolver: (secretRef: string) => Promise<string>) {
    this.secretsResolver = secretsResolver;
  }

  /**
   * Call a tool on an MCP server
   */
  async callTool(
    mcpBinding: McpBinding,
    toolArguments: unknown,
    correlationId: string
  ): Promise<McpCallToolResult> {
    const { serverConfig, tool_name } = mcpBinding;

    // Build headers
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-Correlation-ID': correlationId,
    };

    // Add authentication
    if (serverConfig.auth) {
      const apiKey = await this.secretsResolver(serverConfig.auth.secret_ref!);
      headers[serverConfig.auth.header || 'X-API-Key'] = apiKey;
    }

    // Build JSON-RPC request
    const rpcRequest: JsonRpcRequest = {
      jsonrpc: '2.0',
      id: ++this.requestId,
      method: 'tools/call',
      params: {
        name: tool_name,
        arguments: toolArguments,
      },
    };

    // Make HTTP request to MCP server
    const response = await fetch(serverConfig.endpoint, {
      method: 'POST',
      headers,
      body: JSON.stringify(rpcRequest),
    });

    if (!response.ok) {
      throw new McpClientError(
        `MCP server returned ${response.status}: ${response.statusText}`,
        serverConfig.name,
        tool_name
      );
    }

    const rpcResponse = (await response.json()) as JsonRpcResponse<McpCallToolResult>;

    if (rpcResponse.error) {
      throw new McpClientError(
        rpcResponse.error.message,
        serverConfig.name,
        tool_name,
        rpcResponse.error.code
      );
    }

    return rpcResponse.result!;
  }

  /**
   * List tools from an MCP server (for discovery/sync)
   */
  async listTools(serverConfig: McpServerConfig): Promise<unknown[]> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (serverConfig.auth) {
      const apiKey = await this.secretsResolver(serverConfig.auth.secret_ref!);
      headers[serverConfig.auth.header || 'X-API-Key'] = apiKey;
    }

    const rpcRequest: JsonRpcRequest = {
      jsonrpc: '2.0',
      id: ++this.requestId,
      method: 'tools/list',
      params: {},
    };

    const response = await fetch(serverConfig.endpoint, {
      method: 'POST',
      headers,
      body: JSON.stringify(rpcRequest),
    });

    const rpcResponse = (await response.json()) as JsonRpcResponse<{ tools: unknown[] }>;

    if (rpcResponse.error) {
      throw new McpClientError(
        rpcResponse.error.message,
        serverConfig.name,
        'tools/list',
        rpcResponse.error.code
      );
    }

    return rpcResponse.result?.tools ?? [];
  }

  /**
   * Health check for an MCP server
   */
  async healthCheck(serverConfig: McpServerConfig & { health_endpoint?: string }): Promise<boolean> {
    try {
      const endpoint = serverConfig.health_endpoint || serverConfig.endpoint.replace('/mcp', '/health');
      const response = await fetch(endpoint);
      return response.ok;
    } catch {
      return false;
    }
  }
}

export class McpClientError extends Error {
  constructor(
    message: string,
    public serverName: string,
    public toolName: string,
    public code?: number
  ) {
    super(`[${serverName}/${toolName}] ${message}`);
    this.name = 'McpClientError';
  }
}

// =============================================================================
// Example: How to integrate with existing Tool Gateway
// =============================================================================

/**
 * This shows how you would modify the Tool Gateway's execute.ts
 * to support both local handlers AND MCP server delegation.
 */

/*
// In tool-gateway/src/registry/tools.ts, extend ToolDefinition:

export interface ToolDefinition {
  id: string;
  name: string;
  description: string;
  version: string;
  riskTier: RiskTier;
  inputSchema: object;
  outputSchema: object;
  requiredScopes: string[];
  requiredRoles?: string[];
  timeout: number;
  idempotent: boolean;
  
  // Either a local handler OR an MCP binding (mutually exclusive)
  handler?: (input: unknown, userContext?: UserContext) => Promise<unknown>;
  mcp?: {
    server: string;
    serverConfig: McpServerConfig;
    method: string;
    tool_name: string;
  };
}

// In tool-gateway/src/routes/execute.ts, modify the execution logic:

import { McpClientAdapter } from '../mcp/client-adapter.js';

const mcpClient = new McpClientAdapter(async (secretRef) => {
  // In production: fetch from Azure Key Vault, AWS Secrets Manager, etc.
  return process.env[secretRef] || 'dev-key';
});

// In the execute handler:
if (tool.handler) {
  // Local handler execution (existing logic)
  result = await tool.handler(input, userContext);
} else if (tool.mcp) {
  // MCP server delegation
  const mcpResult = await mcpClient.callTool(tool.mcp, input, correlationId);
  
  // Extract text content from MCP response
  const textContent = mcpResult.content.find(c => c.type === 'text');
  if (textContent) {
    try {
      result = JSON.parse(textContent.text);
    } catch {
      result = { text: textContent.text };
    }
  }
} else {
  throw new Error(`Tool ${toolId} has no handler or MCP binding`);
}
*/

// =============================================================================
// Example: Loading tools from compiled catalog
// =============================================================================

/**
 * Load MCP tools from the compiled tool_catalog.json into the Tool Gateway
 */
export async function loadMcpToolsFromCatalog(catalogPath: string): Promise<ToolDefinition[]> {
  const catalogContent = await import(catalogPath, { assert: { type: 'json' } });
  const catalog = catalogContent.default;

  const tools: ToolDefinition[] = [];

  for (const tool of catalog.tools) {
    tools.push({
      id: tool.id,
      name: tool.name,
      description: tool.description,
      version: tool.version,
      riskTier: tool.risk_tier,
      inputSchema: tool.input_schema,
      outputSchema: tool.output_schema,
      requiredScopes: [...tool.scopes.data, ...tool.scopes.actions],
      requiredRoles: tool.required_roles,
      timeout: tool.timeout_ms,
      idempotent: tool.idempotent,
      // MCP binding instead of local handler
      mcp: tool.mcp,
    });
  }

  return tools;
}
