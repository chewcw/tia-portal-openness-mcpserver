# TIA Portal MCP Server CLI

CLI to install and manage [TIA Portal MCP Server](https://github.com/bizarreaster/tia-portal-openness-mcpserver) releases.

## Requirements

- Node.js >= 20
- Windows machine with Siemens TIA Portal V20 installed (for the server)

## Installation

```bash
npm install -g @bizarreaster/tia-portal-openness-mcpserver
```

Or use directly with npx:

```bash
npx @bizarreaster/tia-portal-openness-mcpserver <command>
```

## Commands

### `install`

Install latest or selected server release.

```bash
tia-mcp install
tia-mcp install --server-version v1.0.0
tia-mcp install --install-dir /custom/path
tia-mcp install --yes
```

### `download`

Download a release asset without activating install.

```bash
tia-mcp download --server-version v1.0.0
```

### `list`

List available GitHub releases.

```bash
tia-mcp list
```

### `check`

Run local prerequisite checks.

```bash
tia-mcp check
```

### `update`

Update existing install with rollback safety.

```bash
tia-mcp update
```

### `run`

Launch currently installed server.

```bash
tia-mcp run
```

### `skills`

Manage companion skills.

```bash
tia-mcp skills install --skills <name> --agent-type opencode
```

## Global Options

| Option | Description |
| --- | --- |
| `--server-version <tag>` | Explicit server release tag |
| `--install-dir <path>` | Override default install root |
| `--skills-repo <url>` | Companion skills repository URL |
| `--skills-ref <ref>` | Tag, branch, or commit for companion skills |
| `--skills <name[,name...]>` | Sync specific skills |
| `--all` | Sync all available skills |
| `--yes` | Accept defaults for interactive prompts |
| `--non-interactive` | Disable prompts, require explicit flags |
| `--verbose` | Enable verbose diagnostics output |
| `--help` | Show help |
| `--version` | Show CLI version |

## Interactive Behavior

- Interactive mode is enabled by default
- During install, the CLI prompts for install directory, optional companion skills sync, and skills repo/ref
- Non-interactive mode (`--yes` / `--non-interactive`) fails fast when required values are missing

## Installation Location

Default: per-user AppData at `%APPDATA%/TiaPortalMcpServerCli/server`

## Environment Variables

- `GITHUB_TOKEN` (optional): Token for authenticated GitHub API access
- `TIA_MCP_SKILLS_REPO` (optional): Default companion skills repository URL

## License

MIT
