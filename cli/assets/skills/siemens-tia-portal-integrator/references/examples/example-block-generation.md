# Example — Generate Function Blocks from Excel → TIA Portal

Demonstrates Pattern C end-to-end, including delegation to the
`siemens-awl-stl-programmer` skill.

---

## Conversation

**User:**
> Generate a function block for each row in `blocks_spec.xlsx` and upload them to `PLC_1`.

---

**Agent step 1 — List sheets**

List worksheets in the Excel file.

Result: `["FB_Specs", "FC_Specs"]`

Agent → User:
```
Found 2 sheets: FB_Specs, FC_Specs.
Which sheet should I use? Or should I process both?
```

User: "Process FB_Specs first."

---

**Agent step 2 — Read sheet**

Read the FB_Specs worksheet.

Result: 3 rows

| BlockName | BlockType | Inputs | Outputs | StaticVars | LogicDescription |
|---|---|---|---|---|---|
| PumpControl | FB | RunCmd:BOOL, Fault:BOOL | PumpOut:BOOL, Alarm:BOOL | RunLatch:BOOL | Latch on RunCmd, reset on Fault |
| ValveControl | FB | OpenCmd:BOOL, CloseCmd:BOOL | ValveOpen:BOOL, ValveClosed:BOOL | State:INT | State machine: 0=closed, 1=opening, 2=open, 3=closing |
| ConveyorDrive | FB | Enable:BOOL, Speed:REAL | Running:BOOL, Fault:BOOL | SpeedRamp:REAL | Ramp speed from 0 to Speed over 5s on Enable |

---

**Agent step 3 — Validate columns**

```python
validate_columns(
    headers  = ["BlockName","BlockType","Inputs","Outputs","StaticVars","LogicDescription"],
    required = ["block_name", "block_type"],
    optional = ["inputs", "outputs", "static_vars", "logic"],
)
```

Result: all resolved ✓

---

**Agent step 4 — Verify session and device**

Verify TIA Portal session is active. List available devices.

Result: `PLC_1` available as specified.

---

**Agent step 5 — Row 1: PumpControl**

Build spec and delegate to siemens-awl-stl-programmer:
```
Block name : PumpControl
Block type : FB
Inputs     : RunCmd:BOOL, Fault:BOOL
Outputs    : PumpOut:BOOL, Alarm:BOOL
Static vars: RunLatch:BOOL
Logic      : Latch on RunCmd, reset on Fault
```

→ Generate AWL source → save to `/tmp/PumpControl.awl` → upload and compile

Result: compiled ✓

---

**Agent step 5 — Row 2: ValveControl**

→ Generate AWL source → save to `/tmp/ValveControl.awl` → upload and compile

Result: compiled with 2 warnings:
```
W: Line 45 — NOP 0 inserted before unconditional jump
W: Line 67 — Unreachable code after BEU
```

Agent notes warnings but continues (non-fatal).

---

**Agent step 5 — Row 3: ConveyorDrive**

→ Generate AWL source → save to `/tmp/ConveyorDrive.awl` → upload and compile

Result: compiled ✓

---

**Agent step 6 — Full software compile and save**

Compile software for PLC_1 and save the project.

Result: 0 errors, 2 warnings (same ValveControl warnings as above).

---

**Agent response:**

```
✓ Block generation completed (FB_Specs sheet)

Blocks processed : 3
Generated        : 3
Compile errors   : 0
Compile warnings : 2

  • PumpControl   (FB) — ✓ compiled, no issues
  • ValveControl  (FB) — ✓ compiled, 2 warnings:
      - NOP 0 inserted before unconditional jump (line 45)
      - Unreachable code after BEU (line 67)
  • ConveyorDrive (FB) — ✓ compiled, no issues

Recommendations:
  - Review ValveControl jump logic — the siemens-awl-stl-programmer skill
    can regenerate with explicit jump labels if needed

Shall I now process the FC_Specs sheet?
```
