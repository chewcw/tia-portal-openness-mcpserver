---
name: siemens-awl-stl-programmer
description: Generate Siemens S7 AWL/STL blocks and UDT examples for Step7 Classic and TIA Portal. Use when the request is specifically about STL/AWL PLC code, block headers, timers, counters, or memory addressing.
---

# Siemens AWL/STL Programmer

## Scope
- Generate AWL/STL for OB, FC, FB, DB, and UDT blocks.
- Default to TIA Portal-compatible output; switch to Step7 Classic-only formatting when requested.

## Hard Rules
- Omit `NAME : '<number>'` unless the user explicitly requests a fixed block number.
- Use quoted symbolic block names and matching instance DB names.
- Include `{ S7_Optimized_Access := 'TRUE' }` for TIA Portal blocks.
- Use uppercase mnemonics, semicolons on every instruction and declaration line, and one `NETWORK` per logical group.
- Use `//` comments, not `(* ... *)`.
- Use S5 timers in FCs and IEC timers in FBs.
- Keep logic deterministic and compilable; do not mix LAD, FBD, or SCL syntax into AWL.

## Block Patterns
- FC: stateless helper with `VAR_INPUT`, `VAR_OUTPUT`, and optional `VAR_TEMP`.
- FB: stateful logic with `VAR` and a matching instance DB.
- OB: scan-cycle orchestration and block calls.
- DB/UDT: data definitions only.

## References
- `reference/STL_Core_Instructions.md` for instruction syntax and uncommon mnemonics.
- `example_blocks/` for standalone templates.

## Consult Siemens Docs When
Use the official Siemens STL reference when the request uses an instruction or pattern not covered by the local reference files, or when the mnemonic is uncertain: https://docs.tia.siemens.cloud/r/en-us/v21/creating-stl-programs-s7-300-s7-400-s7-1500
