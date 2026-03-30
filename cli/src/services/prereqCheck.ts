import { spawn } from "node:child_process";
import { MissingDependencyError } from "../errors.js";

async function commandExists(command: string, args: string[]): Promise<boolean> {
  return new Promise((resolve) => {
    const child = spawn(command, args, {
      stdio: ["ignore", "ignore", "ignore"],
    });

    child.on("error", () => {
      resolve(false);
    });

    child.on("close", (code) => {
      resolve(code === 0);
    });
  });
}

export async function ensureGitAvailable(): Promise<void> {
  const hasGit = await commandExists("git", ["--version"]);

  if (!hasGit) {
    throw new MissingDependencyError(
      "git",
      "Install Git and ensure it is available on PATH before enabling companion skills sync."
    );
  }
}
