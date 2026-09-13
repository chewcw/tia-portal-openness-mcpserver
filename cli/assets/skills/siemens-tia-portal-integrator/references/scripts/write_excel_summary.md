# Script: write_excel_summary

Write a list of dicts to a formatted Excel `.xlsx` file.  
Used in **reverse-direction** workflows where project data extracted from TIA Portal is
persisted as a spreadsheet for the user.

## When to Use

- Exporting block inventories, tag lists, or device tables from TIA Portal to Excel
- Producing a summary report after a batch import (created / failed / skipped rows)
- Any workflow where the final deliverable is an `.xlsx` file

## Dependencies

```cmd
:: Windows
pip install openpyxl
```

```bash
# Linux (system-managed Python)
pip install openpyxl --break-system-packages
```

Requires Python 3.6+. Compatible with Python 3.6, 3.7, 3.8, 3.9, 3.10, 3.11, 3.12.

## Script

```python
#!/usr/bin/env python3
"""
Write a list of dicts to a formatted Excel file.
Call write_summary() in-process, or run standalone for a self-test.

Parameters
----------
rows     : list of dicts — each dict is one data row
headers  : column order (keys from each row dict)
filepath : output path, e.g. "C:\\Temp\\block_inventory.xlsx"

Compatible: Python 3.6+, Windows / Linux / macOS
Requires  : openpyxl  (pip install openpyxl)
"""
from __future__ import annotations

import json
from typing import Dict, List


def write_summary(
    rows,        # type: List[Dict]
    headers,     # type: List[str]
    filepath,    # type: str
):
    # type: (...) -> dict
    try:
        import openpyxl
        from openpyxl.styles import Alignment, Font, PatternFill
    except ImportError:
        return {"error": "openpyxl not installed. Run: pip install openpyxl"}

    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "Summary"

    # Header row styling
    header_font = Font(bold=True, color="FFFFFF")
    header_fill = PatternFill("solid", fgColor="1F4E79")
    center      = Alignment(horizontal="center")

    for col_idx, header in enumerate(headers, start=1):
        cell           = ws.cell(row=1, column=col_idx, value=header)
        cell.font      = header_font
        cell.fill      = header_fill
        cell.alignment = center

    # Data rows
    for row_idx, row in enumerate(rows, start=2):
        for col_idx, header in enumerate(headers, start=1):
            ws.cell(row=row_idx, column=col_idx, value=row.get(header, ""))

    # Auto-fit column widths
    for col in ws.columns:
        max_len = max((len(str(cell.value or "")) for cell in col), default=10)
        ws.column_dimensions[col[0].column_letter].width = min(max_len + 4, 60)

    wb.save(filepath)
    return {"success": True, "filepath": filepath, "rows_written": len(rows)}


if __name__ == "__main__":
    sample = [
        {"BlockName": "PumpControl",   "BlockType": "FB", "Inputs": "RunCmd:BOOL, Fault:BOOL"},
        {"BlockName": "ScaleFlow",     "BlockType": "FC", "Inputs": "RawVal:INT"},
        {"BlockName": "ConveyorDrive", "BlockType": "FB", "Inputs": "Enable:BOOL, Speed:REAL"},
    ]
    import tempfile, os
    out = os.path.join(tempfile.gettempdir(), "block_inventory.xlsx")
    result = write_summary(sample, headers=["BlockName", "BlockType", "Inputs"], filepath=out)
    print(json.dumps(result, indent=2))
```

## Example Output (return value)

```json
{
  "success": true,
  "filepath": "C:\\Temp\\block_inventory.xlsx",
  "rows_written": 3
}
```

## Windows Usage Notes

- Use raw strings or double backslashes for Windows paths:
  ```python
  write_summary(rows, headers, r"C:\Temp\output.xlsx")
  write_summary(rows, headers, "C:\\Temp\\output.xlsx")
  ```
- The output directory must already exist — the script does not create it
- If the file is already open in Excel, `wb.save()` will raise a `PermissionError` — close Excel first

## Notes

- Produces a single sheet named `"Summary"` with a styled dark-blue header row
- Column widths are auto-calculated (capped at 60 characters)
- Missing keys in a row dict produce an empty cell — no `KeyError`
- After writing, present the file path to the user for download
