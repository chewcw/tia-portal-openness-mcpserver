import { installCommand } from "./install.js";
import { skillsCommand } from "./skills.js";
import { CommandContext, CommandHandler } from "../types.js";

const COMMANDS: Record<string, CommandHandler> = {
  install: installCommand,
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
