using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// acmod-mcp: the local stdio MCP server for Claude Code (CC-2). Like the CLI, it holds no mod logic of its own (CC-1): each tool calls Core.
// stdout carries the protocol, so every log line goes to stderr.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly(); // every [McpServerToolType] class here
await builder.Build().RunAsync();
