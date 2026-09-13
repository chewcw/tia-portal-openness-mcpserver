# Pattern A — Import Tags from CSV → TIA Portal

Import PLC tags from a CSV or Excel file into a tag table in a live TIA Portal project.

## Trigger Phrases

- "Import tags from CSV / Excel"
- "Load tags from a file"
- "Create tags from spreadsheet"
- "Batch import tags"

---

## Step-by-Step Orchestration

### 1. Read the file

Read the source file (CSV or Excel) to extract tag data.

Use available file I/O tools. Fall back to `../scripts/read_csv.md` or `../scripts/read_excel.md` if needed.

---

### 2. Validate columns

Ensure the file contains the required columns for tag import. See `../scripts/validate_columns.md`:

```python
result = validate_columns(
    headers  = <headers from step 1>,
    required = ["tag_name", "data_type"],
    optional = ["logical_address", "comment"],
)
```

If `result["valid"]` is `False` → report missing columns to user and stop.
If warnings exist → note optional fields are absent and continue.

---

### 3. Verify TIA Portal session

Confirm a TIA Portal project is open and accessible. If no session is active, open the project or instruct the user.

---

### 4. Resolve target device and tag table

Identify the target device and tag table.

- If the target table exists → proceed
- If the target table does not exist → offer to create it

---

### 5. Check for duplicates (optional but recommended)

Retrieve existing tags from the target tag table. Compare existing tag names with import rows. Report conflicts and ask:
- Skip duplicates (recommended default)
- Overwrite (if supported)
- Abort

---

### 6. Create tags (loop)

For each row in the import data:

1. Extract tag name, data type, address, and comment
2. Apply validation rules (see Validation Rules below)
3. Create the tag in the target tag table
4. Continue on individual failures — do not abort the loop

```python
created, failed = [], []
mapping = result["mapping"]

for row in rows:
    tag_name = row[mapping["tag_name"]]
    try:
        # Create tag with: name, data_type, address, comment
        created.append(tag_name)
    except Exception as e:
        failed.append({"tag": tag_name, "error": str(e)})
```

---

### 7. Save and report

Save the project after all tags have been processed.

Report summary:

```
✓ Tag import completed

Total rows : 42
Created    : 41
Failed     : 1

Failed:
  • Motor1_Start — Tag already exists in table

Next steps:
  - Review failed tags
  - Compile software
```

---

## Validation Rules for Tags

| Field | Rule |
|---|---|
| Tag name | Max 24 chars; letters, digits, underscore only; first char not a digit |
| Data type | Exact spelling: `Bool` `Byte` `Word` `DWord` `Int` `DInt` `Real` `LReal` `Time` `Char` `String` |
| Logical address | Format: `%I0.0` `%IW2` `%Q0.0` `%QW4` `%M10.0` `%MW20` `%MD100` |

---

## Error Handling

| Situation | Agent Action |
|---|---|
| File not found | Report path; ask user to verify |
| Missing required column | Show found columns; ask for correct mapping |
| No TIA Portal session | Open project or instruct user |
| Device not found | List available devices; ask user to choose |
| Tag table not found | Offer to create it |
| Tag already exists | Skip with warning; continue loop |
| Invalid data type | Suggest correct capitalisation; skip row |
