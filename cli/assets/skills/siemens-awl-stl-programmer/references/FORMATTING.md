# Siemens S7 STL Formatting Rules

These formatting rules MUST be followed for all generated STL code.

---

## Block Header

- Use quoted symbolic names: `FUNCTION_BLOCK "Motor Control"` — not numeric references like `FB 20`
- Always include `{ S7_Optimized_Access := 'TRUE' }` on the line immediately after the block keyword line
- Always include `VERSION : 0.1`
- **NEVER include `NAME : '<number>'`** — omit entirely; TIA Portal auto-assigns

**Correct:**
```
FUNCTION "Analog Scaling" : Void
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1
```

**Incorrect (do not use):**
```
FUNCTION "Analog Scaling" : Void
{ S7_Optimized_Access := 'TRUE' }
NAME : '1'
VERSION : 0.1
```

---

## VAR Sections

- Each variable on its own line ending with `;`
- Align variable names and data types for readability
- Data types in UPPERCASE: BOOL, INT, REAL, BYTE, DINT, TON, TOF, TP, etc.
- Use `VAR_IN_OUT` when a parameter must be both read and written within the FB

**Example:**
```
VAR_INPUT
    StartPB      : BOOL ;
    StopPB       : BOOL ;
    Overload     : BOOL ;
    Temperature  : REAL ;
END_VAR

VAR_OUTPUT
    MotorOutput  : BOOL ;
    AlarmOutput  : BOOL ;
END_VAR

VAR
    StartEdge    : BOOL ;
    RunLatch     : BOOL ;
    HighTemp     : BOOL ;
    StartDelay   : TON ;
END_VAR
```

---

## Networks

- Every logical group gets its own `NETWORK` + `TITLE = ...` header
- Add a `// comment` line directly below TITLE to describe the network's purpose
- Instructions are indented with 6 spaces
- Every instruction line ends with `;`
- Leave a blank line between NETWORKs

**Example:**
```
NETWORK
TITLE = Rising Edge Detection for Start
// Detect rising edge of StartPB to initiate start sequence
      A     #StartPB;
      FP    #StartEdge;
      =     #StartEdge;

NETWORK
TITLE = Start Latch Logic
// Motor starts if start edge detected, no stop signal, no overload
      A     #StartEdge;
      AN    #StopPB;
      AN    #Overload;
      S     #RunLatch;
```

---

## CALL Syntax

- FC call: `CALL "FC Name"` with parameters in aligned `( param := value ,` format
- FB call: `CALL "FB Name", "Instance DB Name"` with same parameter format
- Align `:=` using spaces so all assignments line up vertically
- Each parameter on its own line; last parameter has no trailing comma

**FC Call:**
```
CALL "Analog Scaling"
(  IN                          := #RawAnalog ,
   HI_LIM                      := #HI ,
   LO_LIM                      := #LOW ,
   OUT                         := #OUTPUT
);
```

**FB Call:**
```
CALL "Motor Control", "Motor Control_DB"
(  StartPB                     := #StartPB ,
   StopPB                      := #StopPB ,
   Overload                    := #Overload ,
   Temperature                 := #Temperature ,
   MotorOutput                 := #MotorOutput ,
   AlarmOutput                 := #AlarmOutput
);
```

---

## Parenthesized Logic

- Use `A(;` ... `);` or `O(;` ... `);` for grouped boolean sub-expressions
- Always close with `);` on its own line before continuing the outer logic

**Example:**
```
A(;
   O     #ManualOpen;
   O     #AutoOpen;
);
A     #SystemReady;
AN    #EmergencyStop;
=     #OpenLatch;
```

---

## Comments

- Use `//` for inline and network description comments
- Do not use `(*` ... `*)` block comment style

---

## Semicolons

- Every instruction line ends with `;`
- Every VAR declaration ends with `;`
- Every STRUCT member ends with `;`

---

## Variable Prefixing

- Prefix all local/static variables with `#` (e.g., `#StartPB`, `#RunLatch`)
- Global variables (markers, inputs, outputs) do NOT use prefix

---

## Code Generation Constraints

### MUST:
- Use symbolic quoted names (e.g., `"Motor Control"`) for all block references
- Generate complete, compilable blocks — no stubs or placeholders
- Separate every logical group into its own NETWORK with TITLE and comment
- Use uppercase STL mnemonics (A, AN, O, =, S, R, L, T, FP, FN, ITD, DTR, etc.)
- Use S5 time format for timers (e.g., `S5T#3S`, `S5T#500MS`)
- Use **S5 timers** (`SE`, `SP`, `SD`, `SA`, `SS`) inside FCs
- Use **IEC timers** (`TON`, `TOF`, `TP`) inside FBs (stored in instance DB)
- Always generate the matching instance DB when generating an FB
- **Omit the `NAME : '<number>'` line** from all block headers

### MUST NOT:
- Mix LAD, SCL, or TIA Portal syntax
- Use numeric-only block references (FB 20, FC 5) unless the user explicitly requires them
- Invent unsupported mnemonics
- Write pseudocode or placeholder logic
- Assume S7-1200/1500 features unless explicitly requested
- Include `NAME : '<number>'` in any generated block

---

## Block Hierarchy

```
OB (organization block — main scan cycle entry point)
 ├── CALL "FC Name"              → stateless logic, no instance DB
 └── CALL "FB Name", "DB Name"  → stateful logic, requires instance DB
         └── (same FB can be called with different DBs for multiple instances)

Global DBs  → standalone, referenced by name from any block
UDTs        → referenced as types in DB VAR declarations
```

---

## Industrial Context

Generated code must resemble real industrial use cases:
- Motor start/stop with interlocks and fault latching
- Alarm conditions (temperature, overload, pressure, level)
- Timer-based delays and sequencing steps
- Analog input scaling (0–27648 raw to engineering units)
- Counter-based batch and production tracking
- Conveyor, pump, valve, and compressor control
- Multi-instance FB reuse for identical equipment (pumps, motors, valves)

Avoid toy, abstract, or academic examples.