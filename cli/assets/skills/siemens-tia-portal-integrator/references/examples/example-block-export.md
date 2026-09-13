# Example — Export Block Inventory to Excel

Demonstrates Pattern D end-to-end.

---

## Conversation

**User:**
> Give me an Excel overview of all blocks in `PLC_1`.

---

**Agent step 1 — Verify session**

Verify TIA Portal session is active.

Result: project "Plant_A" open, session active ✓

---

**Agent step 2 — List blocks**

List all PLC blocks in PLC_1.

Result: 9 blocks

| Name | Type |
|---|---|
| OB1 | OB |
| OB100 | OB |
| FB10 | FB |
| FB11 | FB |
| FC20 | FC |
| FC21 | FC |
| DB100 | DB |
| DB101 | DB |
| DB200 | DB |

---

**Agent step 3 — Extract source and parse interfaces (× 9)**

For each non-DB block:

1. Generate source text from the block
2. Parse `VAR_INPUT` and `VAR_OUTPUT` sections

DB blocks are noted with `InputCount=0, OutputCount=0` — no VAR_INPUT/OUTPUT in data blocks.

---

**Agent step 4 — Build summary rows**

```python
rows = [
    {"BlockName":"OB1",   "BlockType":"OB","InputCount":0,"OutputCount":0,"Inputs":"—","Outputs":"—"},
    {"BlockName":"OB100", "BlockType":"OB","InputCount":0,"OutputCount":0,"Inputs":"—","Outputs":"—"},
    {"BlockName":"FB10",  "BlockType":"FB","InputCount":3,"OutputCount":2,
     "Inputs":"Enable:BOOL, SetPoint:REAL, Reset:BOOL",
     "Outputs":"Active:BOOL, Fault:BOOL"},
    {"BlockName":"FB11",  "BlockType":"FB","InputCount":2,"OutputCount":1,
     "Inputs":"RunCmd:BOOL, Speed:REAL",
     "Outputs":"Running:BOOL"},
    {"BlockName":"FC20",  "BlockType":"FC","InputCount":3,"OutputCount":1,
     "Inputs":"RawVal:INT, HiLim:REAL, LoLim:REAL",
     "Outputs":"ScaledVal:REAL"},
    {"BlockName":"FC21",  "BlockType":"FC","InputCount":2,"OutputCount":1,
     "Inputs":"Val1:REAL, Val2:REAL",
     "Outputs":"Result:REAL"},
    {"BlockName":"DB100", "BlockType":"DB","InputCount":0,"OutputCount":0,"Inputs":"—","Outputs":"—"},
    {"BlockName":"DB101", "BlockType":"DB","InputCount":0,"OutputCount":0,"Inputs":"—","Outputs":"—"},
    {"BlockName":"DB200", "BlockType":"DB","InputCount":0,"OutputCount":0,"Inputs":"—","Outputs":"—"},
]
```

---

**Agent step 5 — Write Excel**

Write the summary rows to an Excel file.

---

**Agent response:**

```
✓ Export completed

Device : PLC_1
Blocks : 9  (2 OB, 2 FB, 2 FC, 3 DB)
File   : /tmp/Plant_A_Block_Inventory.xlsx

Note: DB blocks do not expose VAR_INPUT/OUTPUT — InputCount and OutputCount are 0.
```

File presented to user for download.
