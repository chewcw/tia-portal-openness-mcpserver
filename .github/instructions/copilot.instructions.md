---
description: Guidance for Copilot when working on this repo
# applyTo: '**/*'
---

Project Context
- This repo hosts a .NET-based MCP server that integrates with Siemens TIA Portal v20 Openness.
- Current target is .NET 9; future refactor to .NET Framework is expected.
- TIA Openness is COM-based and sensitive to threading and object lifetime.

General Rules
- Prefer safe defaults and explicit boundaries over convenience.
- Keep changes small and focused.
- Do not edit generated files or build outputs (bin/, obj/).
- Use ASCII unless a file already uses non-ASCII and it is necessary.

Architecture and Code Organization
- Separate MCP transport/tool handlers from domain logic.
- Keep TIA Openness access behind a dedicated adapter layer.
- Do not mix UI/CLI concerns into the MCP server runtime.
- Keep public APIs documented with XML comments.

Configuration and Secrets
- Use environment variables for secrets and machine-specific paths.
- Validate configuration at startup; fail fast with clear errors.
- Never log credentials or proprietary project content.

Logging and Diagnostics
- Use structured logging with consistent event ids and names.
- Log external API calls with timing and status at info level.
- Include correlation ids where available.

Error Handling
- Translate exceptions into MCP errors with clear, user-safe messages.
- Avoid swallowing exceptions; add context and rethrow where appropriate.

Threading and Lifetime
- Respect TIA Openness threading and COM rules.
- Use deterministic disposal for TIA objects; avoid finalizer reliance.

Testing
- Keep logic testable without TIA by isolating adapters.
- Integration tests must be opt-in and check environment prerequisites.

Tool Design (MCP)
- Keep tools small and composable with strict schemas.
- Read operations should be idempotent and side-effect free.
- For write operations, provide a dry-run option when feasible.
- Return explicit summaries of changes.

Review Mindset
- Prioritize correctness, safety, and compatibility with TIA Openness.
- Watch for breaking changes in tool schemas and config defaults.