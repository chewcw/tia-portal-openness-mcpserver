# TIA Portal MCP Server CLI

CLI to install and manage [TIA Portal MCP Server](https://github.com/chewcw/tia-portal-openness-mcpserver) releases.

## Requirements

- Node.js >= 20
- Windows machine with Siemens TIA Portal V20 installed (for the server)

## Installation

```bash
npm install -g @chewcw/tia-portal-openness-mcpserver
```

Or use directly with npx:

```bash
npx @chewcw/tia-portal-openness-mcpserver <command>
```

## Commands

### `install`

Install latest or selected server release.

```bash
tia-portal-openness-mcpserver install
tia-portal-openness-mcpserver install --server-version v1.0.0
tia-portal-openness-mcpserver install --install-dir /custom/path
```

### `skills`

Manage companion skills.

```bash
tia-portal-openness-mcpserver skills install --skills <name[,name...]> --agent-type <type>
```

Available skills:

| Skill | Description |
| --- | --- |
| `siemens-awl-stl-programmer` | Generate Siemens S7 STL/AWL PLC programs |
| `siemens-tia-portal-integrator` | Orchestrate TIA Portal data-driven workflows |

Supported agent types: `opencode`, `claude`, `cursor`, `generic`.

## Global Options

| Option | Description |
| --- | --- |
| `--server-version <tag>` | Explicit server release tag |
| `--install-dir <path>` | Override default install root |
| `--skills <name[,name...]>` | Comma-separated list of skills to install |
| `--agent-type <type>` | Target agent type for skills: `opencode`, `claude`, `cursor`, or `generic` |
| `--help` | Show help |
| `--version` | Show CLI version |

## Installation Location

Default: per-user AppData at `%APPDATA%/TiaPortalMcpServerCli/server`

## Environment Variables

| Variable | Description |
| --- | --- |
| `GITHUB_TOKEN` | Token for authenticated GitHub API access |
| `TIA_MCP_INSTALL_DIR` | Override default install directory |
| `TIA_MCP_AGENT_TYPE` | Override agent type detection (`opencode`, `claude`, `cursor`, `generic`) |
| `TIA_MCP_SKILLS_PATH` | Override skills installation path |

The CLI also auto-detects the agent type from runtime environment variables (`OPENCODE_SESSION_ID`, `CLAUDE_AGENT_METADATA_FILE`, `CURSOR_SESSION_ID`).

## License

MIT
