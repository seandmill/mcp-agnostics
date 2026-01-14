#!/usr/bin/env node
/**
 * Local Knowledge Index MCP Server
 * 
 * A minimal MCP server providing document ingestion and TF/keyword search.
 * Tools: docs_ingest, docs_query
 * Resources: docs://<id>
 */

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
    CallToolRequestSchema,
    ListToolsRequestSchema,
    ListResourcesRequestSchema,
    ReadResourceRequestSchema,
    ErrorCode,
    McpError,
} from "@modelcontextprotocol/sdk/types.js";
import * as fs from "fs";
import * as path from "path";
import { fileURLToPath } from "url";

// --- Types ---

interface DocumentMetadata {
    [key: string]: unknown;
}

interface Document {
    id: string;
    text: string;
    metadata: DocumentMetadata;
    createdAt: string;
    tokens: string[]; // Pre-tokenized for search
}

interface DocumentStore {
    documents: Record<string, Document>;
}

// --- Persistence ---

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const DATA_FILE = path.join(__dirname, "..", "documents.json");

function loadStore(): DocumentStore {
    try {
        if (fs.existsSync(DATA_FILE)) {
            const data = fs.readFileSync(DATA_FILE, "utf-8");
            return JSON.parse(data) as DocumentStore;
        }
    } catch (err) {
        console.error("Warning: Could not load data file, starting fresh:", err);
    }
    return { documents: {} };
}

function saveStore(store: DocumentStore): void {
    // Atomic write: write to temp file then rename
    const tempFile = DATA_FILE + ".tmp";
    fs.writeFileSync(tempFile, JSON.stringify(store, null, 2), "utf-8");
    fs.renameSync(tempFile, DATA_FILE);
}

// --- Tokenization & Search ---

function tokenize(text: string): string[] {
    return text
        .toLowerCase()
        .replace(/[^\w\s]/g, " ")
        .split(/\s+/)
        .filter((t) => t.length > 1);
}

/**
 * Simple TF (Term Frequency) scoring.
 * Score = sum of query term frequencies in document.
 */
function scoreDocument(doc: Document, queryTokens: string[]): number {
    const docTokenCounts = new Map<string, number>();
    for (const token of doc.tokens) {
        docTokenCounts.set(token, (docTokenCounts.get(token) || 0) + 1);
    }

    let score = 0;
    for (const queryToken of queryTokens) {
        score += docTokenCounts.get(queryToken) || 0;
    }
    return score;
}

interface SearchResult {
    id: string;
    score: number;
    snippet: string;
    metadata: DocumentMetadata;
}

function searchDocuments(
    store: DocumentStore,
    query: string,
    limit: number
): SearchResult[] {
    const queryTokens = tokenize(query);
    if (queryTokens.length === 0) {
        return [];
    }

    const results: SearchResult[] = [];

    for (const doc of Object.values(store.documents)) {
        const score = scoreDocument(doc, queryTokens);
        if (score > 0) {
            // Create snippet: first 150 chars
            const snippet =
                doc.text.length > 150 ? doc.text.slice(0, 150) + "..." : doc.text;
            results.push({
                id: doc.id,
                score,
                snippet,
                metadata: doc.metadata,
            });
        }
    }

    // Sort by score descending
    results.sort((a, b) => b.score - a.score);
    return results.slice(0, limit);
}

// --- Input Validation ---

function validateIngestInput(args: unknown): {
    id: string;
    text: string;
    metadata: DocumentMetadata;
} {
    if (typeof args !== "object" || args === null) {
        throw new McpError(ErrorCode.InvalidParams, "Arguments must be an object");
    }

    const { id, text, metadata } = args as Record<string, unknown>;

    if (typeof id !== "string" || id.trim().length === 0) {
        throw new McpError(
            ErrorCode.InvalidParams,
            "id must be a non-empty string"
        );
    }

    if (typeof text !== "string" || text.trim().length === 0) {
        throw new McpError(
            ErrorCode.InvalidParams,
            "text must be a non-empty string"
        );
    }

    if (metadata !== undefined && typeof metadata !== "object") {
        throw new McpError(ErrorCode.InvalidParams, "metadata must be an object");
    }

    return {
        id: id.trim(),
        text: text.trim(),
        metadata: (metadata as DocumentMetadata) || {},
    };
}

function validateQueryInput(args: unknown): { query: string; limit: number } {
    if (typeof args !== "object" || args === null) {
        throw new McpError(ErrorCode.InvalidParams, "Arguments must be an object");
    }

    const { query, limit } = args as Record<string, unknown>;

    if (typeof query !== "string" || query.trim().length === 0) {
        throw new McpError(
            ErrorCode.InvalidParams,
            "query must be a non-empty string"
        );
    }

    let parsedLimit = 10; // default
    if (limit !== undefined) {
        if (typeof limit !== "number" || limit < 1 || limit > 100) {
            throw new McpError(
                ErrorCode.InvalidParams,
                "limit must be a number between 1 and 100"
            );
        }
        parsedLimit = Math.floor(limit);
    }

    return { query: query.trim(), limit: parsedLimit };
}

// --- MCP Server Setup ---

const server = new Server(
    {
        name: "local-knowledge",
        version: "1.0.0",
    },
    {
        capabilities: {
            tools: {},
            resources: {},
        },
    }
);

// Load document store
let store = loadStore();

// --- Tool Handlers ---

server.setRequestHandler(ListToolsRequestSchema, async () => {
    return {
        tools: [
            {
                name: "docs_ingest",
                description:
                    "Ingest a document into the local knowledge index. The document will be persisted and indexed for keyword search.",
                inputSchema: {
                    type: "object" as const,
                    properties: {
                        id: {
                            type: "string",
                            description: "Unique identifier for the document",
                        },
                        text: {
                            type: "string",
                            description: "The full text content of the document",
                        },
                        metadata: {
                            type: "object",
                            description: "Optional metadata (author, tags, source, etc.)",
                        },
                    },
                    required: ["id", "text"],
                },
            },
            {
                name: "docs_query",
                description:
                    "Search the local knowledge index using keyword/TF scoring. Returns ranked document matches.",
                inputSchema: {
                    type: "object" as const,
                    properties: {
                        query: {
                            type: "string",
                            description: "Search query (keywords)",
                        },
                        limit: {
                            type: "number",
                            description: "Maximum number of results (default: 10, max: 100)",
                        },
                    },
                    required: ["query"],
                },
            },
        ],
    };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    switch (name) {
        case "docs_ingest": {
            const { id, text, metadata } = validateIngestInput(args);

            const isUpdate = id in store.documents;
            const doc: Document = {
                id,
                text,
                metadata,
                createdAt: isUpdate
                    ? store.documents[id].createdAt
                    : new Date().toISOString(),
                tokens: tokenize(text),
            };

            store.documents[id] = doc;
            saveStore(store);

            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify(
                            {
                                success: true,
                                action: isUpdate ? "updated" : "created",
                                id,
                                tokenCount: doc.tokens.length,
                            },
                            null,
                            2
                        ),
                    },
                ],
            };
        }

        case "docs_query": {
            const { query, limit } = validateQueryInput(args);
            const results = searchDocuments(store, query, limit);

            return {
                content: [
                    {
                        type: "text",
                        text: JSON.stringify(
                            {
                                query,
                                resultCount: results.length,
                                results,
                            },
                            null,
                            2
                        ),
                    },
                ],
            };
        }

        default:
            throw new McpError(ErrorCode.MethodNotFound, `Unknown tool: ${name}`);
    }
});

// --- Resource Handlers ---

server.setRequestHandler(ListResourcesRequestSchema, async () => {
    const resources = Object.keys(store.documents).map((id) => ({
        uri: `docs://${id}`,
        name: id,
        description: `Document: ${id}`,
        mimeType: "application/json",
    }));

    return { resources };
});

server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
    const { uri } = request.params;

    // Parse docs://<id> URI
    const match = uri.match(/^docs:\/\/(.+)$/);
    if (!match) {
        throw new McpError(ErrorCode.InvalidRequest, `Invalid resource URI: ${uri}`);
    }

    const id = match[1];
    const doc = store.documents[id];

    if (!doc) {
        throw new McpError(ErrorCode.InvalidRequest, `Document not found: ${id}`);
    }

    return {
        contents: [
            {
                uri,
                mimeType: "application/json",
                text: JSON.stringify(
                    {
                        id: doc.id,
                        text: doc.text,
                        metadata: doc.metadata,
                        createdAt: doc.createdAt,
                    },
                    null,
                    2
                ),
            },
        ],
    };
});

// --- Main ---

async function main() {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error("Local Knowledge MCP server running on stdio");
}

main().catch((err) => {
    console.error("Fatal error:", err);
    process.exit(1);
});
