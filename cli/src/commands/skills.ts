import { CommandContext } from "../types.js";
import path from "node:path";
import {
  syncSkillsRepository,
} from "../services/skillsRepo.js";
import { loadServerManifest } from "../state/serverManifest.js";
import {
  loadSkillsManifest,
  saveSkillsManifest,
  SKILLS_MANIFEST_VERSION,
} from "../state/skillsManifest.js";
import { getSkillsPath, setAgentType, resolveSkillsPath } from "../services/agentPathResolver.js";
import { AVAILABLE_SKILLS, COMMON_SKILLS_REPO } from "../constants.js";

type AgentType = "opencode" | "claude" | "cursor" | "generic";

function printSkillsHelp(): void {
  process.stdout.write(
    [
      "Manage companion skills",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver skills install [options]",
      "",
      "Options:",
      "  --skills <name[,name...]>       Skills to install (required)",
      "  --agent-type <type>             Agent type: opencode|claude|cursor|generic (required)",
      "  --help                          Show help",
    ].join("\n") + "\n"
  );
}

function parseSkillsArg(raw: string | undefined): string[] {
  if (!raw) {
    return [];
  }

  return raw
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
}

function validateAgentType(raw: string | undefined): AgentType | undefined {
  if (!raw) {
    return undefined;
  }

  const normalized = raw.toLowerCase().trim();
  if (normalized === "opencode" || normalized === "claude" || normalized === "cursor" || normalized === "generic") {
    return normalized;
  }

  return undefined;
}

function validateSkills(requested: string[]): string[] {
  const availableSet = new Set(AVAILABLE_SKILLS.map((s) => s.name));
  const valid: string[] = [];
  const invalid: string[] = [];

  for (const skill of requested) {
    if (availableSet.has(skill)) {
      valid.push(skill);
    } else {
      invalid.push(skill);
    }
  }

  if (invalid.length > 0) {
    throw new Error(`Unknown skills: ${invalid.join(", ")}. Available: ${AVAILABLE_SKILLS.map((s) => s.name).join(", ")}`);
  }

  return valid;
}

async function installSkills(skills: string[], agentType: AgentType): Promise<number> {
  setAgentType(agentType);

  const manifest = await loadSkillsManifest();
  const serverManifest = await loadServerManifest();

  const resolvedRepo = COMMON_SKILLS_REPO;
  const resolvedRef = "main";
  const destinationPath = resolveSkillsPath(agentType);

  await syncSkillsRepository({
    repoUrl: resolvedRepo,
    ref: resolvedRef,
    destinationPath,
    sparsePaths: skills,
  });

  const serverVersion = serverManifest?.serverVersion ?? manifest?.serverVersion ?? "unknown";

  await saveSkillsManifest({
    schemaVersion: SKILLS_MANIFEST_VERSION,
    repoUrl: resolvedRepo,
    ref: resolvedRef,
    selectedSkills: skills,
    syncedAtUtc: new Date().toISOString(),
    serverVersion,
  });

  process.stdout.write(
    [
      `repo=${resolvedRepo}`,
      `ref=${resolvedRef}`,
      `local=${destinationPath}`,
      `selected_skills=${skills.join(",")}`,
    ].join("\n") + "\n"
  );

  return 0;
}

export async function skillsCommand(context: CommandContext): Promise<number> {
  if (context.parsed.options.help) {
    printSkillsHelp();
    return 0;
  }

  const args = context.parsed.args;

  if (args.length === 0 || args[0] === "install") {
    const skillsArg = context.parsed.options.skills as unknown as string | undefined;
    const agentTypeArg = context.parsed.options.agentType as unknown as string | undefined;

    const agentType = validateAgentType(agentTypeArg);
    if (!agentType) {
      process.stderr.write("--agent-type is required. Use: opencode, claude, cursor, or generic\n");
      return 1;
    }

    const skills = parseSkillsArg(skillsArg);
    if (skills.length === 0) {
      process.stderr.write("--skills is required. Provide skill names separated by commas.\n");
      return 1;
    }

    try {
      const validSkills = validateSkills(skills);
      return installSkills(validSkills, agentType);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      process.stderr.write(`${message}\n`);
      return 1;
    }
  }

  printSkillsHelp();
  return 1;
}
