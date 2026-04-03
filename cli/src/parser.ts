import { GlobalOptions, ParsedCommand } from "./types.js";

const KNOWN_COMMANDS = new Set([
  "install",
  "skills",
]);

function parseBooleanFlag(args: string[], index: number, key: keyof GlobalOptions, options: GlobalOptions): number {
  options[key] = true as never;
  return index;
}

function parseValueFlag(
  args: string[],
  index: number,
  key: keyof Pick<GlobalOptions, "serverVersion" | "installDir" | "skillsRepo" | "skillsRef" | "skills" | "agentType">,
  options: GlobalOptions
): number {
  const value = args[index + 1];
  if (!value || value.startsWith("-")) {
    throw new Error(`Missing value for flag ${args[index]}`);
  }

  if (key === "skills") {
    const parsed = value
      .split(",")
      .map((item) => item.trim())
      .filter((item) => item.length > 0);

    if (parsed.length === 0) {
      throw new Error("Missing skill names for flag --skills");
    }

    options.skills = parsed;
    return index + 1;
  }

  options[key] = value;
  return index + 1;
}

export function parseArgs(argv: string[]): ParsedCommand {
  const options: GlobalOptions = {
    help: false,
    version: false,
    yes: false,
    nonInteractive: false,
    verbose: false,
    allSkills: false,
  };

  let commandName: string | undefined;
  const commandArgs: string[] = [];

  for (let i = 0; i < argv.length; i += 1) {
    const token = argv[i];
    if (token === undefined) {
      continue;
    }

    if (token === "--help" || token === "-h") {
      i = parseBooleanFlag(argv, i, "help", options);
      continue;
    }

    if (token === "--version") {
      i = parseBooleanFlag(argv, i, "version", options);
      continue;
    }

    if (token === "--yes") {
      i = parseBooleanFlag(argv, i, "yes", options);
      continue;
    }

    if (token === "--non-interactive") {
      i = parseBooleanFlag(argv, i, "nonInteractive", options);
      continue;
    }

    if (token === "--verbose") {
      i = parseBooleanFlag(argv, i, "verbose", options);
      continue;
    }

    if (token === "--all") {
      i = parseBooleanFlag(argv, i, "allSkills", options);
      continue;
    }

    if (token === "--server-version") {
      i = parseValueFlag(argv, i, "serverVersion", options);
      continue;
    }

    if (token === "--install-dir") {
      i = parseValueFlag(argv, i, "installDir", options);
      continue;
    }

    if (token === "--skills-repo") {
      i = parseValueFlag(argv, i, "skillsRepo", options);
      continue;
    }

    if (token === "--skills-ref") {
      i = parseValueFlag(argv, i, "skillsRef", options);
      continue;
    }

    if (token === "--skills") {
      i = parseValueFlag(argv, i, "skills", options);
      continue;
    }

    if (token === "--agent-type") {
      i = parseValueFlag(argv, i, "agentType", options);
      continue;
    }

    if (!commandName && KNOWN_COMMANDS.has(token)) {
      commandName = token;
      continue;
    }

    commandArgs.push(token);
  }

  return {
    name: commandName ?? "",
    args: commandArgs,
    options,
  };
}
