# Pattern D — Export TIA Portal Blocks → Excel

Extract block inventory and interface details from a live TIA Portal project and write them
to a formatted Excel file for documentation or review.

## Trigger Phrases

- "Export block list to Excel"
- "Give me an Excel overview of all blocks"
- "Document the PLC software in a spreadsheet"
- "Export block interfaces / signatures"

---

## Step-by-Step Orchestration

### 1. Verify session and resolve device

Confirm a TIA Portal project is open and accessible.

Ask user which device to export if not specified.

---

### 2. List all blocks

Retrieve the list of all PLC blocks in the target device. Each block should include `name` and `type` (OB / FB / FC / DB).

---

### 3. Extract source for each block (optional, for interface detail)

For each block where you need variable signatures:

1. Generate source text from the block
2. Parse the source to extract `VAR_INPUT`, `VAR_OUTPUT`, `VAR_IN_OUT`, `VAR` sections

**Regex for variable sections:**

```python
import re

def extract_var_section(awl_source: str, section: str) -> list[str]:
    """
    section: one of VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR, VAR_STAT
    Returns list of "<name> : <type>" strings.
    """
    pattern = rf"{section}\s*(.*?)END_VAR"
    match = re.search(pattern, awl_source, re.DOTALL | re.IGNORECASE)
    if not match:
        return []
    lines = [ln.strip().rstrip(";") for ln in match.group(1).splitlines() if ln.strip()]
    return [ln for ln in lines if ":" in ln]
```

---

### 4. Build summary rows

```python
rows = []
for block in blocks:
    awl    = generate_source_from_block(deviceName, block["name"], block["type"])
    inputs = extract_var_section(awl, "VAR_INPUT")
    outputs= extract_var_section(awl, "VAR_OUTPUT")
    rows.append({
        "BlockName"   : block["name"],
        "BlockType"   : block["type"],
        "InputCount"  : len(inputs),
        "OutputCount" : len(outputs),
        "Inputs"      : ", ".join(inputs),
        "Outputs"     : ", ".join(outputs),
    })
```

---

### 5. Write to Excel

Use `../scripts/write_excel_summary.md`:

```python
write_summary(
    rows     = rows,
    headers  = ["BlockName", "BlockType", "InputCount", "OutputCount", "Inputs", "Outputs"],
    filepath = "/tmp/<ProjectName>_Block_Inventory.xlsx",
)
```

Present the file path to the user for download.

---

### 6. Report

```
✓ Export completed

Device    : PLC_1
Blocks    : 18  (3 OB, 7 FB, 5 FC, 3 DB)
File      : /tmp/Plant_A_Block_Inventory.xlsx

Note: DB blocks do not have VAR_INPUT/OUTPUT sections — InputCount/OutputCount will be 0.
```

---

## Error Handling

| Situation | Agent Action |
|---|---|
| No TIA Portal session | Open project or instruct user |
| Source generation fails for a block | Log; insert empty row; continue loop |
| openpyxl not installed | Install required package |
| Output directory not writable | Try `/tmp/` as fallback |
