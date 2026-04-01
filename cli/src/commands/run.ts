import { CommandContext } from "../types.js";
import { access } from "node:fs/promises";
import { spawn } from "node:child_process";
import { getDefaultStateFilePath, loadCliState } from "../state/installStateStore.js";

export async function runCommand(context: CommandContext): Promise<number> {
  void context;

  const stateFilePath = getDefaultStateFilePath();
  const state = await loadCliState(stateFilePath);

  if (!state.installedServer) {
    process.stderr.write("No installed server found. Run install first.\n");
    return 1;
  }

  const executablePath = state.installedServer.executablePath;

  try {
    await access(executablePath);
  } catch {
    process.stderr.write(`Installed executable not found: ${executablePath}\n`);
    process.stderr.write("Run install to repair the installation.\n");
    return 1;
  }

  return await new Promise<number>((resolve) => {
    const child = spawn(executablePath, context.parsed.args, {
      stdio: "inherit",
    });

    child.on("error", (error: unknown) => {
      const message = error instanceof Error ? error.message : String(error);
      process.stderr.write(`Failed to launch server: ${message}\n`);
      resolve(1);
    });

    child.on("close", (code) => {
      resolve(code ?? 1);
    });
  });
}
