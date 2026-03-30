# TIA Portal Openness MCP Server

An MCP (Model Context Protocol) server that exposes TIA Portal Openness API functionality, allowing LLMs to programmatically interact with Siemens TIA Portal for PLC programming and automation engineering.

## Overview

This server provides tools for:
- Project management (create, open, save, close projects)
- Hardware configuration (add devices, configure parameters)
- Software/PLC development (add blocks, tags, compilation)
- Utility operations (project info, library listing)

## Prerequisites

- **TIA Portal V20** installed and licensed on the host machine
- **.NET Framework 4.7.2** (or compatible .NET runtime)
- The server runs on stdio transport for MCP communication
- Default network endpoint (if a network transport is enabled): `127.0.0.1:3000` — override with environment variables `MCP_HOST` / `MCP_PORT` or configuration keys `MCP:Host` / `MCP:Port`

### CI Build Requirement

The server project cannot be built from nuget.org packages alone. The Siemens TIA Openness NuGet package only wires up references; it still expects `Siemens.Engineering.dll` to be available through `TiaPortalLocation` or a matching TIA Portal registry entry.

The release workflow uses `windows-latest` and stages a private zip bundle from a GitHub Release asset containing the TIA Portal V20 `PublicAPI\V20` assemblies.

Configure these repository variables:

- `TIA_PUBLICAPI_RELEASE_REPOSITORY`: artifact repository in `owner/repo` format
- `TIA_PUBLICAPI_RELEASE_TAG`: release tag that contains the zip asset
- `TIA_PUBLICAPI_RELEASE_ASSET_NAME`: zip asset name, for example `PortalV20-PublicAPI-V20.zip`

Configure this repository secret:

- `TIA_PUBLICAPI_GITHUB_TOKEN`: token with read access to the artifact repository release assets

The bundle must contain a directory that resolves to this shape after extraction:

```text
<some-root>\PublicAPI\V20\Siemens.Engineering.dll
```

The workflow derives `TiaPortalLocation` from that extracted layout and passes it to MSBuild automatically.

## Installation

1. Ensure TIA Portal V20 is installed at the default location
2. Build the project:
   ```bash
   dotnet build
   ```
3. Run the server:
   ```bash
   dotnet run
   ```

### CLI-Based Installation

The repository also contains a TypeScript CLI under `../cli` for release-based installation and updates.

Examples:

```bash
npx @tiaportal/mcp-server-cli list
npx @tiaportal/mcp-server-cli install --server-version v1.0.0
npx @tiaportal/mcp-server-cli update
```

Set `TIA_MCP_GITHUB_REPOSITORY` to the GitHub repository in `owner/repo` format before using release-based commands.

## Tools Available

### Project Management
- `projects_create` - Create a new TIA Portal project (prompts for name/path if missing and client supports elicitation)
- `projects_open` - Open an existing project (prompts for path if missing and client supports elicitation)
- `projects_close` - Close the current project
- `projects_save` - Save the current project

### Device
- `devices_list` - List all devices in the project
- `devices_create` - Add a new device to the project (prompts for missing fields and project path if needed)
- `devices_delete` - Remove a device from the project (prompts for device name and project path if needed)

### Device Items
- `deviceitems_list` - List device items in a device
- `deviceitems_get_attributes` - Get attributes of a device item
- `deviceitems_plug_new` - Plug a new device item into a device
- `deviceitems_plug_move` - Move a device item to a new position
- `deviceitems_copy` - Copy a device item to a new position
- `deviceitems_delete` - Delete a device item from a device

### Software/PLC Development
- `blocks_create_db` - Create a new data block
- `blocks_create_fb` - Create a new function block
- `blocks_create_fc` - Create a new function
- `blocks_compile` - Compile blocks
- `tags_create` - Create PLC tags (prompts for missing fields and project path if needed)
- `tags_list` - List PLC tags
- `tags_tagtable_create` - Create tag tables (prompts for missing fields and project path if needed)

### Utility Operations
- `projects_info` - Get project information
- `libraries_list` - List available libraries
- `utilities_elicit_user_input` - Ask the user for missing information via MCP Apps/elicitation

## Usage with MCP Inspector

Test the server using MCP Inspector:
```bash
npx @modelcontextprotocol/inspector dotnet run
```

This will allow you to interactively test all available tools.

## Development Notes

- Built with .NET Framework 4.8 and ModelContextProtocol SDK
- Logging provided by Serilog (configured via appsettings.json)
- All logs written to stderr to maintain MCP protocol integrity on stdout
- TIA Openness assemblies are referenced but implementation is placeholder
- Full TIA integration requires Windows environment with TIA Portal installed
- Error handling maps TIA exceptions to MCP error codes


## Architecture

- Uses MCP SDK for stdio-based server
- Tools auto-discovered via `[McpServerToolType]` attributes
- Dependency injection with Microsoft.Extensions.Hosting
- **Logging:** Serilog configured to write to stderr (required for MCP protocol compatibility)
- **Project structure is organized by feature and concern:**
   - `ProjectManagement/`, `HardwareConfiguration/`, `SoftwareDevelopment/`, `UtilityOperations/` for main features
   - `Models/`, `Services/`, `Controllers/`, `Interfaces/` for separation of concerns
- **Testing project:** `TiaPortalMcpServer.Tests` contains unit and integration tests

## Limitations

- Currently placeholder implementations (TODO comments indicate TIA API calls needed)
- Requires TIA Portal V20 on Windows
- No authentication beyond MCP protocol

## Contributing

1. Implement TODO placeholders with actual TIA Openness API calls
2. Add proper error handling for TIA exceptions
3. Test with real TIA Portal instances
4. Add unit tests for TIA operations
