# Example — Device Import with Catalog Validation

Demonstrates Pattern B end-to-end.

---

## Conversation

**User:**
> Import hardware from `hardware_list.xlsx`. Validate against catalog first.

---

**Agent step 1 — List sheets**

List worksheets in the Excel file.

Result: `["Devices"]` — single sheet, used automatically.

---

**Agent step 2 — Read sheet**

Read the Devices worksheet.

Result: 8 rows — headers: `[DeviceName, OrderNumber, Position, Comment]`

---

**Agent step 3 — Validate columns**

```python
validate_columns(
    headers  = ["DeviceName", "OrderNumber", "Position", "Comment"],
    required = ["device_name", "order_number"],
    optional = ["position", "comment"],
)
```

Result: all columns resolved ✓

---

**Agent step 4 — Pre-validate order numbers (× 8)**

Search hardware catalog for each order number:

```
6ES7 516-3AN02-0AB0  → found: CPU 1516-3 PN/DP  ✓
6AV2 124-0MC01-0AX0  → found: Comfort Panel 15"  ✓
6ES7 193-6BP20-0BA0  → found: ET200SP Interface  ✓
6ES7 134-6HD00-0BA1  → found: Digital Input       ✓
6GK5 208-0BA00-2AC2  → found: Scalance X208       ✓
6ES7 155-6AU01-0BN0  → found: ET200SP IM          ✓
6ES7 132-6BH01-0BA0  → found: DQ 8x24VDC         ✓
6ES7 999-0000-0AB0   → NOT FOUND                  ✗
```

---

**Agent → User (checkpoint):**

```
⚠ 1 order number not found in the hardware catalog:

  Row 8 — DeviceName: "IO_Ext_1", OrderNumber: "6ES7 999-0000-0AB0"

Possible causes:
  - Typo in the order number
  - Hardware catalog not installed for this device family

Continue importing the 7 valid devices?
```

**User:**
> Yes, continue.

---

**Agent step 5 — Verify session**

Verify TIA Portal session is active.

Session active ✓

---

**Agent step 6 — Create devices (loop × 7)**

Create each device in the project.

All 7 succeed ✓

---

**Agent step 7 — Save**

Save the project.

---

**Agent response:**

```
✓ Device import completed

Total rows          : 8
Valid (catalog)     : 7   → 7 created
Skipped (no catalog): 1   — Row 8: IO_Ext_1 (6ES7 999-0000-0AB0)

Created devices:
  • PLC_1     — CPU 1516-3 PN/DP     (6ES7 516-3AN02-0AB0)
  • HMI_1     — Comfort Panel 15"    (6AV2 124-0MC01-0AX0)
  • IO_Dev_1  — ET200SP Interface    (6ES7 193-6BP20-0BA0)
  • DI_Mod_1  — Digital Input Module (6ES7 134-6HD00-0BA1)
  • Switch_1  — Scalance X208        (6GK5 208-0BA00-2AC2)
  • IO_Dev_2  — ET200SP IM           (6ES7 155-6AU01-0BN0)
  • DQ_Mod_1  — DQ 8x24VDC          (6ES7 132-6BH01-0BA0)

Next steps:
  - Fix order number for IO_Ext_1 and re-run import for that row
  - Configure Profinet topology
  - Compile hardware
```
