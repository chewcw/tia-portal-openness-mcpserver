# Script: validate_columns

Map actual spreadsheet headers to canonical field names using a flexible alias table,
and report any missing required columns.

This is a **pure-Python utility** — run it inline (not as a subprocess) by copying the function
into your processing script, or execute it standalone for a quick self-test.

## When to Use

- After reading a CSV or Excel file, before constructing any MCP tool calls
- When headers may differ between files (e.g. `"TagName"` vs `"Name"` vs `"Signal"`)
- To produce a clean `mapping` dict that decouples header names from downstream logic

## Dependencies

Python 3 standard library only — **no pip install required**.  
Compatible with Python 3.6+. Works on Windows, Linux, and macOS.

## Script

```python
#!/usr/bin/env python3
"""
Validate that required columns are present in a list of headers,
using flexible alias matching. Returns a JSON mapping report.

Import and call validate_columns() in-process, or run standalone for a self-test.

Compatible: Python 3.6+, Windows / Linux / macOS
No external dependencies.
"""
from __future__ import annotations

import json
from typing import Dict, List, Optional

# Canonical field → list of recognised column header aliases (case-insensitive)
ALIAS_MAP = {
    "tag_name":        ["Name", "TagName", "Tag", "PLC_Tag", "Signal"],
    "data_type":       ["DataType", "Type", "DataTypeName", "DType"],
    "logical_address": ["LogicalAddress", "Address", "Addr", "HWAddress"],
    "comment":         ["Comment", "Description", "Remark", "Note"],
    "device_name":     ["DeviceName", "Device", "PLC", "Controller"],
    "order_number":    ["OrderNumber", "MLFB", "ArticleNumber", "PartNumber", "OrderNo"],
    "position":        ["Position", "Location", "Slot", "Rack"],
    "block_name":      ["BlockName", "Block", "FBName", "FCName", "OBName"],
    "block_type":      ["BlockType", "Type", "Kind"],
    "db_name":         ["DBName", "InstanceDB", "DataBlock"],
    "inputs":          ["Inputs", "Input", "VarInput", "IN"],
    "outputs":         ["Outputs", "Output", "VarOutput", "OUT"],
    "static_vars":     ["StaticVars", "Static", "VarStatic", "Stat"],
    "logic":           ["LogicDescription", "Logic", "Description", "Behaviour"],
}  # type: Dict[str, List[str]]


def validate_columns(
    headers,           # type: List[str]
    required,          # type: List[str]
    optional=None,     # type: Optional[List[str]]
):
    # type: (...) -> dict
    """
    Parameters
    ----------
    headers  : column names from the CSV / Excel file
    required : canonical field names that MUST be resolved for the import to proceed
    optional : canonical field names that are useful but not mandatory

    Returns
    -------
    {
        "mapping":  { canonical_name: actual_header },   # resolved fields
        "missing":  [ canonical_name, ... ],             # required fields not found
        "warnings": [ canonical_name, ... ],             # optional fields not found
        "valid":    bool                                  # True if no required fields missing
    }
    """
    if optional is None:
        optional = []

    mapping  = {}
    missing  = []
    warnings = []

    lower_map = {h.lower(): h for h in headers}

    def resolve(canonical):
        for alias in ALIAS_MAP.get(canonical, [canonical]):
            if alias.lower() in lower_map:
                return lower_map[alias.lower()]
        return None

    for field in required:
        match = resolve(field)
        if match:
            mapping[field] = match
        else:
            missing.append(field)

    for field in optional:
        match = resolve(field)
        if match:
            mapping[field] = match
        else:
            warnings.append(field)

    return {"mapping": mapping, "missing": missing, "warnings": warnings, "valid": not missing}


if __name__ == "__main__":
    # Self-test
    headers = ["TagName", "DataType", "Addr", "Comment"]
    result  = validate_columns(
        headers,
        required=["tag_name", "data_type"],
        optional=["logical_address", "comment"],
    )
    print(json.dumps(result, indent=2))
```

## Example Output

```json
{
  "mapping": {
    "tag_name":        "TagName",
    "data_type":       "DataType",
    "logical_address": "Addr",
    "comment":         "Comment"
  },
  "missing":  [],
  "warnings": [],
  "valid": true
}
```

## Example: Missing Required Column

Input headers: `["Signal", "Addr"]` — no data type column present.

```json
{
  "mapping":  { "tag_name": "Signal", "logical_address": "Addr" },
  "missing":  ["data_type"],
  "warnings": ["comment"],
  "valid":    false
}
```

Agent response when `valid` is `false`:

```
❌ Required column not found: data_type
   Expected one of: DataType, Type, DataTypeName, DType

   Found columns: Signal, Addr
   Please add a data type column to the file, or tell me which column holds the data type.
```

## Extending the Alias Map

Add new entries to `ALIAS_MAP` for project-specific column conventions:

```python
ALIAS_MAP["ip_address"] = ["IPAddress", "IP", "NetworkAddress", "HostAddress"]
```
