# AGENTS

Purpose
- This file gives guidance for contributors and automation agents working in this repo.
- It focuses on .NET app and MCP server best practices, plus TIA Portal Openness specifics.

Project Snapshot
- Current target: .NET 9 (will be refactored later to .NET Framework).
- Domain: Siemens TIA Portal v20 Openness API/SDK.
- Role: MCP server that exposes safe, structured tooling to clients.

Core Principles
- Prefer safety and explicit boundaries over convenience.
- Keep tool contracts stable; version intentionally when needed.
- Make side effects visible and reversible where possible.
- Fail fast on invalid inputs with clear error messages.

Development Guidelines

Build and Runtime
- Keep the build clean: no warnings, analyzers enabled, nullable reference types on.
- Use deterministic builds and lock dependency versions.
- Avoid platform-specific assumptions; document any required OS or TIA install paths.

Configuration
- Use environment variables for secrets and machine-specific paths.
- Provide a sample config file with safe defaults.
- Validate config at startup and surface actionable errors.

Logging and Diagnostics
- Use structured logging with consistent event names and ids.
- Log external API calls at info level with timing and result status.
- Redact credentials and any proprietary project data from logs.

Error Handling
- Wrap TIA Openness calls with a thin adapter that normalizes errors.
- Avoid swallowing exceptions; translate to MCP errors with clear, user-safe messages.

Threading and Lifetime
- TIA Openness is sensitive to threading and COM lifetime rules.
- Keep API calls on a controlled scheduler/thread if required by the SDK.
- Ensure deterministic disposal of TIA objects; avoid finalizer reliance.

Testing
- Separate pure logic from TIA Openness integration to allow unit tests.
- For integration tests, guard with explicit opt-in and environment checks.
- Use snapshots or recorded fixtures only when data is safe to store.

MCP Server Best Practices

Tool Design
- Keep tools small and composable; avoid large, stateful workflows.
- Define clear schemas with strict input validation and helpful defaults.
- Use descriptive, stable tool names and concise documentation.

Tool Naming Convention
- Follow the standardized `[namespace]_[action]` pattern for all tool names.
- Namespaces represent the primary domain (e.g., `blocks`, `devices`, `software`, `hardware`, `projects`, `utilities`).
- Actions use consistent verbs: `list` (for enumerations), `get` (for retrieval), `set` (for modification), `create`, `delete`, etc.
- Examples: `blocks_list`, `devices_create`, `software_add_tag`, `utilities_get_project_info`, `projects_get_session_info`.
- List operations use the pattern `[namespace]_list` (not `list_[namespace]`) for consistency with other action verbs.
- This convention ensures discoverability by LLMs and clarity for external clients.

Security
- Enforce least privilege for file and project access.
- Validate all file paths and restrict to allowed roots.
- Treat client input as untrusted; reject ambiguous inputs.

Idempotency and Side Effects
- Make read operations idempotent and side-effect free.
- For write operations, expose a dry-run mode when feasible.
- Return a summary of changes and affected artifacts.

Versioning
- Track tool version changes in a changelog.
- Provide compatibility notes when modifying schemas or behaviors.

TIA Portal Openness Notes
- Assume projects can be large; optimize for incremental queries.
- Cache metadata carefully and invalidate on project changes.
- Expose explicit project open/close tools; do not auto-open on request.
- Be explicit about supported TIA versions and SDK dependencies.

Code Style and Structure
- Keep adapters for TIA Openness in a dedicated namespace.
- Keep MCP transport and tool handlers separate from domain logic.
- Document public methods and tool schemas with XML comments.

Contribution Workflow
- Keep changes small and focused.
- Add or update tests when behavior changes.
- Update documentation for any new tool or config change.

Safety Checklist for Agents
- Before changes: read relevant files, understand current behavior.
- During changes: avoid touching generated files or build outputs.
- After changes: run tests if feasible and report results.
