# Pattern B — Import Devices from Excel → TIA Portal

Import hardware devices from an Excel file, validate each order number against the hardware
catalog, and create devices in a live TIA Portal project.

## Trigger Phrases

- "Import devices from Excel / XLSX"
- "Load hardware list from spreadsheet"
- "Create devices from file"
- "Batch import hardware"

---

## Step-by-Step Orchestration

### 1. List and pick sheet

List available worksheets in the Excel file.

- Single sheet → use automatically
- Multiple sheets → ask user which sheet contains device data

---

### 2. Read the sheet

Read the selected worksheet to extract device data.

---

### 3. Validate columns

```python
result = validate_columns(
    headers  = <headers from step 2>,
    required = ["device_name", "order_number"],
    optional = ["position", "comment"],
)
```

If `result["valid"]` is `False` → stop and report missing columns.

---

### 4. Pre-validate order numbers

Before creating any device, validate all order numbers against the hardware catalog.

```python
valid_rows   = []
invalid_rows = []

for row in rows:
    catalog_result = search_hardware_catalog(query=row[order_number_col])
    if catalog_result["count"] > 0:
        valid_rows.append({**row, "typeIdentifier": catalog_result["results"][0]["typeIdentifier"]})
    else:
        invalid_rows.append(row)
```

If any invalid rows → report to user and ask whether to continue with valid rows only.

---

### 5. Verify TIA Portal session

Confirm a TIA Portal project is open and accessible.

---

### 6. Create devices (loop)

For each valid row:

1. Extract device name, order number, and position
2. Create the device in the project
3. Continue on individual failures — do not abort the loop

```python
created, failed = [], []

for row in valid_rows:
    try:
        # Create device with: name, order_number, position
        created.append(row[device_name_col])
    except Exception as e:
        failed.append({"device": row[device_name_col], "error": str(e)})
```

---

### 7. Save and report

Save the project after all devices have been created.

```
✓ Device import completed

Total rows       : 8
Valid (catalog)  : 7
Created          : 7
Failed           : 0
Skipped (invalid): 1  — Row 6: order number not found in catalog

Created devices:
  • PLC_1        CPU 1516-3 PN/DP   (6ES7 516-3AN02-0AB0)
  • HMI_1        Comfort Panel 15"  (6AV2 124-0MC01-0AX0)
  …

Next steps:
  - Configure Profinet/Profibus network topology
  - Add device modules
  - Compile hardware
```

---

## Error Handling

| Situation | Agent Action |
|---|---|
| Sheet not found | List available sheets; ask user to choose |
| Missing required column | Show found columns; ask user to map |
| Order number not in catalog | Warn; ask whether to skip or abort |
| Duplicate device name | Warn; offer auto-rename or skip |
| No TIA Portal session | Open project or instruct user |
| Device creation fails | Log error; continue loop |
