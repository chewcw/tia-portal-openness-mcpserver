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

const VALID_AGENT_TYPES: AgentType[] = ["opencode", "claude", "cursor", "generic"];

function printSkillsHelp(): void {
  const skillsList = AVAILABLE_SKILLS.map((s) => `  - ${s.name}`).join("\n");

  process.stdout.write(
    [
      "Manage companion skills",
      "",
      "Usage:",
      "  @chewcw/tia-portal-openness-mcpserver skills <command> [options]",
      "",
      "Commands:",
      "  install       Install selected skills for specified agent type(s)",
      "",
      "Options:",
      "  --skills <name[,name...]>       Skills to install (required)",
      "  --agent-type <type,...>           Agent type(s): opencode|claude|cursor|generic, comma-separated for multiple (required)",
      "  --help                          Show help",
      "",
      "Available skills:",
      skillsList,
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

function validateAgentType(raw: string | undefined): AgentType[] | undefined {
  if (!raw) {
    return undefined;
  }

  const parts = raw
    .split(",")
    .map((item) => item.trim().toLowerCase())
    .filter((item) => item.length > 0);

  if (parts.length === 0) {
    return undefined;
  }

  for (const part of parts) {
    if (!VALID_AGENT_TYPES.includes(part as AgentType)) {
      return undefined;
    }
  }

  return parts as AgentType[];
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

async function installSkills(skills: string[], agentTypes: AgentType[]): Promise<number> {
  for (const agentType of agentTypes) {
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
        `agent=${agentType}`,
        `repo=${resolvedRepo}`,
        `ref=${resolvedRef}`,
        `local=${destinationPath}`,
        `selected_skills=${skills.join(",")}`,
      ].join("\n") + "\n"
    );
  }

  return 0;
}

export async function skillsCommand(context: CommandContext): Promise<number> {
  if (context.parsed.options.help) {
    printSkillsHelp();
    return 0;
  }

  const args = context.parsed.args;
  const skillsArg = context.parsed.options.skills as unknown as string | undefined;
  const agentTypeArg = context.parsed.options.agentType as unknown as string | undefined;

  if (args.length === 0 && !skillsArg && !agentTypeArg) {
    printSkillsHelp();
    return 0;
  }

  if (args.length === 0 || args[0] === "install") {
    const skillsArg = context.parsed.options.skills as unknown as string | undefined;
    const agentTypeArg = context.parsed.options.agentType as unknown as string | undefined;

    const agentTypes = validateAgentType(agentTypeArg);
    if (!agentTypes || agentTypes.length === 0) {
      process.stderr.write("--agent-type is required. Use: opencode, claude, cursor, or generic (comma-separated for multiple)\n");
      return 1;
    }

    const skills = parseSkillsArg(skillsArg);
    if (skills.length === 0) {
      process.stderr.write("--skills is required. Provide skill names separated by commas.\n");
      return 1;
    }

    try {
      const validSkills = validateSkills(skills);
      return installSkills(validSkills, agentTypes);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      process.stderr.write(`${message}\n`);
      return 1;
    }
  }

  printSkillsHelp();
  return 1;
}
