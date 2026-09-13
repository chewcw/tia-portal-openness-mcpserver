# Script: read_csv

Read a CSV file and return all rows as a JSON array.  
Use this when `files_read_csv` MCP tool is unavailable or when you need local preprocessing.

## When to Use

- Source file is a `.csv` (comma, semicolon, or tab delimited)
- MCP `files_read_csv` tool is not available in the current session
- Custom delimiter detection is needed

## Dependencies

Python 3 standard library only (`csv`, `json`) — **no pip install required**.  
Compatible with Python 3.6+. Works on Windows, Linux, and macOS.

## Script

```python
#!/usr/bin/env python3
"""
Read a CSV file and return rows as a JSON array.
Usage : python read_csv.py <filepath> [delimiter]
Output: {"headers": [...], "rows": [{...}, ...], "row_count": N}

Compatible: Python 3.6+, Windows / Linux / macOS
No external dependencies.
"""
from __future__ import annotations

import csv
import json
import sys


def read_csv(filepath: str, delimiter: str = ",") -> dict:
    rows = []
    with open(filepath, newline="", encoding="utf-8-sig") as fh:
        reader = csv.DictReader(fh, delimiter=delimiter)
        headers = list(reader.fieldnames or [])
        for row in reader:
            # Skip entirely blank rows
            if any(v.strip() for v in row.values()):
                rows.append({k: v.strip() for k, v in row.items()})
    return {"headers": headers, "rows": rows, "row_count": len(rows)}


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"error": "Usage: read_csv.py <filepath> [delimiter]"}))
        sys.exit(1)
    filepath  = sys.argv[1]
    delimiter = sys.argv[2] if len(sys.argv) > 2 else ","
    try:
        print(json.dumps(read_csv(filepath, delimiter), ensure_ascii=False, indent=2))
    except Exception as exc:
        print(json.dumps({"error": str(exc)}))
        sys.exit(1)
```

## Example Output

```json
{
  "headers": ["Name", "DataType", "LogicalAddress", "Comment"],
  "rows": [
    {"Name": "Motor1_Start", "DataType": "Bool", "LogicalAddress": "%I0.0", "Comment": "Start button"},
    {"Name": "Motor1_Stop",  "DataType": "Bool", "LogicalAddress": "%I0.1", "Comment": "Stop button"}
  ],
  "row_count": 2
}
```

## Windows Usage

```cmd
python read_csv.py C:\Projects\io_list.csv
python read_csv.py C:\Projects\io_list.csv ;
```

Paths with spaces must be quoted:

```cmd
python read_csv.py "C:\My Projects\io list.csv" ,
```

## Notes

- Encoding: UTF-8 with optional BOM (`utf-8-sig`) — handles files exported from Excel on Windows
- Empty rows are silently skipped
- All cell values are stripped of leading/trailing whitespace
- Common delimiters: `,` (default), `;` (European Excel export), `\t` (tab-separated)
- On Windows, Excel typically exports CSVs with `;` as delimiter when the system locale uses `,` as the decimal separator — pass `;` as the second argument in that case
