import { confirm, input, select } from "@inquirer/prompts";
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

  return {
    installDir: selectedInstallDir,
    ...(serverVersion ? { serverVersion } : {}),
  };
}
