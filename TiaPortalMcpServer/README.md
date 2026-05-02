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
- **.NET Framework 4.8**
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

The bundle must contain `Siemens.Engineering.dll` somewhere in the archive. Two layouts are accepted:

1. **Preferred** – DLL already nested under a `PublicAPI\V20` directory tree:
   ```text
   <some-root>\PublicAPI\V20\Siemens.Engineering.dll
   ```
   The workflow derives `TiaPortalLocation` from `<some-root>` and passes it to MSBuild automatically.

2. **Flat** – DLL (and sibling assemblies) at the archive root or any other location *not* containing `PublicAPI\V20` in the path:
   ```text
   Siemens.Engineering.dll
   Siemens.Engineering.Hmi.dll
   ...
   ```
   The workflow automatically constructs the expected `PublicAPI\V20` directory tree from the DLL's source directory so that MSBuild can resolve `TiaPortalLocation`.

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
npx @chewcw/tia-portal-openness-mcpserver list
npx @chewcw/tia-portal-openness-mcpserver install --server-version v1.0.0
npx @chewcw/tia-portal-openness-mcpserver update
```

## Tools Available

### Project Management
- `projects_create` - Create a new TIA Portal project (prompts for name/path if missing and client supports elicitation)
- `projects_open` - Open an existing project (prompts for path if missing and client supports elicitation)
- `projects_close` - Close the current project
- `projects_save` - Save the current project

### Device
- `devices_list` - List all devices in the project
- `devices_create` - Add a new device to the project
- `devices_delete` - Remove a device from the project
- `devices_get_attributes` - Get attributes of a device
- `devices_set_attribute` - Set an attribute on a device
- `devices_get_app_id` - Get the application ID of a device
- `devices_set_app_id` - Set the application ID of a device
- `devices_search_catalog` - Search the hardware catalog for devices

### Device Items
- `deviceitems_list` - List device items in a device
- `deviceitems_get_attributes` - Get attributes of a device item
- `deviceitems_plug_move` - Move a device item to a new position
- `deviceitems_copy` - Copy a device item to a new position
- `deviceitems_delete` - Delete a device item from a device
- `catalog_search_device_items` - Search the hardware catalog for device items

### Software/PLC Development
- `software_add_block` - Add a block to a PLC
- `blocks_list` - List all blocks in a PLC
- `software_get_block_hierarchy` - Get the call hierarchy of a block
- `tags_create` - Create PLC tags
- `tags_list` - List PLC tags
- `tags_tagtable_create` - Create tag tables
- `tags_tagtable_list` - List tag tables
- `tags_tagtable_get` - Get a tag table
- `tags_tagtable_delete` - Delete a tag table
- `tags_tagtable_export` - Export a tag table
- `tags_tagtable_open_editor` - Open a tag table in the TIA editor
- `tags_group_create` - Create a tag group
- `tags_group_list` - List tag groups
- `tags_group_find` - Find a tag group
- `tags_group_delete` - Delete a tag group
- `tags_group_system_get` - Get system tag groups
- `tags_constants_user_list` - List user constants
- `tags_constants_system_list` - List system constants

### Blocks (Advanced)
- `blocks_external_source_add` - Add an external source
- `blocks_external_source_list` - List external sources
- `blocks_external_source_generate_all` - Generate all external sources
- `blocks_external_source_generate_with_options` - Generate external source with options
- `blocks_external_source_generate_in_group` - Generate external source in a group
- `blocks_external_source_delete` - Delete an external source
- `blocks_source_generate` - Generate source from blocks
- `blocks_source_generate_from_block` - Generate source from a single block
- `blocks_prodiag_assigned_get` - Get ProDiag assignment for a member
- `blocks_prodiag_assigned_set` - Set ProDiag assignment for a member
- `blocks_prodiag_attributes_get` - Get ProDiag attributes
- `blocks_prodiag_export_csv` - Export ProDiag to CSV
- `blocks_udt_delete` - Delete a UDT
- `blocks_system_types_list` - List system types
- `blocks_user_types_list` - List user types
- `blocks_editor_open_block` - Open a block in the TIA editor
- `blocks_editor_open_type` - Open a type in the TIA editor
- `blocks_fingerprints_get` - Get block fingerprints
- `blocks_ob_priority_set` - Set OB priority

### Utility Operations
- `utilities_get_project_info` - Get project information
- `utilities_list_libraries` - List available libraries
- `utilities_elicit_user_input` - Ask the user for missing information via MCP elicitation

### Compilation
- `compilation_project` - Compile the entire project
- `compilation_software` - Compile PLC software

### File Handling
- `files_read_csv` - Read a CSV file
- `files_read_excel` - Read an Excel file
- `files_list_sheets` - List sheets in an Excel file
- `files_validate_format` - Validate file format
- `files_get_info` - Get file information

### HMI Targets
- `hmi_targets_list` - List HMI targets
- `hmi_targets_get` - Get HMI target details
- `hmi_targets_validate` - Validate HMI target configuration

### Sampling (LLM-assisted)
- `sampling_generate_code` - Generate code with LLM assistance
- `sampling_summarize_project` - Summarize the current project
- `sampling_get_suggestions` - Get suggestions based on context

### Project Management (Extended)
- `projects_get_session_info` - Get current session information
- `projects_open_with_upgrade` - Open a project with upgrade

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
- Full TIA integration requires Windows environment with TIA Portal installed
- Error handling maps TIA exceptions to MCP error codes


## Architecture

- Uses MCP SDK for stdio-based server
- Tools auto-discovered via `[McpServerToolType]` attributes
- Dependency injection with Microsoft.Extensions.Hosting
- **Logging:** Serilog configured to write to stderr (required for MCP protocol compatibility)
- **Project structure is organized by feature and concern:**
   - `Tools/` — MCP tool classes grouped by domain (ProjectManagement, Device, DeviceItem, Software, Tag, Blocks, Compilation, File, HmiTarget, Utility, Sampling, UserInteraction, Test, Hardware)
   - `Services/` — TIA Portal adapters and session management (TiaPortalService, TiaPortalSessionManager, BlocksAdapter, HmiTargetAdapter, FileAdapter)
   - `Models/` — Data transfer objects and result models
   - `Extensions/` — DI and service collection helpers
- **Testing project:** `TiaPortalMcpServer.Tests` contains unit and integration tests

## Limitations

- Requires TIA Portal V20 on Windows
- No authentication beyond MCP protocol
- Some advanced block operations (e.g., WebServer page generation, loadable generation) are not yet exposed as tools

## Contributing

1. Follow the tool naming convention: `[namespace]_[action]` (e.g., `blocks_list`, `devices_create`)
2. Add proper error handling for TIA exceptions
3. Test with real TIA Portal instances
4. Add unit tests for new tools
5. Update this README when adding or modifying tools
