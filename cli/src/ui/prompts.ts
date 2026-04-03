import { confirm, input, select } from "@inquirer/prompts";
import type { AgentType } from "../services/agentPathResolver.js";
import { resolveSkillsPath } from "../services/agentPathResolver.js";
import { AVAILABLE_SKILLS, COMMON_SKILLS_REPO } from "../constants.js";

export interface InstallPromptInput {
  installDirDefault: string;
  skillsRefDefault: string;
  nonInteractive: boolean;
  yes: boolean;
  installDirFromOption?: string;
  skillsRefFromOption?: string;
  skillsFromOption?: string[];
  allSkillsFromOption: boolean;
  detectedAgentType: AgentType;
  agentTypeFromOption?: string;
  latestServerVersion?: string;
  serverVersionFromOption?: string;
}

export interface InstallPromptResult {
  installDir: string;
  shouldSyncSkills: boolean;
  selectedSkills: string[];
  agentType: AgentType;
  serverVersion?: string;
  skillsRef?: string;
  skillsRepo?: string;
}

const AGENT_TYPES: AgentType[] = ["opencode", "claude", "cursor", "generic"];

async function promptAgentType(
  detected: AgentType,
  fromOption?: string
): Promise<AgentType> {
  if (fromOption) {
    return fromOption as AgentType;
  }
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

  if (inputValues.nonInteractive || inputValues.yes) {
    let selectedSkills: string[] = [];
    if (inputValues.skillsFromOption && inputValues.skillsFromOption.length > 0) {
      selectedSkills = inputValues.skillsFromOption;
    } else if (inputValues.allSkillsFromOption) {
      selectedSkills = AVAILABLE_SKILLS.map((s) => s.name);
    }
    return {
      agentType: (inputValues.agentTypeFromOption as AgentType) ?? inputValues.detectedAgentType,
      installDir: installDirDefault,
      shouldSyncSkills: selectedSkills.length > 0,
      selectedSkills,
      skillsRef: inputValues.skillsRefFromOption ?? inputValues.skillsRefDefault,
      skillsRepo: COMMON_SKILLS_REPO,
      ...(inputValues.serverVersionFromOption ? { serverVersion: inputValues.serverVersionFromOption } : {}),
    };
  }

  const agentType = await promptAgentType(
    inputValues.detectedAgentType,
    inputValues.agentTypeFromOption
  );

  const serverVersion = await promptServerVersion(
    inputValues.latestServerVersion,
    inputValues.serverVersionFromOption
  );

  const selectedInstallDir = await input({
    message: "Installation directory",
    default: installDirDefault,
  });

  let selectedSkills: string[] = [];
  if (inputValues.skillsFromOption && inputValues.skillsFromOption.length > 0) {
    selectedSkills = inputValues.skillsFromOption;
  } else if (inputValues.allSkillsFromOption) {
    selectedSkills = AVAILABLE_SKILLS.map((s) => s.name);
  } else {
    for (const skill of AVAILABLE_SKILLS) {
      const installSkill = await confirm({
        message: `Install companion skill ${skill.name}?`,
        default: false,
      });
      if (installSkill) {
        selectedSkills.push(skill.name);
      }
    }
  }

  const skillsRef = inputValues.skillsRefFromOption ?? inputValues.skillsRefDefault;

  return {
    agentType,
    installDir: selectedInstallDir,
    shouldSyncSkills: selectedSkills.length > 0,
    selectedSkills,
    skillsRef,
    skillsRepo: COMMON_SKILLS_REPO,
    ...(serverVersion ? { serverVersion } : {}),
  };
}
