import { checkbox, confirm, input, select } from "@inquirer/prompts";
import { resolveSkillsPath } from "../services/agentPathResolver.js";
import { AVAILABLE_SKILLS, COMMON_SKILLS_REPO } from "../constants.js";

export interface InstallPromptInput {
  installDirDefault: string;
  latestServerVersion?: string;
  serverVersionFromOption?: string;
  installDirFromOption?: string;
}

export interface InstallPromptResult {
  installDir: string;
  serverVersion?: string;
  installCompanionSkills: boolean;
  companionSkillPaths: string[];
  companionSkillsEnv: "global" | "local";
}

const AGENT_TYPES = ["opencode", "claude", "cursor", "generic"] as const;
type AgentType = typeof AGENT_TYPES[number];

async function promptAgentType(detected: AgentType): Promise<AgentType> {
  const selected = await select<AgentType>({
    message: "Select target agent:",
    choices: AGENT_TYPES.map((t) => ({ name: t, value: t })),
    default: detected,
  });
  process.stdout.write(`The skill will be installed to ${resolveSkillsPath(selected)}/\n`);
  return selected;
}

const CUSTOM_VERSION_MARKER = "__custom__";

const SKILL_PATH_CHOICES = [
  { name: ".agents/skills", value: ".agents/skills", checked: true },
  { name: ".claude/skills", value: ".claude/skills" },
  { name: ".codex/skills", value: ".codex/skills" },
  { name: ".cursor/skills", value: ".cursor/skills" },
  { name: ".copilot/skills", value: ".copilot/skills" },
  { name: ".github/skills", value: ".github/skills" },
  { name: ".windsurf/skills", value: ".windsurf/skills" },
  { name: ".antigravity/skills", value: ".antigravity/skills" },
];

async function promptInstallCompanionSkills(): Promise<boolean> {
  return confirm({
    message: "Install companion skills along with the MCP server?",
    default: true,
  });
}

async function promptCompanionSkillPaths(): Promise<string[]> {
  return checkbox<string>({
    message: "Select companion skill installation locations:",
    choices: SKILL_PATH_CHOICES,
    validate: (value) =>
      value.length > 0 || "Select at least one installation location",
  });
}

async function promptCompanionSkillsEnvironment(): Promise<"global" | "local"> {
  return select<"global" | "local">({
    message: "Install selected companion skills in:",
    choices: [
      { name: "Global environment (user home directory)", value: "global" },
      { name: "Local environment (current working directory)", value: "local" },
    ],
    default: "local",
  });
}

async function promptServerVersion(
  latestTag?: string,
  fromOption?: string
): Promise<string | undefined> {
  if (fromOption) {
    return fromOption;
  }
  const latestLabel = latestTag ? `Latest (${latestTag})` : "Latest";
  const chosen = await select<string>({
    message: "Server version to install:",
    choices: [
      { name: latestLabel, value: latestTag ?? "latest" },
      { name: "Enter specific version...", value: CUSTOM_VERSION_MARKER },
    ],
    default: latestTag ?? "latest",
  });
  if (chosen === CUSTOM_VERSION_MARKER) {
    return input({
      message: "Enter server version tag (e.g. v1.0.0):",
      validate: (v) => v.trim().length > 0 || "Version tag cannot be empty",
    });
  }
  return chosen;
}

export async function collectInstallPromptResult(
  inputValues: InstallPromptInput
): Promise<InstallPromptResult> {
  const installDirDefault = inputValues.installDirFromOption ?? inputValues.installDirDefault;

  const serverVersion = await promptServerVersion(
    inputValues.latestServerVersion,
    inputValues.serverVersionFromOption
  );

  const selectedInstallDir = await input({
    message: "Installation directory",
    default: installDirDefault,
  });

  const installCompanionSkills = await promptInstallCompanionSkills();
  const companionSkillPaths = installCompanionSkills
    ? await promptCompanionSkillPaths()
    : [];
  const companionSkillsEnv = installCompanionSkills
    ? await promptCompanionSkillsEnvironment()
    : "local";

  return {
    installDir: selectedInstallDir,
    ...(serverVersion ? { serverVersion } : {}),
    installCompanionSkills,
    companionSkillPaths,
    companionSkillsEnv,
  };
}
