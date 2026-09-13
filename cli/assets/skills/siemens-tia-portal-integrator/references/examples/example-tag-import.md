# Example — Batch Tag Import from CSV

Demonstrates Pattern A end-to-end.

---

## Conversation

**User:**
> Import tags from `io_list.csv` into `PLC_1` GlobalTags table.

---

**Agent step 1 — Read file**

Read the CSV file to extract tag data.

Result:
```
42 rows — headers: [Name, DataType, Address, Comment]
```

---

**Agent step 2 — Validate columns**

```python
validate_columns(
    headers  = ["Name", "DataType", "Address", "Comment"],
    required = ["tag_name", "data_type"],
    optional = ["logical_address", "comment"],
)
```

Result:
```json
{
  "mapping": {
    "tag_name":        "Name",
    "data_type":       "DataType",
    "logical_address": "Address",
    "comment":         "Comment"
  },
  "missing":  [],
  "valid":    true
}
```

---

**Agent step 3 — Verify session**

Verify TIA Portal session is active.

Result: Session active, project "Plant_A" open.

---

**Agent step 4 — Confirm tag table**

List tag tables for device PLC_1.

Result: `["GlobalTags", "Safety_Tags"]` — GlobalTags exists ✓

---

**Agent step 5 — Check for duplicates**

List existing tags in GlobalTags.

Result: 12 existing tags — none conflict with the 42 import rows ✓

---

**Agent step 6 — Create tags (loop × 42)**

Create each tag in the target tag table.

Row 27: Motor1_Start — Tag already exists in table → skipped with warning.

---

**Agent step 7 — Save**

Save the project.

---

**Agent response:**

```
✓ Tag import completed

Total rows : 42
Created    : 41
Failed     : 1

Failed:
  • Motor1_Start (row 27) — Tag already exists in table

Recommendation:
  - Remove duplicate row from io_list.csv

Next steps:
  - Compile software
```
