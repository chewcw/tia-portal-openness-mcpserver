# Script: read_excel

Read one worksheet from an Excel `.xlsx` file and return all rows as a JSON array.  
Use this when `files_read_excel` MCP tool is unavailable or when you need local preprocessing.

## When to Use

- Source file is `.xlsx`
- MCP `files_read_excel` tool is not available in the current session
- You need to process a specific sheet by index rather than name

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
Read one sheet from an Excel (.xlsx) file and return rows as a JSON array.
Usage : python read_excel.py <filepath> [sheet_name_or_index]
Output: {"sheet": "...", "headers": [...], "rows": [{...}, ...], "row_count": N}

Compatible: Python 3.6+, Windows / Linux / macOS
Requires  : openpyxl  (pip install openpyxl)
"""
from __future__ import annotations

import json
import sys
from typing import Union


def read_excel(filepath: str, sheet: Union[str, int] = 0) -> dict:
    try:
        import openpyxl
    except ImportError:
        return {"error": "openpyxl not installed. Run: pip install openpyxl"}

    wb = openpyxl.load_workbook(filepath, data_only=True)

    ws = wb.worksheets[sheet] if isinstance(sheet, int) else wb[sheet]

    rows_iter  = ws.iter_rows(values_only=True)
    raw_header = next(rows_iter, [])
    headers    = [str(c) if c is not None else "col_{}".format(i)
                  for i, c in enumerate(raw_header)]

    rows = []
    for raw in rows_iter:
        row = {headers[i]: (str(v).strip() if v is not None else "")
               for i, v in enumerate(raw)}
        if any(v for v in row.values()):   # skip blank rows
            rows.append(row)

    return {"sheet": ws.title, "headers": headers, "rows": rows, "row_count": len(rows)}


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"error": "Usage: read_excel.py <filepath> [sheet_name_or_index]"}))
        sys.exit(1)
    filepath = sys.argv[1]
    sheet    = sys.argv[2] if len(sys.argv) > 2 else 0
    if isinstance(sheet, str) and sheet.isdigit():
        sheet = int(sheet)
    try:
        print(json.dumps(read_excel(filepath, sheet), ensure_ascii=False, indent=2))
    except Exception as exc:
        print(json.dumps({"error": str(exc)}))
        sys.exit(1)
```

## Example Output

```json
{
  "sheet": "Tags",
  "headers": ["Name", "DataType", "LogicalAddress"],
  "rows": [
    {"Name": "Motor1_Start", "DataType": "Bool", "LogicalAddress": "%I0.0"},
    {"Name": "PumpSpeed",    "DataType": "Real", "LogicalAddress": "%IW4"}
  ],
  "row_count": 2
}
```

## Windows Usage

```cmd
python read_excel.py C:\Projects\io_list.xlsx Tags
python read_excel.py C:\Projects\io_list.xlsx 0
```

Paths with spaces must be quoted:

```cmd
python read_excel.py "C:\My Projects\io list.xlsx" Tags
```

## Notes

- Only `.xlsx` is supported — not legacy `.xls`. To use `.xls` files, open them in Excel and save as `.xlsx` first.
- `data_only=True` reads cached cell values, not live formulas. The file must have been saved in Excel after last edit for formula results to be present.
- Sheet can be referenced by name (`"Tags"`) or zero-based index (`0`)
- Empty rows are silently skipped
- `None` cell values are converted to empty string `""`
- `from __future__ import annotations` and `typing.Union` ensure Python 3.6+ compatibility
