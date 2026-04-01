#!/usr/bin/env node

import { dispatchCommand } from "./commands/router.js";
import { parseArgs } from "./parser.js";

const version = "0.1.0";

function printHelp(): void {
  process.stdout.write(
    [
      "TIA Portal MCP Server CLI",
      "",
      "Usage:",
      "  tia-mcp <command> [options]",
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
    ].join("\n") + "\n"
  );
}

async function run(argv: string[]): Promise<number> {
  const parsed = parseArgs(argv);

  if (parsed.options.version) {
    process.stdout.write(version + "\n");
    return 0;
  }

  if (argv.length === 0 || parsed.options.help) {
    printHelp();
    return 0;
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
