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
  key: keyof Pick<GlobalOptions, "serverVersion" | "installDir" | "skills" | "agentType">,
  options: GlobalOptions
): number {
  const value = args[index + 1];
  if (!value || value.startsWith("-")) {
    throw new Error(`Missing value for flag ${args[index]}`);
  }

  options[key] = value;
  return index + 1;
}

export function parseArgs(argv: string[]): ParsedCommand {
  const options: GlobalOptions = {
    help: false,
    version: false,
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

    if (token === "--server-version") {
      i = parseValueFlag(argv, i, "serverVersion", options);
      continue;
    }

    if (token === "--install-dir") {
      i = parseValueFlag(argv, i, "installDir", options);
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
