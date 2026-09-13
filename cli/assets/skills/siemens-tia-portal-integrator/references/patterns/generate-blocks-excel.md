# Pattern C — Generate STL Blocks from Excel → TIA Portal

Read a block specification table from Excel, delegate AWL/STL code generation to the
`siemens-awl-stl-programmer` skill, upload each source file, compile, and save.

## Trigger Phrases

- "Generate function blocks from Excel"
- "Create PLC blocks from spreadsheet spec"
- "Build FB/FC from specification table"
- "Auto-generate blocks from file"

---

## Expected Excel Schema

First row must be a header row. Required and optional columns:

| Column (canonical) | Aliases | Required |
|---|---|---|
| `block_name` | BlockName, Block, FBName, FCName | ✓ |
| `block_type` | BlockType, Type, Kind | ✓ |
| `inputs` | Inputs, VarInput, IN | optional |
| `outputs` | Outputs, VarOutput, OUT | optional |
| `static_vars` | StaticVars, Static, VarStatic | optional |
| `logic` | LogicDescription, Logic, Description | optional |
| `db_name` | DBName, InstanceDB | optional (FB only) |

**Example row:**

| BlockName | BlockType | Inputs | Outputs | StaticVars | LogicDescription |
|---|---|---|---|---|---|
| PumpControl | FB | RunCmd:BOOL, Fault:BOOL | PumpOut:BOOL, Alarm:BOOL | RunLatch:BOOL | Latch on RunCmd, reset on Fault |

---

## Step-by-Step Orchestration

### 1. Read and validate the sheet

Read the Excel file and list worksheets if multiple exist.

```python
result = validate_columns(
    headers  = <headers>,
    required = ["block_name", "block_type"],
    optional = ["inputs", "outputs", "static_vars", "logic", "db_name"],
)
```

---

### 2. Verify TIA Portal session and identify device

Confirm a TIA Portal project is open. Identify the target device for block creation.

---

### 3. For each row: generate STL code

Construct a natural-language specification from the row columns:

```python
spec = f"""
Block name : {row['BlockName']}
Block type : {row['BlockType']}
Inputs     : {row.get('Inputs', 'none')}
Outputs    : {row.get('Outputs', 'none')}
Static vars: {row.get('StaticVars', 'none')}
Logic      : {row.get('LogicDescription', 'implement basic structure')}
"""
```

Pass `spec` to the **siemens-awl-stl-programmer** skill.
Receive back complete AWL source text.

---

### 4. Write AWL source to a temp file

Save the generated AWL source to a temporary file:

```python
source_path = f"/tmp/{row['BlockName']}.awl"
with open(source_path, "w") as f:
    f.write(awl_source)
```

---

### 5. Upload source to TIA Portal

Add the external source file to the project and compile it into a block.

If compilation returns errors → present compiler output to user; do not proceed to save.

---

### 6. Compile and save

After all blocks are uploaded:

1. Compile the software for the target device
2. Save the project

---

### 7. Report

```
✓ Block generation completed

Blocks processed : 3
Generated        : 3
Compile warnings : 2  (see details below)
Compile errors   : 0

  • PumpControl  (FB)  — ✓ compiled
  • ValveControl (FB)  — ✓ compiled, 2 warnings
  • ScaleFlow    (FC)  — ✓ compiled

Next steps:
  - Review compiler warnings for ValveControl
  - Download to PLC
```

---

## Error Handling

| Situation | Agent Action |
|---|---|
| Missing block_name or block_type column | Stop; report to user |
| STL generation returns empty | Retry with more detailed spec; report if still failing |
| External source upload fails | Log; skip block; continue loop |
| Block compilation error | Show output; do not save; ask user to review |
| No TIA Portal session | Open project or instruct user |
