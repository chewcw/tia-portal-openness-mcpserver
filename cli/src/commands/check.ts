import { CommandContext } from "../types.js";

export async function checkCommand(context: CommandContext): Promise<number> {
  void context;
  process.stdout.write("check command is not implemented yet.\n");
  return 1;
}
