#!/usr/bin/env npx ts-node
/**
 * Tool Registry Compiler
 * 
 * Reads all tool.yaml files and registry.config.yaml,
 * validates them, and compiles to a single tool_catalog.json.
 * 
 * Usage: npx ts-node compile_registry.ts
 * 
 * This runs as part of CI/CD to validate and publish the registry.
 */

import * as fs from 'fs';
import * as path from 'path';
import * as yaml from 'yaml';

interface McpServerConfig {
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

interface McpBinding {
  server: string;
  method: string;
  tool_name: string;
}

interface ToolDefinitionYaml {
  tool: {
    id: string;
    name: string;
    description: string;
    version: string;
    mcp: McpBinding;
    risk_tier: 'low' | 'medium' | 'high';
    idempotent: boolean;
    scopes: {
      data: string[];
      actions: string[];
    };
    required_roles: string[];
    timeout_ms: number;
    retry_policy?: string;
    max_retries?: number;
    input_schema: object;
    output_schema: object;
    redaction?: {
      input?: Array<{ field: string; action: string }>;
      output?: Array<{ field: string; action: string }>;
    };
  };
}

interface RegistryConfig {
  version: string;
  registry_name: string;
  description: string;
  mcp_servers: Record<string, McpServerConfig>;
  defaults: Record<string, unknown>;
  risk_tiers: Record<string, unknown>;
}

interface CompiledTool {
  id: string;
  name: string;
  description: string;
  version: string;
  mcp: {
    server: string;
    serverConfig: McpServerConfig;
    method: string;
    tool_name: string;
  };
  risk_tier: string;
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
}

interface ToolCatalog {
  version: string;
  registry_name: string;
  compiled_at: string;
  mcp_servers: Record<string, McpServerConfig>;
  tools: CompiledTool[];
}

const REGISTRY_DIR = path.dirname(new URL(import.meta.url).pathname);
const CONFIG_FILE = path.join(REGISTRY_DIR, 'registry.config.yaml');
const SERVERS_DIR = path.join(REGISTRY_DIR, 'servers');
const DIST_DIR = path.join(REGISTRY_DIR, 'dist');

function loadConfig(): RegistryConfig {
  const content = fs.readFileSync(CONFIG_FILE, 'utf-8');
  return yaml.parse(content) as RegistryConfig;
}

function findToolYamlFiles(dir: string): string[] {
  const files: string[] = [];
  
  function walk(currentDir: string) {
    if (!fs.existsSync(currentDir)) return;
    
    const entries = fs.readdirSync(currentDir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(currentDir, entry.name);
      if (entry.isDirectory()) {
        walk(fullPath);
      } else if (entry.name === 'tool.yaml') {
        files.push(fullPath);
      }
    }
  }
  
  walk(dir);
  return files;
}

function loadToolDefinition(filePath: string): ToolDefinitionYaml {
  const content = fs.readFileSync(filePath, 'utf-8');
  return yaml.parse(content) as ToolDefinitionYaml;
}

function validateTool(tool: ToolDefinitionYaml, servers: Record<string, McpServerConfig>): string[] {
  const errors: string[] = [];
  const t = tool.tool;
  
  if (!t.id) errors.push('Missing tool.id');
  if (!t.name) errors.push('Missing tool.name');
  if (!t.version) errors.push('Missing tool.version');
  if (!t.mcp?.server) errors.push('Missing tool.mcp.server');
  if (!t.mcp?.tool_name) errors.push('Missing tool.mcp.tool_name');
  if (!t.input_schema) errors.push('Missing tool.input_schema');
  if (!t.output_schema) errors.push('Missing tool.output_schema');
  
  if (t.mcp?.server && !servers[t.mcp.server]) {
    errors.push(`Unknown MCP server: ${t.mcp.server}`);
  }
  
  if (!['low', 'medium', 'high'].includes(t.risk_tier)) {
    errors.push(`Invalid risk_tier: ${t.risk_tier}`);
  }
  
  return errors;
}

function compileTool(
  tool: ToolDefinitionYaml,
  servers: Record<string, McpServerConfig>,
  defaults: Record<string, unknown>
): CompiledTool {
  const t = tool.tool;
  const serverConfig = servers[t.mcp.server]!;
  
  return {
    id: t.id,
    name: t.name,
    description: t.description,
    version: t.version,
    mcp: {
      server: t.mcp.server,
      serverConfig,
      method: t.mcp.method || 'tools/call',
      tool_name: t.mcp.tool_name,
    },
    risk_tier: t.risk_tier,
    idempotent: t.idempotent ?? false,
    scopes: t.scopes || { data: [], actions: [] },
    required_roles: t.required_roles || [],
    timeout_ms: t.timeout_ms || (defaults.timeout_ms as number) || 10000,
    retry_policy: t.retry_policy || (defaults.retry_policy as string) || 'none',
    max_retries: t.max_retries || (defaults.max_retries as number) || 0,
    input_schema: t.input_schema,
    output_schema: t.output_schema,
    redaction: {
      input: t.redaction?.input || [],
      output: t.redaction?.output || [],
    },
  };
}

async function main() {
  console.log('Tool Registry Compiler');
  console.log('======================\n');
  
  // Load config
  console.log('Loading registry config...');
  const config = loadConfig();
  console.log(`  Registry: ${config.registry_name}`);
  console.log(`  MCP Servers: ${Object.keys(config.mcp_servers).join(', ')}\n`);
  
  // Find all tool.yaml files
  console.log('Scanning for tool definitions...');
  const toolFiles = findToolYamlFiles(SERVERS_DIR);
  console.log(`  Found ${toolFiles.length} tool definitions\n`);
  
  // Load and validate tools
  console.log('Validating tools...');
  const tools: CompiledTool[] = [];
  let hasErrors = false;
  
  for (const filePath of toolFiles) {
    const relativePath = path.relative(REGISTRY_DIR, filePath);
    const toolDef = loadToolDefinition(filePath);
    const errors = validateTool(toolDef, config.mcp_servers);
    
    if (errors.length > 0) {
      console.log(`  [FAIL] ${relativePath}`);
      for (const error of errors) {
        console.log(`         - ${error}`);
      }
      hasErrors = true;
    } else {
      console.log(`  [OK]   ${relativePath} -> ${toolDef.tool.id}`);
      tools.push(compileTool(toolDef, config.mcp_servers, config.defaults));
    }
  }
  
  if (hasErrors) {
    console.error('\nValidation failed. Fix errors and try again.');
    process.exit(1);
  }
  
  // Compile catalog
  const catalog: ToolCatalog = {
    version: config.version,
    registry_name: config.registry_name,
    compiled_at: new Date().toISOString(),
    mcp_servers: config.mcp_servers,
    tools,
  };
  
  // Write output
  if (!fs.existsSync(DIST_DIR)) {
    fs.mkdirSync(DIST_DIR, { recursive: true });
  }
  
  const outputPath = path.join(DIST_DIR, 'tool_catalog.json');
  fs.writeFileSync(outputPath, JSON.stringify(catalog, null, 2));
  
  console.log(`\nCompiled ${tools.length} tools to ${outputPath}`);
  console.log('\nTool Summary:');
  console.log('─'.repeat(60));
  for (const tool of tools) {
    console.log(`  ${tool.id.padEnd(25)} ${tool.risk_tier.padEnd(8)} ${tool.mcp.server}`);
  }
  console.log('─'.repeat(60));
  console.log('\nDone!');
}

main().catch(console.error);
