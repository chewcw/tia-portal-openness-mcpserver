# Plan: Refactor "tools" into a dedicated program under `Tools/`

## Short summary
Move the tool class implementations currently defined inline in `TiaPortalMcpServer/Program.cs` into dedicated files under `TiaPortalMcpServer/Tools/` (keep the same namespace to preserve DI/discovery). This is low-risk and keeps runtime behavior unchanged.

---

## Files & symbols to move
- `TiaPortalMcpServer/Program.cs` — remove tool class definitions **lines 29–170** (these contain the inline tool implementations to relocate). Reason: separate tool implementation code from host startup.
- Create new files under `TiaPortalMcpServer/Tools/` (one file per tool class) — preserve the existing namespace so discovery and DI remain unchanged.

---

## Migration approach
Move tool implementations into a `Tools` subfolder inside the same project (single assembly).

---

## Ordered migration checklist
1. Move each tool-class into its own file (preserve namespace)
   - Action: Create one file per tool in `TiaPortalMcpServer/Tools/` and copy the class implementation from `Program.cs`. The name of the tool is like ProjectManagementTools.cs, HardwareTools.cs, etc..
   - Edit: remove corresponding class definitions from `TiaPortalMcpServer/Program.cs` (lines **29–170**).
   - Files/symbols: `TiaPortalMcpServer/Program.cs` (29–170) → new files in `TiaPortalMcpServer/Tools/`.

2.  Add `ToolsModule` registrar for organization
   - Action: Add `TiaPortalMcpServer/Tools/ToolsModule.cs` with a small `AddTiaTools(this IServiceCollection)` extension.
   - Files/symbols: new `ToolsModule` class; no behavior change.

3. Tidy `Program.cs` startup
   - Action: Ensure startup remains minimal and still registers services the same way.
   - Edit: remove only the moved class definitions; keep DI calls unchanged.

4. Update docs
   - Action: Update `README.md` and `AGENTS.md` to reference the `Tools/` folder.

5. Build & test
   - Action: `dotnet build` and `dotnet test` for the solution.

---

## Tests & runtime issues to update
- Expected breakages: none if the namespace and assembly remain unchanged.
- If you change namespace or move to a new assembly (Option B), then:
  - Runtime symptom: tools disappear from MCP discovery.
  - Fix: update startup to register types from the new assembly (and add project references where needed).
  - Tests to update: any tests that reference moved types or assume `Program.cs` contains those types — update using statements or project references.

---

## Final 3–6 step implementation plan (actionable)
1. Create `TiaPortalMcpServer/Tools/` and add one `.cs` file per tool class (copy class bodies, keep namespace). ✅
2. Remove the moved classes from `TiaPortalMcpServer/Program.cs` (delete lines 29–170). ✅
3. (Optional) Add `ToolsModule.cs` for organizational registration.
4. Update `README.md` / `AGENTS.md` to reference `Tools/`.
5. Run `dotnet build` and `dotnet test`, fix any compilation issues, then commit and open a PR.

---

> Notes: Keep the original namespace to avoid any discovery/DI regressions. Approach A is recommended for speed and safety; choose Option B only if you need process/assembly separation.


---

*End of plan — ready for refinement.*
