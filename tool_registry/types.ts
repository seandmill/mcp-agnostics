/**
 * Shared types for Tool Registry
 */

export type RiskTier = 'low' | 'medium' | 'high';

export interface UserContext {
  userId: string;
  role: 'agent' | 'senior_agent' | 'finance' | 'executive' | 'hr' | 'analyst' | 'data_scientist' | 'knowledge_admin';
  department?: string;
}

export interface McpServerConfig {
  name: string;
  description: string;
  transport: 'http' | 'stdio';
  endpoint: string;
  health_endpoint?: string;
  auth?: {
    type: 'api_key' | 'oauth' | 'mtls';
    header?: string;
    secret_ref?: string;
  };
  owner: string;
  contact: string;
}

export interface McpBinding {
  server: string;
  serverConfig: McpServerConfig;
  method: string;
  tool_name: string;
}

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
  
  // Local handler (for non-MCP tools)
  handler?: (input: unknown, userContext?: UserContext) => Promise<unknown>;
  
  // MCP binding (for MCP server tools)
  mcp?: McpBinding;
}

export interface ToolCatalog {
  version: string;
  registry_name: string;
  compiled_at: string;
  mcp_servers: Record<string, McpServerConfig>;
  tools: Array<{
    id: string;
    name: string;
    description: string;
    version: string;
    mcp: McpBinding;
    risk_tier: RiskTier;
    idempotent: boolean;
    scopes: {
      data: string[];
      actions: string[];
    };
    required_roles: string[];
    timeout_ms: number;
    retry_policy: string;
    max_retries: number;
    input_schema: object;
    output_schema: object;
    redaction: {
      input: Array<{ field: string; action: string }>;
      output: Array<{ field: string; action: string }>;
    };
  }>;
}
