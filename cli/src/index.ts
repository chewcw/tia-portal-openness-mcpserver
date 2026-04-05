#!/usr/bin/env node

import { readFileSync } from "fs";
import { fileURLToPath } from "url";
import { dirname, join } from "path";
import { dispatchCommand } from "./commands/router.js";
import { parseArgs } from "./parser.js";

function findPackageJson(startDir: string): string {
  let currentDir = startDir;
  while (currentDir !== dirname(currentDir)) {
    const candidate = join(currentDir, "package.json");
    try {
      readFileSync(candidate);
      return candidate;
    } catch {
      currentDir = dirname(currentDir);
    }
  }
  throw new Error("package.json not found");
}

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const packageJsonPath = findPackageJson(__dirname);
const packageJson = JSON.parse(readFileSync(packageJsonPath, "utf-8"));
const version = packageJson.version;

function printHelp(): void {
  process.stdout.write(
    [
      "TIA Portal MCP Server CLI",
      "",
      "Usage:",
      "  tia-portal-openness-mcpserver <command> [options]",
      "",
      "Commands:",
      "  install      Install latest or selected server release",
      "  skills       Manage companion skills",
      "",
      "Global options:",
      "  --help       Show this help",
      "  --version    Show CLI version",
      "  --server-version <tag>",
      "  --install-dir <path>",
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
