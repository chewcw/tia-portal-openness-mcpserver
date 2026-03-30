import { CommandContext } from "../types.js";

export async function skillsCommand(context: CommandContext): Promise<number> {
  void context;
  process.stdout.write("skills command is not implemented yet.\n");
  return 1;
}
