# TIA Portal MCP Server CLI

This directory contains the TypeScript CLI used to install and manage TIA Portal MCP Server releases.

## Phase 1 Contract

### Primary goals
- Install server binaries from GitHub Releases.
- Support interactive onboarding by default.
- Optionally sync companion skills from an external public GitHub repository.

### Command surface
- install: Install latest or selected server release.
- download: Download a release asset without activating install.
- list: List available GitHub releases.
- check: Run local prerequisite checks.
- update: Update existing install with rollback safety.
- run: Launch currently installed server.
- skills: Manage companion skills checkout/sync.

### Global options
- --server-version <tag>: Explicit server release tag.
- --install-dir <path>: Override default install root.
- --skills-repo <url>: Companion skills repository URL.
- --skills-ref <ref>: Tag, branch, or commit for companion skills.
- --skills <name[,name...]>: Sync specific skills under the companion namespace.
- --all: Sync all available skills in the companion namespace.
- --yes: Accept defaults for interactive prompts.
- --non-interactive: Disable prompts and require explicit flags for required values.
- --verbose: Enable verbose diagnostics output.

### Interactive behavior contract
- Interactive mode is enabled by default.
- During install, the CLI prompts for:
  - install directory (default per-user AppData)
  - optional companion skills sync
  - skills repo/ref when needed
- Non-interactive mode must fail fast when required values are missing.

### Installation location
- Default: per-user AppData at:
  - %APPDATA%/TiaPortalMcpServerCli/server

### Update and rollback behavior contract
- Updates are in-place and transactional.
- Before activating a new install, the previous install is moved to a rollback cache.
- If extraction, validation, or activation fails, rollback restores the previous install.

### Version mapping contract
- CLI and server use independent semantic versioning.
- Companion skills default to the same tag as selected server release.
- Users may override skills ref explicitly.

### Metadata contract
- Install metadata and skills metadata are stored under:
  - %APPDATA%/TiaPortalMcpServerCli/state
- Metadata includes installed server version, active install path, update timestamps, and rollback path.
