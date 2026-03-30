import { CommandContext } from "../types.js";

export async function runCommand(context: CommandContext): Promise<number> {
  void context;
  process.stdout.write("run command is not implemented yet.\n");
  return 1;
}
