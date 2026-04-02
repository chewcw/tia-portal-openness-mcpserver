import { CommandContext } from "../types.js";
import { readdir } from "node:fs/promises";
import path from "node:path";
import {
  listDirectoryEntriesAtRef,
  syncSkillsRepository,
} from "../services/skillsRepo.js";
import { loadCliState } from "../state/installStateStore.js";
import { saveSkillsState } from "../state/skillsStateStore.js";
import { SCHEMA_VERSION } from "../state/schema.js";
import { getSkillsPath, getStateFilePath } from "../services/agentPathResolver.js";

const DEFAULT_NAMESPACE_PATH = "siemens";

function printSkillsHelp(): void {
  process.stdout.write(
    [
      "Manage companion skills",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver skills [options] [subcommand] [subcommand-options]",
      "",
      "Subcommands:",
      "  status  Show current skills configuration",
      "  sync    Synchronize skills from repository",
      "  list    List locally available skills",
      "",
      "Options:",
      "  --skills-repo <url>   Skills repository URL",
      "  --skills-ref <ref>    Git reference (branch, tag, commit)",
      "  --skills <name[,name...]>  Specific skills to manage",
      "  --all                 Manage all skills",
      "  --help                Show help",
      "  --version             Show version",
    ].join("\n") + "\n"
  );
}

function printSkillsStatusHelp(): void {
  process.stdout.write(
    [
      "Show current skills configuration",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver skills status [options]",
      "",
      "Options:",
      "  --help                  Show help",
    ].join("\n") + "\n"
  );
}

function printSkillsSyncHelp(): void {
  process.stdout.write(
    [
      "Synchronize skills from repository",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver skills sync [options]",
      "",
      "Options:",
      "  --skills-repo <url>   Skills repository URL",
      "  --skills-ref <ref>    Git reference (branch, tag, commit)",
      "  --skills <name[,name...]>  Specific skills to sync",
      "  --all                 Sync all skills",
      "  --help                Show help",
    ].join("\n") + "\n"
  );
}

function printSkillsListHelp(): void {
  process.stdout.write(
    [
      "List locally available skills",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver skills list [options]",
      "",
      "Options:",
      "  --help                  Show help",
    ].join("\n") + "\n"
  );
}

interface NormalizedSkillsRepo {
  repositoryUrl: string;
  treeRef?: string;
  namespacePath: string;
}

function normalizeRepositorySource(source: string | undefined): NormalizedSkillsRepo | undefined {
  if (!source) {
    return undefined;
  }

  const trimmed = source.trim();
  if (!trimmed) {
    return undefined;
  }

  const githubTreeMatch = trimmed.match(/^https:\/\/github\.com\/([^/]+)\/([^/]+)\/tree\/([^/]+)(?:\/(.+))?$/i);
  if (githubTreeMatch) {
    const owner = githubTreeMatch[1];
    const repo = githubTreeMatch[2];
    const ref = githubTreeMatch[3];
    const pathSuffix = githubTreeMatch[4]?.replace(/\/+$/, "");
    const normalized: NormalizedSkillsRepo = {
      repositoryUrl: `https://github.com/${owner}/${repo}`,
      namespacePath: pathSuffix && pathSuffix.length > 0 ? pathSuffix : DEFAULT_NAMESPACE_PATH,
    };

    if (ref) {
      normalized.treeRef = ref;
    }

    return normalized;
  }

  return {
    repositoryUrl: trimmed.replace(/\/+$/, ""),
    namespacePath: DEFAULT_NAMESPACE_PATH,
  };
}

function parseRequestedSkills(rawSkills: string[] | undefined): string[] {
  if (!rawSkills) {
    return [];
  }

  const unique = new Set<string>();

  for (const entry of rawSkills) {
    const name = entry.trim();
    if (name.length === 0) {
      continue;
    }

    if (!/^[A-Za-z0-9._-]+$/.test(name)) {
      throw new Error(`Invalid skill name '${entry}'. Use folder names only.`);
    }

    unique.add(name);
  }

  return [...unique].sort((a, b) => a.localeCompare(b));
}

function nonEmpty(value: string | undefined): string | undefined {
  if (!value) {
    return undefined;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

async function listLocalSkillFolders(rootPath: string): Promise<string[]> {
  const namespaceRoot = path.join(rootPath, DEFAULT_NAMESPACE_PATH);
  const entries = await readdir(namespaceRoot, { withFileTypes: true });
  return entries
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort((a, b) => a.localeCompare(b));
}

function resolveSubcommand(args: string[]): string {
  const candidate = args[0]?.toLowerCase();
  if (!candidate) {
    return "status";
  }

  if (candidate === "status" || candidate === "sync" || candidate === "list") {
    return candidate;
  }

  return "";
}

async function printStatus(): Promise<number> {
  const stateFilePath = getStateFilePath();
  const state = await loadCliState(stateFilePath);

  if (!state.skills) {
    process.stdout.write("No skills metadata found. Run 'skills sync' first.\n");
    return 0;
  }

  process.stdout.write(
    [
      `repo=${state.skills.repoUrl}`,
      `ref=${state.skills.ref}`,
      `local=${state.skills.localPath}`,
      `synced_at=${state.skills.syncedAtUtc}`,
      `server_version=${state.skills.serverVersion}`,
      `selected_skills=${state.skills.selectedSkills.join(",")}`,
      `selected_paths=${state.skills.selectedPaths.join(",")}`,
    ].join("\n") + "\n"
  );

  return 0;
}

async function syncSkills(context: CommandContext): Promise<number> {
  const stateFilePath = getStateFilePath();
  const state = await loadCliState(stateFilePath);

  const parsedRepo = normalizeRepositorySource(context.parsed.options.skillsRepo || "https://github.com/chewcw/agent-skills/tree/main/siemens");
  const stateRepo = normalizeRepositorySource(state.skills?.repoUrl);
  const resolvedRepo = parsedRepo?.repositoryUrl ?? stateRepo?.repositoryUrl;

  if (!resolvedRepo) {
    process.stderr.write("Skills repository URL is required. Use --skills-repo or run install with skills sync.\n");
    return 1;
  }

  const namespacePath = parsedRepo?.namespacePath ?? stateRepo?.namespacePath ?? DEFAULT_NAMESPACE_PATH;
  const resolvedRef =
    nonEmpty(context.parsed.options.skillsRef) ??
    parsedRepo?.treeRef ??
    state.skills?.ref ??
    state.installedServer?.serverVersion ??
    "main";

  const destinationPath = state.skills?.localPath ?? getSkillsPath();
  const requestedSkills = parseRequestedSkills(context.parsed.options.skills);

  if (context.parsed.options.allSkills && requestedSkills.length > 0) {
    process.stderr.write("Use either --skills or --all, not both.\n");
    return 1;
  }

  await syncSkillsRepository({
    repoUrl: resolvedRepo,
    ref: resolvedRef,
    destinationPath,
  });

  const availableSkills = await listDirectoryEntriesAtRef(destinationPath, resolvedRef, namespacePath);

  if (availableSkills.length === 0) {
    process.stderr.write(`No skills found under '${namespacePath}' at ref '${resolvedRef}'.\n`);
    return 1;
  }

  const availableSet = new Set(availableSkills);
  const selectedSkills =
    requestedSkills.length > 0 || context.parsed.options.allSkills
      ? (requestedSkills.length > 0 ? requestedSkills : availableSkills)
      : availableSkills;

  const invalidSkills = selectedSkills.filter((skill) => !availableSet.has(skill));
  if (invalidSkills.length > 0) {
    process.stderr.write(`Unknown skills: ${invalidSkills.join(", ")}\n`);
    process.stderr.write(`Available skills: ${availableSkills.join(", ")}\n`);
    return 1;
  }

  const selectedPaths = selectedSkills.map((skill) => `${namespacePath}/${skill}`);

  const result = await syncSkillsRepository({
    repoUrl: resolvedRepo,
    ref: resolvedRef,
    destinationPath,
    sparsePaths: selectedPaths,
  });

  const serverVersion = state.installedServer?.serverVersion ?? state.skills?.serverVersion ?? "unknown";

  await saveSkillsState(
    {
      schemaVersion: SCHEMA_VERSION,
      repoUrl: resolvedRepo,
      ref: resolvedRef,
      localPath: result.destinationPath,
      selectedSkills,
      selectedPaths,
      syncedAtUtc: new Date().toISOString(),
      serverVersion,
    },
    stateFilePath
  );

  process.stdout.write(
    [
      `repo=${resolvedRepo}`,
      `ref=${resolvedRef}`,
      `namespace=${namespacePath}`,
      `local=${result.destinationPath}`,
      `selected_skills=${selectedSkills.join(",")}`,
    ].join("\n") + "\n"
  );

  return 0;
}

async function listSkills(): Promise<number> {
  const state = await loadCliState(getStateFilePath());
  const root = state.skills?.localPath ?? getSkillsPath();

  try {
    const names = await listLocalSkillFolders(root);
    if (names.length === 0) {
      process.stdout.write("No local skills found. Run 'skills sync' first.\n");
      return 0;
    }

    process.stdout.write(names.join("\n") + "\n");
    return 0;
  } catch {
    process.stderr.write("Skills are not available locally. Run 'skills sync' first.\n");
    return 1;
  }
}

export async function skillsCommand(context: CommandContext): Promise<number> {
  // Check for help flag first
  if (context.parsed.options.help) {
    const subcommand = resolveSubcommand(context.parsed.args);
    
    // Show specific help based on subcommand
    if (subcommand === "status") {
      printSkillsStatusHelp();
      return 0;
    }
    
    if (subcommand === "sync") {
      printSkillsSyncHelp();
      return 0;
    }
    
    if (subcommand === "list") {
      printSkillsListHelp();
      return 0;
    }
    
    // Show general skills help if no subcommand or unknown subcommand
    printSkillsHelp();
    return 0;
  }

  const subcommand = resolveSubcommand(context.parsed.args);

  if (!subcommand) {
    process.stderr.write("Unknown skills subcommand. Use: status, sync, or list.\n");
    return 1;
  }

  if (subcommand === "status") {
    return printStatus();
  }

  if (subcommand === "sync") {
    return syncSkills(context);
  }

  if (subcommand === "list") {
    return listSkills();
  }

  process.stderr.write("Unknown skills subcommand. Use: status, sync, or list.\n");
  return 1;
}
