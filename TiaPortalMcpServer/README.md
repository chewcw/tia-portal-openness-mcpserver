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

## Tools Available

### Test Tools
- `hello_world`: Returns a greeting from TIA MCP Server

### Project Management
- `create_project(name, path)`: Create a new TIA project
- `open_project(path)`: Open existing project
- `save_project(projectId)`: Save current project
- `close_project(projectId)`: Close project

### Hardware Configuration
- `add_device(projectId, deviceType, name, orderNumber)`: Add hardware device
- `list_devices(projectId)`: List devices in project
- `configure_device(deviceId, parameters)`: Set device parameters

### Software/PLC Tools
- `add_block(deviceId, blockType, name)`: Add PLC block
- `list_blocks(deviceId)`: List blocks in device
- `add_tag(parentId, name, dataType)`: Add variable/tag

### Compilation
- `compile_project(projectId)`: Compile entire project
- `compile_software(deviceId)`: Compile PLC software

### Utilities
- `get_project_info(projectId)`: Get project metadata
- `list_available_libraries()`: List TIA libraries

## Usage with MCP Inspector

Test the server using MCP Inspector:
```bash
npx @modelcontextprotocol/inspector dotnet run
```

This will allow you to interactively test all available tools.

## Development Notes

- Built with .NET 9.0 and ModelContextProtocol SDK
- TIA Openness assemblies are referenced but implementation is placeholder
- Full TIA integration requires Windows environment with TIA Portal installed
- Error handling maps TIA exceptions to MCP error codes

## Architecture

- Uses MCP SDK for stdio-based server
- Tools auto-discovered via `[McpServerToolType]` attributes
- Dependency injection with Microsoft.Extensions.Hosting
- Logging configured to stderr

## Limitations

- Currently placeholder implementations (TODO comments indicate TIA API calls needed)
- Requires TIA Portal V20 on Windows
- No authentication beyond MCP protocol

## Contributing

1. Implement TODO placeholders with actual TIA Openness API calls
2. Add proper error handling for TIA exceptions
3. Test with real TIA Portal instances
4. Add unit tests for TIA operations