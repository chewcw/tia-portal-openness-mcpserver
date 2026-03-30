import { access, mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { spawn } from "node:child_process";
import { ExternalCommandError } from "../errors.js";
import { ensureGitAvailable } from "./prereqCheck.js";

export interface SkillsSyncOptions {
  repoUrl: string;
  ref: string;
  destinationPath: string;
}

export interface SkillsSyncResult {
  destinationPath: string;
  ref: string;
}

function runGit(args: string[]): Promise<void> {
  return new Promise((resolve, reject) => {
    const child = spawn("git", args, {
      stdio: ["ignore", "pipe", "pipe"],
    });

    let stderr = "";

    child.stderr.on("data", (chunk: Buffer) => {
      stderr += chunk.toString("utf8");
    });

    child.on("error", (error: unknown) => {
      const detail = error instanceof Error ? error.message : String(error);
      reject(new ExternalCommandError("git", detail));
    });

    child.on("close", (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new ExternalCommandError("git", `${args.join(" ")} failed with code ${code}: ${stderr.trim()}`));
    });
  });
}

async function hasGitRepository(pathValue: string): Promise<boolean> {
  try {
    await access(path.join(pathValue, ".git"));
    return true;
  } catch {
    return false;
  }
}

export async function syncSkillsRepository(options: SkillsSyncOptions): Promise<SkillsSyncResult> {
  const repoUrl = options.repoUrl.trim();
  const ref = options.ref.trim();

  if (!repoUrl) {
    throw new Error("Skills repository URL is required when skills sync is enabled.");
  }

  if (!ref) {
    throw new Error("Skills repository ref is required when skills sync is enabled.");
  }

  await mkdir(options.destinationPath, { recursive: true });
  await ensureGitAvailable();

  const alreadyCloned = await hasGitRepository(options.destinationPath);

  if (!alreadyCloned) {
    await rm(options.destinationPath, { recursive: true, force: true });
    await runGit(["clone", "--depth", "1", "--branch", ref, repoUrl, options.destinationPath]);
  } else {
    await runGit(["-C", options.destinationPath, "fetch", "--all", "--tags", "--prune"]);
    await runGit(["-C", options.destinationPath, "checkout", ref]);
  }

  return {
    destinationPath: options.destinationPath,
    ref,
  };
}
