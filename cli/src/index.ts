#!/usr/bin/env node

import { dispatchCommand } from "./commands/router.js";
import { parseArgs } from "./parser.js";
import { setAgentType } from "./services/agentPathResolver.js";

const version = "0.1.0";

function printHelp(): void {
  process.stdout.write(
    [
      "TIA Portal MCP Server CLI",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver <command> [options]",
      "",
      "Commands:",
      "  install      Install latest or selected server release",
      "  download     Download a release asset",
      "  list         List available server releases",
      "  check        Run local prerequisite checks",
      "  update       Update existing installation",
      "  run          Launch installed server",
      "  skills       Manage companion skills",
      "",
      "Global options:",
      "  --help       Show this help",
      "  --version    Show CLI version",
      "  --yes        Accept prompt defaults",
      "  --non-interactive  Disable prompts",
      "  --server-version <tag>",
      "  --install-dir <path>",
      "  --skills-repo <url>",
      "  --skills-ref <ref>",
      "  --skills <name[,name...]>",
      "  --all",
      "  --verbose",
      "  --agent-type <opencode|claude|cursor|generic>",
    ].join("\n") + "\n"
  );
}

async function run(argv: string[]): Promise<number> {
  const parsed = parseArgs(argv);

  if (parsed.options.version) {
    process.stdout.write(version + "\n");
    return 0;
  }

  // Show general help if no command is provided or if help flag is provided without a command
  if (argv.length === 0 || (parsed.options.help && !parsed.name)) {
    printHelp();
    return 0;
  }

  // If help flag is provided with a command, let the command handler deal with it
  // (we don't return here, we let dispatchCommand handle it)

  if (parsed.options.agentType) {
    setAgentType(parsed.options.agentType as "opencode" | "claude" | "cursor" | "generic");
  }

  return dispatchCommand({ parsed });
}

run(process.argv.slice(2))
  .then((exitCode) => {
    process.exitCode = exitCode;
  })
  .catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    process.stderr.write(`Fatal error: ${message}\n`);
    process.exitCode = 1;
  });
