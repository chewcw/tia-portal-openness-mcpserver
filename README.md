# TIA Portal Openness MCP Server

MCP (Model Context Protocol) server for Siemens TIA Portal Openness, plus a companion CLI for release installation and updates.

## What this repository contains

- `TiaPortalMcpServer/`: C# MCP server (target framework: `net48`) exposing TIA Portal engineering operations as MCP tools.
- `TiaPortalMcpServer.Tests/`: unit and integration tests for tool contracts and server behavior.
- `cli/`: TypeScript CLI for listing, downloading, installing, updating, and running server releases.

## Key capabilities

The server provides tool groups for:

- project lifecycle (`projects_create`, `projects_open`, `projects_save`, `projects_close`)
- hardware and device management (`devices_list`, `devices_create`, `devices_search_catalog`)
- device item management (`deviceitems_list`, `deviceitems_get_attributes`, `deviceitems_delete`)
- PLC software and block operations (`software_add_block`, `blocks_list`, `software_get_block_hierarchy`)
- tag and tag-table management (`tags_create`, `tags_tagtable_create`, `tags_group_create`)
- compilation (`compilation_project`, `compilation_software`)
- file import and validation (`files_read_csv`, `files_read_excel`, `files_validate_format`)
- external source handling and ProDiag support (in `BlocksTools`)
- elicitation and utility helpers (`utilities_elicit_user_input`, project/library metadata tools)

All active tools are declared in `TiaPortalMcpServer/Tools/`.

## Runtime model

- Transport: MCP over `stdio`.
- Host process: .NET console app using `Microsoft.Extensions.Hosting`.
- Tool discovery: attribute-based (`[McpServerToolType]`, `[McpServerTool]`).
- Logging: Serilog to stderr and rolling file logs (`TiaPortalMcpServer/logs/`).

## Requirements

- Windows machine.
- Siemens TIA Portal V20 installed and licensed.
- User account in the `TIA Portal Openness Users` group.
- .NET Framework 4.8 runtime/developer pack (for building/running `net48` project).

## Build and run

From repository root:

```bash
dotnet build tia-portal-openness-mcpserver.sln
dotnet run --project TiaPortalMcpServer/TiaPortalMcpServer.csproj
```

## Test

```bash
dotnet test TiaPortalMcpServer.Tests/TiaPortalMcpServer.Tests.csproj
```

Unit tests only:

```bash
dotnet test TiaPortalMcpServer.Tests/TiaPortalMcpServer.Tests.csproj --filter "Category!=Integration"
```

Integration tests only:

```bash
dotnet test TiaPortalMcpServer.Tests/TiaPortalMcpServer.Tests.csproj --filter "Category=Integration"
```

## MCP Inspector

```bash
npx @modelcontextprotocol/inspector dotnet run --project TiaPortalMcpServer/TiaPortalMcpServer.csproj
```

## CLI quick start

The companion CLI is in `cli/` and is intended for npm/`npx` workflows.

```bash
npx @bizarreaster/tia-portal-openness-mcpserver list
npx @bizarreaster/tia-portal-openness-mcpserver install --server-version v1.0.0
npx @bizarreaster/tia-portal-openness-mcpserver update
```

Release discovery variables used by the CLI:

- `GITHUB_TOKEN` (optional): token for authenticated GitHub API access.
- `TIA_MCP_SKILLS_REPO` (optional): default companion skills repository.

See `cli/README.md` for full CLI contract and options.

## Configuration

Server configuration lives in `TiaPortalMcpServer/appsettings.json`.

Current keys include:

- `MCP:Host`, `MCP:Port` (general MCP endpoint settings)
- `FileHandling:MaxFileSizeMB`, `FileHandling:AllowedRootPaths`
- `Serilog` logging levels and sinks

## CI/build constraint

This project depends on Siemens Openness assemblies and cannot be built from public NuGet packages alone on generic hosted runners.

- `Siemens.Engineering.dll` must be resolvable via installed TIA Portal V20 or `TiaPortalLocation`.
- For CI, use a self-hosted Windows runner with TIA Portal V20 installed (or provide the required private assembly bundle).

## Repository layout

```text
.
|- TiaPortalMcpServer/          # MCP server
|- TiaPortalMcpServer.Tests/    # tests
|- cli/                         # release/install CLI
|- Taskfile.yml                 # convenience tasks (inspector/tests)
`- tia-portal-openness-mcpserver.sln
```