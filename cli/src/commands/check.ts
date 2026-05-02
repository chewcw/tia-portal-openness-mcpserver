import { CommandContext } from "../types.js";
import { access } from "node:fs/promises";
import { ensureGitAvailable } from "../services/prereqCheck.js";
import { getDefaultStateFilePath, loadCliState } from "../state/installStateStore.js";

export async function checkCommand(context: CommandContext): Promise<number> {
  void context;

  const stateFilePath = getDefaultStateFilePath();
  let hasFailure = false;

  process.stdout.write("Running local checks...\n");

  let stateLoaded = false;
  let installedExecutablePath: string | undefined;

  try {
    const state = await loadCliState(stateFilePath);
    stateLoaded = true;
    installedExecutablePath = state.installedServer?.executablePath;
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    process.stdout.write(`state: FAIL (${message})\n`);
    hasFailure = true;
  }

  if (stateLoaded) {
    process.stdout.write(`state: OK (${stateFilePath})\n`);
  }

  if (!installedExecutablePath) {
    process.stdout.write("install: INFO (server not installed yet)\n");
  } else {
    try {
      await access(installedExecutablePath);
      process.stdout.write(`install: OK (${installedExecutablePath})\n`);
    } catch {
      process.stdout.write(`install: FAIL (missing executable at ${installedExecutablePath})\n`);
      hasFailure = true;
    }
  }

  try {
    await ensureGitAvailable();
    process.stdout.write("git: OK\n");
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    process.stdout.write(`git: WARN (${message})\n`);
  }

  return hasFailure ? 1 : 0;
}
