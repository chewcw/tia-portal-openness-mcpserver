---
name: siemens-tia-portal-integrator
description: >
  Orchestrate data-driven Siemens TIA Portal workflows: read structured data from CSV/Excel files,
  generate STL/AWL code via the siemens-awl-stl-programmer skill, and apply changes to a live TIA
  Portal project. Trigger when the user asks to import, generate, sync, or batch-apply PLC blocks,
  tags, devices, or data blocks from a spreadsheet or CSV source — or to export existing TIA Portal
  project data into a file.
metadata:
  version: "1.1"
---

# Siemens TIA Portal Integrator Skill

## Overview

This skill bridges three layers:

| Layer | Purpose |
|---|---|
| **File I/O** | Read/write CSV and Excel source files |
| **Code Generation** | Produce compilable STL/AWL source via `siemens-awl-stl-programmer` skill |
| **Project Automation** | Apply changes to a live TIA Portal project |

**Prerequisite:** This skill requires TIA Portal automation capability (e.g., TIA Portal Openness API or MCP server). The agent is responsible for discovering and using the appropriate tools for its environment.

**Always** invoke the `siemens-awl-stl-programmer` skill when the workflow involves generating or modifying STL/AWL block source. Validate data completely before applying any changes to the project.

---

## Skill Map

```
siemens-tia-portal-integrator/
│
├── SKILL.md                         ← you are here (index + quick-reference)
│
├── references/
│   ├── scripts/                     ← Helper scripts for file I/O and validation
│   │   ├── read_csv.md
│   │   ├── read_excel.md
│   │   ├── list_sheets.md
│   │   ├── validate_columns.md
│   │   └── write_excel_summary.md
│   ├── patterns/                    ← Workflow patterns
│   │   ├── import-tags-csv.md       ← Pattern A: Import tags from CSV/Excel
│   │   ├── import-devices-excel.md  ← Pattern B: Import devices from Excel
│   │   ├── generate-blocks-excel.md ← Pattern C: Generate blocks from Excel spec
│   │   └── export-blocks-excel.md   ← Pattern D: Export blocks to Excel
│   └── examples/                    ← Full conversation examples
│       ├── example-tag-import.md
│       ├── example-device-import.md
│       ├── example-block-generation.md
│       └── example-block-export.md
```

---

## Validation Rules

Apply these rules before creating any data in TIA Portal:

**Tag names**
- Max 24 characters; first character must be a letter or underscore
- Allowed: letters, digits, underscore — no spaces or special characters

**Data types**
- `Bool` `Byte` `Word` `DWord` `Int` `DInt` `Real` `LReal` `Time` `Char` `String`
- Capitalisation is exact — use these spellings

**Logical addresses**
- Inputs: `%I<byte>.<bit>` or `%IW<byte>`
- Outputs: `%Q<byte>.<bit>` or `%QW<byte>`
- Memory: `%M<byte>.<bit>` or `%MW<byte>` or `%MD<byte>`

**Order numbers**
- Example: `6ES7 516-3AN02-0AB0`
- Pattern: `[Family 4 chars][Space][Article]-[SubArticle]-[Variant]`

**Block names**
- Max 24 characters; no special characters except underscore; unique within the device

---

## Workflow Patterns

Choose the pattern that matches the user's request:

| Pattern | File | Trigger |
|---|---|---|
| A — Import tags | [references/patterns/import-tags-csv.md](references/patterns/import-tags-csv.md) | "Import tags from CSV/Excel" |
| B — Import devices | [references/patterns/import-devices-excel.md](references/patterns/import-devices-excel.md) | "Import devices from Excel" |
| C — Generate blocks | [references/patterns/generate-blocks-excel.md](references/patterns/generate-blocks-excel.md) | "Generate blocks from spreadsheet" |
| D — Export blocks | [references/patterns/export-blocks-excel.md](references/patterns/export-blocks-excel.md) | "Export blocks to Excel" |

---

## General Workflow Checklist

For any import/generate workflow, follow this sequence:

- [ ] **Step 1: Read source file** — Read the CSV or Excel file containing the data
- [ ] **Step 2: Validate columns** — Ensure required columns exist; map aliases to canonical names
- [ ] **Step 3: Verify TIA Portal session** — Confirm a project is open and accessible
- [ ] **Step 4: Resolve target** — Identify the target device, tag table, or block destination
- [ ] **Step 5: Pre-validate** — Check catalog validity, duplicates, naming rules
- [ ] **Step 6: Apply changes** — Create/modify data in the project; continue on single failures
- [ ] **Step 7: Save** — Save the project after every successful batch
- [ ] **Step 8: Compile** — Compile software after block changes
- [ ] **Step 9: Report** — Summarize created/failed/skipped with details

---

## General Best Practices

1. **Verify session first** — confirm TIA Portal is accessible before any write operation.
2. **Validate before writing** — run column validation and catalog checks before creating any data.
3. **Ask on ambiguity** — if a requirement or target (device, sheet, table, mapping, action) is unclear or missing, ask the user before proceeding; do not assume.
4. **One operation per item** — process items individually; no bulk payloads.
5. **Save after each batch** — save the project after every successful write batch.
6. **Compile after block changes** — compile software after uploading block sources.
7. **Delegate code generation** — never write AWL inline; invoke `siemens-awl-stl-programmer` skill.
8. **Continue on single failures** — collect all failures and report at the end, do not abort early.
