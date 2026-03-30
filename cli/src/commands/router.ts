import { checkCommand } from "./check.js";
import { downloadCommand } from "./download.js";
import { installCommand } from "./install.js";
import { listCommand } from "./list.js";
import { runCommand } from "./run.js";
import { skillsCommand } from "./skills.js";
import { updateCommand } from "./update.js";
import { CommandContext, CommandHandler } from "../types.js";

const COMMANDS: Record<string, CommandHandler> = {
  install: installCommand,
  download: downloadCommand,
  list: listCommand,
  check: checkCommand,
  update: updateCommand,
  run: runCommand,
  skills: skillsCommand,
};

export async function dispatchCommand(context: CommandContext): Promise<number> {
  const handler = COMMANDS[context.parsed.name];

  if (!handler) {
    process.stderr.write(`Unknown or missing command: '${context.parsed.name}'. Use --help for usage.\n`);
    return 1;
  }

  return handler(context);
}
