# Script: list_sheets

List all worksheet names in an Excel `.xlsx` file.  
Use this when `files_list_sheets` MCP tool is unavailable.

## When to Use

- User has not specified which sheet to read
- You need to enumerate sheets before calling `read_excel`
- MCP `files_list_sheets` is not available in the current session

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
List all worksheet names in an Excel file.
Usage : python list_sheets.py <filepath>
Output: {"sheets": [...], "sheet_count": N}

Compatible: Python 3.6+, Windows / Linux / macOS
Requires  : openpyxl  (pip install openpyxl)
"""
from __future__ import annotations

import json
import sys


def list_sheets(filepath: str) -> dict:
    try:
        import openpyxl
    except ImportError:
        return {"error": "openpyxl not installed. Run: pip install openpyxl"}
    wb = openpyxl.load_workbook(filepath, read_only=True, data_only=True)
    return {"sheets": wb.sheetnames, "sheet_count": len(wb.sheetnames)}


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"error": "Usage: list_sheets.py <filepath>"}))
        sys.exit(1)
    try:
        print(json.dumps(list_sheets(sys.argv[1]), indent=2))
    except Exception as exc:
        print(json.dumps({"error": str(exc)}))
        sys.exit(1)
```

## Example Output

```json
{
  "sheets": ["Devices", "Tags", "Blocks", "Config"],
  "sheet_count": 4
}
```

## Windows Usage

```cmd
python list_sheets.py C:\Projects\hardware_list.xlsx
```

## Agent Behaviour After Calling This Script

If the user has not specified a sheet name:
- If only one sheet exists → use it automatically
- If multiple sheets exist → ask the user which sheet to use, displaying the list

```
The Excel file has 4 sheets: Devices, Tags, Blocks, Config.
Which sheet contains the data you want to import?
```
