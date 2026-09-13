# agent-skills

Two specialized skills for Siemens PLC programming and TIA Portal integration.

## Skills

### siemens-awl-stl-programmer
Generate Siemens S7-300/S7-400/S7-1500 Statement List (STL/AWL) programs compatible with Step7 Classic and TIA Portal.

### siemens-tia-portal-integrator
Orchestrate data-driven Siemens TIA Portal workflows: read structured data from CSV/Excel files, generate STL/AWL code, and apply changes to a live TIA Portal project.

## Installation

Install individual skills:

```bash
npx skills add chewcw/agent-skills --skill siemens-awl-stl-programmer
npx skills add chewcw/agent-skills --skill siemens-tia-portal-integrator
```

Install both skills at once:

```bash
npx skills add chewcw/agent-skills --skill siemens-awl-stl-programmer --skill siemens-tia-portal-integrator
```
