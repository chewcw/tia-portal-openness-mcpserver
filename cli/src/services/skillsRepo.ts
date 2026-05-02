import { access, cp, mkdir, rm } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawn } from "node:child_process";
import { ExternalCommandError } from "../errors.js";
import { ensureGitAvailable } from "./prereqCheck.js";

export interface SkillsSyncOptions {
  repoUrl: string;
  ref: string;
  destinationPath: string;
  sparsePaths?: string[];
}

export interface SkillsSyncResult {
  destinationPath: string;
  ref: string;
  sparsePaths: string[];
}

interface GitRunResult {
  stdout: string;
  stderr: string;
}

function runGit(args: string[]): Promise<GitRunResult> {
  return new Promise((resolve, reject) => {
    const child = spawn("git", args, {
      stdio: ["ignore", "pipe", "pipe"],
    });

    let stdout = "";
    let stderr = "";

    child.stdout.on("data", (chunk: Buffer) => {
      stdout += chunk.toString("utf8");
    });

    child.stderr.on("data", (chunk: Buffer) => {
      stderr += chunk.toString("utf8");
    });

    child.on("error", (error: unknown) => {
      const detail = error instanceof Error ? error.message : String(error);
      reject(new ExternalCommandError("git", detail));
    });

    child.on("close", (code) => {
      if (code === 0) {
        resolve({
          stdout,
          stderr,
        });
        return;
      }

      reject(new ExternalCommandError("git", `${args.join(" ")} failed with code ${code}: ${stderr.trim()}`));
    });
  });
}

function normalizePaths(paths: string[] | undefined): string[] {
  if (!paths) {
    return [];
  }

  const unique = new Set<string>();

  for (const entry of paths) {
    const normalized = entry.replace(/\\/g, "/").trim().replace(/^\/+/, "");
    if (normalized.length > 0) {
      unique.add(normalized);
    }
  }

  return [...unique].sort((a, b) => a.localeCompare(b));
}

function parseDirectoryListing(raw: string): string[] {
  return raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .sort((a, b) => a.localeCompare(b));
}

export async function listDirectoryEntriesAtRef(
  repositoryPath: string,
  ref: string
): Promise<string[]> {
  const result = await runGit(["-C", repositoryPath, "ls-tree", "-d", "--name-only", `${ref}:`]);
  return parseDirectoryListing(result.stdout);
}

async function hasGitRepository(pathValue: string): Promise<boolean> {
  try {
    await access(path.join(pathValue, ".git"));
    return true;
  } catch {
    return false;
  }
}

function getStagingPath(): string {
  return path.join(os.tmpdir(), `tia-skills-${Date.now()}`);
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

  const sparsePaths = normalizePaths(options.sparsePaths);
  const stagingPath = getStagingPath();

  await ensureGitAvailable();

  await rm(stagingPath, { recursive: true, force: true }).catch(() => {});
  await mkdir(stagingPath, { recursive: true });

  await runGit(["clone", "--filter=blob:none", "--no-checkout", repoUrl, stagingPath]);

  if (sparsePaths.length > 0) {
    await runGit(["-C", stagingPath, "sparse-checkout", "init", "--cone"]);
    await runGit(["-C", stagingPath, "sparse-checkout", "set", ...sparsePaths]);
  }

  await runGit(["-C", stagingPath, "checkout", ref]);

  await mkdir(options.destinationPath, { recursive: true });

  for (const skill of sparsePaths) {
    const sourcePath = path.join(stagingPath, skill);
    const destPath = path.join(options.destinationPath, skill);
    await rm(destPath, { recursive: true, force: true }).catch(() => {});
    await cp(sourcePath, destPath, { recursive: true });
    const destGitPath = path.join(destPath, ".git");
    await rm(destGitPath, { recursive: true, force: true }).catch(() => {});
  }

  await rm(stagingPath, { recursive: true, force: true });

  return {
    destinationPath: options.destinationPath,
    ref,
    sparsePaths,
  };
}
