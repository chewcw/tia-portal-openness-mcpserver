# Siemens S7 STL (AWL) Core Instruction Reference

Follow these rules strictly:

* STL is accumulator-based (ACCU1 / ACCU2).
* Boolean instructions modify the RLO (Result of Logic Operation).
* Instructions execute sequentially.
* Logic flows left-to-right within a network.
* Always use valid Siemens STL mnemonics.
* Use uppercase mnemonics.
* Keep industrial structure clear and deterministic.
* Assume standard scan cycle model (Read → Execute → Write).

**Official reference (consult for instructions not covered here):**
`https://docs.tia.siemens.cloud/r/en-us/v21/creating-stl-programs-s7-300-s7-400-s7-1500`

---

# 0. Block Number (`NAME`) Line — Always Omit (Unless User Requests a Specific Block Number)

The `NAME : '<number>'` line in block headers (e.g., `NAME : '1'`) specifies the numeric block number. **Always omit this line, unless the user has specifically requested a fixed block number.** TIA Portal automatically assigns the next available block number on import. Including it risks a number conflict with existing project blocks.

```
// ADVISED — no NAME line, TIA assigns block number automatically
FUNCTION "My FC" : Void
{ S7_Optimized_Access := 'TRUE' }
VERSION : 0.1

// NOT ADVISED — never generate this, unless user requested a specific block number
FUNCTION "My FC" : Void
{ S7_Optimized_Access := 'TRUE' }
NAME : '1'
VERSION : 0.1
```

---

# 1. Boolean Logic Instructions

These operate on the RLO (Result of Logic Operation).

Valid mnemonics:

```
A     // AND
AN    // AND NOT
O     // OR
ON    // OR NOT
=     // Assign RLO to operand
S     // Set (latch — retentive)
R     // Reset (unlatch — retentive)
```

Example:

```awl
      A     I0.0;      // AND input
      AN    I0.1;      // AND NOT input
      O     I0.2;      // OR input
      ON    I0.3;      // OR NOT input
      =     Q0.0;      // Assign RLO to output
      S     M0.0;      // Set (latch)
      R     M0.0;      // Reset (unlatch)
```

### Execution Notes

* `A` loads operand into RLO (or ANDs with existing RLO).
* `=` writes RLO without clearing it — can assign to multiple outputs in sequence.
* `S` and `R` are retentive — they hold state across scans regardless of RLO on next scan.
* RLO is built sequentially within a network.

---

# 2. Parenthesized Boolean Logic

Used to group boolean sub-expressions, equivalent to parentheses in algebra. Essential for (A OR B) AND C patterns.

```
A(    // Begin AND group
O(    // Begin OR group
);    // Close group
```

Example — open valve if (Manual OR Auto) AND Ready AND NOT EStop:

```awl
      A(;
         O     #ManualOpen;
         O     #AutoOpen;
      );
      A     #SystemReady;
      AN    #EmergencyStop;
      =     #ValveOutput;
```

### Notes

* `A(;` and `O(;` each end with a semicolon.
* The closing `);` is on its own line.
* Nesting is supported but keep depth ≤ 3 for readability.
* The RLO inside the parentheses is evaluated first, then combined with the outer RLO.

---

# 3. Edge Detection

Used for detecting signal transitions.

```
FP    // Rising edge (Flank Positive)
FN    // Falling edge (Flank Negative)
```

Example:

```awl
      A     I0.0;
      FP    M0.1;      // M0.1 stores previous state
      =     M0.2;      // M0.2 is true for one scan on rising edge
```

### Notes

* `FP` and `FN` require a dedicated memory bit to store the previous state.
* The memory bit must not be reused elsewhere in the program.
* Used for pushbutton detection, pulse counting, one-shot event triggering.
* Result is true for exactly one scan cycle on the detected edge.

---

# 4. Timers

Two timer styles are available. Choose based on the block type the timer lives in.

---

## 4a. S5 Timers (use inside FC or standalone OB logic)

Time literal format: `S5T#3S`, `S5T#500MS`, `S5T#1M30S`

| Instruction | Meaning                        |
|-------------|-------------------------------|
| `SP`        | Pulse timer                   |
| `SE`        | On-delay (non-retentive)      |
| `SD`        | Retentive on-delay            |
| `SA`        | Off-delay                     |
| `SS`        | Retentive pulse timer         |

Example — on-delay in an FC:

```awl
NETWORK
TITLE = Start Conveyor with On-Delay
// Run T5 for 5 seconds when Enable is active
      A     #Enable;
      L     S5T#5S;
      SE    T5;

NETWORK
TITLE = Conveyor Run After Delay
// Activate conveyor output once T5 has elapsed
      A     T5;
      =     #ConveyorRun;
```

### Notes

* Always precede the timer instruction with `A <condition>` and `L S5T#...`.
* Check timer status with `A T<n>` after the timer instruction.
* S5 timers are global — their state is not stored in an instance DB.
* **Use S5 timers in FCs.** Use IEC timers in FBs.
* Timer number range: T0–T255 (S7-300/400).

---

## 4b. IEC Timers — TON, TOF, TP (use inside FB only)

IEC timers are declared in the FB's `VAR` section. Their state is stored in the instance DB automatically.

| Type  | Meaning        |
|-------|---------------|
| `TON` | On-delay       |
| `TOF` | Off-delay      |
| `TP`  | Pulse timer    |

Accessing IEC timer members:

```
#TimerName.IN    // Input (enable signal)
#TimerName.PT    // Preset time (write with L + T)
#TimerName.Q     // Output (elapsed/active flag)
#TimerName.ET    // Elapsed time (read with L)
```

Example (inside FB):

```awl
NETWORK
TITLE = Start Delay Timer (3 seconds)
// Load preset and transfer to timer — timer runs while RunLatch is set
      A     #RunLatch;
      L     S5T#3S;
      T     #StartDelay.PT;

NETWORK
TITLE = Motor Output After Delay
// Activate output once timer Q output is true
      A     #StartDelay.Q;
      =     #MotorOutput;
```

### Timer Selection Summary

| Context     | Timer type    | Instruction style              |
|-------------|---------------|-------------------------------|
| Inside FC   | S5 Timer      | `L S5T#...; SE T<n>;`         |
| Inside FB   | IEC TON/TOF/TP| Declared in VAR, access via `.PT` / `.Q` |
| Off-delay   | S5: `SA`      | IEC: `TOF`                    |
| Pulse       | S5: `SP`      | IEC: `TP`                     |
| Retentive   | S5: `SS`/`SD` | Store DB as RETAIN             |

---

# 5. Counter Instructions

```awl
      L     C#10;      // Load preset value into ACCU1
      CU    C1;        // Count up on rising edge of RLO
      CD    C1;        // Count down on rising edge of RLO
      R     C1;        // Reset counter to 0
      A     C1;        // RLO = 1 if counter value > 0
      L     C1;        // Load current counter value into ACCU1
      T     MW10;      // Transfer counter value to memory word
```

| Instruction | Meaning                          |
|-------------|----------------------------------|
| `CU`        | Count up (on rising edge of RLO) |
| `CD`        | Count down (on rising edge)      |
| `R`         | Reset counter to 0               |
| `A Cn`      | RLO = 1 if counter value > 0     |
| `L Cn`      | Load counter value into ACCU1    |

Example — batch counter:

```awl
NETWORK
TITLE = Count Parts on Sensor
// Increment C10 on each rising edge of part sensor
      A     #PartSensor;
      L     #BatchTarget;
      CU    C10;

NETWORK
TITLE = Reset Counter
// Reset on batch reset command
      A     #BatchReset;
      R     C10;

NETWORK
TITLE = Batch Complete
// Signal complete when counter is at or above target
      A     C10;
      =     #BatchComplete;
```

### Notes

* Load preset with `L C#<value>` or a variable before the counter instruction.
* `CU` and `CD` only increment/decrement on a rising edge of the RLO — must be gated with `A`.
* Counter value range: 0–999.
* Counter numbers: C0–C255 (S7-300/400).

---

# 6. Arithmetic Instructions

### Integer Math

```awl
      L     MW10;
      L     MW12;
      +I;
      T     MW14;
```

| Instruction | Meaning          |
|-------------|-----------------|
| `+I`        | Add Integer      |
| `-I`        | Subtract Integer |
| `*I`        | Multiply Integer |
| `/I`        | Divide Integer   |
| `MOD`       | Modulo (remainder) |

---

### Real Math

```awl
      L     MD10;
      L     MD14;
      +R;
      T     MD18;
```

| Instruction | Meaning       |
|-------------|--------------|
| `+R`        | Add Real      |
| `-R`        | Subtract Real |
| `*R`        | Multiply Real |
| `/R`        | Divide Real   |
| `ABS`       | Absolute value |
| `SQR`       | Square (x²)   |
| `SQRT`      | Square root   |

---

# 7. Compare Instructions

Comparison result sets or clears the RLO.

### Integer Compare

```awl
      L     MW10;
      L     100;
      >I;
      =     Q0.1;
```

### Real Compare

```awl
      L     MD10;
      L     80.0;
      >R;
      =     M0.0;
```

| Instruction | Meaning              |
|-------------|---------------------|
| `>I`        | Greater than (INT)   |
| `<I`        | Less than (INT)      |
| `==I`       | Equal (INT)          |
| `>=I`       | Greater or equal (INT)|
| `<=I`       | Less or equal (INT)  |
| `<>I`       | Not equal (INT)      |
| `>R`        | Greater than (REAL)  |
| `<R`        | Less than (REAL)     |
| `==R`       | Equal (REAL)         |
| `>=R`       | Greater or equal (REAL)|
| `<=R`       | Less or equal (REAL) |
| `<>R`       | Not equal (REAL)     |
| `>D`        | Greater than (DINT)  |
| `==D`       | Equal (DINT)         |

---

# 8. Data Movement

```awl
      L     MW10;      // Load MW10 into ACCU1
      T     MW12;      // Transfer ACCU1 to MW12
```

| Instruction | Meaning                   |
|-------------|--------------------------|
| `L`         | Load into ACCU1           |
| `T`         | Transfer ACCU1 to operand |
| `ITD`       | INT → DINT (sign-extend)  |
| `DTR`       | DINT → REAL               |
| `RND`       | REAL → DINT (round)       |
| `TRUNC`     | REAL → DINT (truncate)    |
| `DTB`       | DINT → BCD                |
| `BTD`       | BCD → DINT                |

### Type Conversion Chain (INT → REAL)

```awl
      L     #IN;       // Load INT
      ITD;             // Convert INT to DINT
      DTR;             // Convert DINT to REAL
      T     #TempReal; // Store as REAL
```

### Type Conversion Chain (REAL → INT)

```awl
      L     #RealVal;  // Load REAL
      RND;             // Round to DINT
      T     MD20;      // Store as DINT (use as INT if range allows)
```

---

# 9. Accumulator Model

STL uses two accumulators (ACCU1, ACCU2). Each `L` instruction shifts ACCU1 into ACCU2 and loads the new value into ACCU1. Arithmetic operates on ACCU1 and ACCU2; result goes into ACCU1.

```awl
      L     10;    // ACCU1 = 10
      L     20;    // ACCU2 = 10, ACCU1 = 20
      +I;          // ACCU1 = ACCU2 + ACCU1 = 30
      T     MW0;   // MW0 = 30
```

### Arithmetic operand order (CRITICAL):

| Operation | Result                  | Load order             |
|-----------|-------------------------|------------------------|
| `+I/+R`   | ACCU2 + ACCU1           | Order doesn't matter   |
| `-I/-R`   | ACCU2 − ACCU1           | Load minuend first, then subtrahend |
| `*I/*R`   | ACCU2 × ACCU1           | Order doesn't matter   |
| `/I//R`  | ACCU2 ÷ ACCU1           | Load dividend first, then divisor |

Example — subtraction (order matters):

```awl
      L     #HI_LIM;   // ACCU1 = HI_LIM (minuend)
      L     #LO_LIM;   // ACCU2 = HI_LIM, ACCU1 = LO_LIM (subtrahend)
      -R;              // ACCU1 = HI_LIM - LO_LIM
```

---

# 10. RLO (Result of Logic Operation)

Boolean logic builds the RLO sequentially within a network.

```awl
      A     I0.0;      // RLO = I0.0
      AN    I0.1;      // RLO = RLO AND NOT I0.1
      =     Q0.0;      // Q0.0 = RLO (RLO is not cleared)
      =     Q0.1;      // Can assign same RLO to multiple outputs
```

* RLO is set to 1 at the start of each new network.
* `=` reads RLO without clearing it — assign to multiple outputs if needed.
* `S` and `R` are conditional on RLO but do not change it.

---

# 11. Jump Instructions

Used for conditional branching within a network or block.

| Instruction | Meaning                               |
|-------------|--------------------------------------|
| `JC`        | Jump if RLO = 1                      |
| `JCN`       | Jump if RLO = 0                      |
| `JU`        | Unconditional jump                   |
| `JZ`        | Jump if result = 0 (after arithmetic) |
| `JP`        | Jump if result > 0                   |
| `JN`        | Jump if result ≠ 0                   |
| `JM`        | Jump if result < 0                   |

Label and jump example — conditional FC call:

```awl
      A     M0.0;          // Enable condition
      JCN   SkipCall;      // Skip if condition false

      CALL "My FC"
      (  Input1              := MW10 ,
         Output1             := MW12
      );

SkipCall: NOP 0;            // Jump target — no operation
```

### Notes

* Labels must be unique within the block.
* `NOP 0` is a no-operation instruction used as a jump target.
* Prefer network separation over jumps where possible for readability.

---

# 12. CALL Instruction

Used to invoke FCs and FBs from OBs, FCs, or FBs.

| Syntax | Usage |
|--------|-------|
| `CALL "FC Name" ( ... )` | Call a Function (no instance DB) |
| `CALL "FB Name", "DB Name" ( ... )` | Call a Function Block with instance DB |

### FC Call

```awl
      CALL "Analog Scaling"
      (  IN                          := #RawAnalog ,
         HI_LIM                      := #HI ,
         LO_LIM                      := #LOW ,
         OUT                         := #OUTPUT
      );
```

### FB Call

```awl
      CALL "Motor Control", "Motor Control_DB"
      (  StartPB                     := #StartPB ,
         StopPB                      := #StopPB ,
         Overload                    := #Overload ,
         Temperature                 := #Temperature ,
         MotorOutput                 := #MotorOutput ,
         AlarmOutput                 := #AlarmOutput
      );
```

### Multiple Instances of the Same FB

```awl
      CALL "Pump Control", "Pump1_DB"
      (  StartCmd                    := M10.0 ,
         StopCmd                     := M10.1 ,
         PumpRun                     := M10.3
      );

      CALL "Pump Control", "Pump2_DB"
      (  StartCmd                    := M11.0 ,
         StopCmd                     := M11.1 ,
         PumpRun                     := M11.3
      );
```

Each `Pump1_DB` and `Pump2_DB` is a separate FB Instance Data Block referencing `"Pump Control"`.

### Rules

* Use quoted symbolic names — not numeric references (`FC 5`, `FB 20`) unless the user requires them.
* Align `:=` assignments vertically using spaces.
* Each parameter on its own line; last parameter has no trailing comma.
* CALL is blocking — execution resumes after the called block completes.
* One dedicated instance DB per FB instance.

---

# 13. VAR Section Types

| Section       | Used in     | Meaning                                               |
|---------------|-------------|-------------------------------------------------------|
| `VAR_INPUT`   | FC / FB     | Read-only inputs from caller                          |
| `VAR_OUTPUT`  | FC / FB     | Write-only outputs to caller                          |
| `VAR_IN_OUT`  | FC / FB     | Read and write by both caller and block               |
| `VAR_TEMP`    | OB / FC / FB| Temporary — valid only during the current scan of the block |
| `VAR`         | FB / DB     | Static — persists between scans (stored in instance DB) |

`VAR_IN_OUT` example — accumulating output:

```awl
VAR_IN_OUT
    Output  : REAL ;    // Caller passes reference; FB reads, modifies, writes back
END_VAR
```

---

# 14. Memory Address Areas

| Area   | Access        | Example          | Notes                         |
|--------|---------------|------------------|-------------------------------|
| I      | Input image   | `I0.0`, `IW2`    | Refreshed at scan start        |
| Q      | Output image  | `Q4.0`, `QW10`   | Written to outputs at scan end |
| M      | Bit memory    | `M10.0`, `MW20`  | General-purpose markers        |
| PIW    | Peripheral in | `PIW256`         | Direct hardware analog input   |
| PQW    | Peripheral out| `PQW256`         | Direct hardware analog output  |
| DB     | Data block    | `DB1.DBW0`       | Via open DB or symbolic access |
| L      | Local data    | `L#` prefix      | Temp vars in current block     |

---

# 15. PLC Scan Model Assumption

Always assume:

1. Inputs are read at the start of each scan (input image refresh).
2. OB1 executes sequentially, network by network.
3. Outputs are written at the end of each scan (output image transfer).
4. Next scan begins immediately.

Do not generate event-driven code unless generating an interrupt OB (OB35, OB40, etc.).
Do not assume parallel execution.

---

# 16. Output Constraints for AI

When generating STL:

* Use correct Siemens mnemonics — no invented instructions.
* Do not mix with Ladder (LAD) or Structured Control Language (SCL).
* Do not mix TIA Portal S7-1200/1500-only syntax unless explicitly requested.
* Avoid pseudocode.
* **Never include `NAME : '<number>'`** in generated block headers, unless the user has specifically requested a fixed block number.
* Respect accumulator load order for arithmetic (especially subtraction and division).
* Maintain deterministic execution order within each network.
* Keep networks logically separated — one purpose per network.
* Every instruction line ends with `;`.
* Block examples and formatting rules are defined in SKILL.md — refer there for complete templates.
* If the required instruction is not listed here, consult the official docs before generating:
  `https://docs.tia.siemens.cloud/r/en-us/v21/creating-stl-programs-s7-300-s7-400-s7-1500`