import { confirm, input } from "@inquirer/prompts";

export interface InstallPromptInput {
  installDirDefault: string;
  skillsRepoDefault?: string;
  skillsRefDefault: string;
  nonInteractive: boolean;
  yes: boolean;
  installDirFromOption?: string;
  skillsRepoFromOption?: string;
  skillsRefFromOption?: string;
}

export interface InstallPromptResult {
  installDir: string;
  shouldSyncSkills: boolean;
  skillsRepo?: string;
  skillsRef?: string;
}

export async function collectInstallPromptResult(inputValues: InstallPromptInput): Promise<InstallPromptResult> {
  const installDir = inputValues.installDirFromOption ?? inputValues.installDirDefault;

  if (inputValues.nonInteractive || inputValues.yes) {
    return {
      installDir,
      shouldSyncSkills: false,
      ...(inputValues.skillsRepoFromOption ? { skillsRepo: inputValues.skillsRepoFromOption } : {}),
      ...(inputValues.skillsRefFromOption ? { skillsRef: inputValues.skillsRefFromOption } : {}),
    };
  }

  const selectedInstallDir = await input({
    message: "Installation directory",
    default: installDir,
  });

  const shouldSyncSkills = await confirm({
    message: "Download companion skills?",
    default: false,
  });

  if (!shouldSyncSkills) {
    return {
      installDir: selectedInstallDir,
      shouldSyncSkills,
    };
  }

  const skillsRepo = await input({
    message: "Companion skills repository URL",
    default: inputValues.skillsRepoFromOption ?? inputValues.skillsRepoDefault ?? "",
  });

  const skillsRef = await input({
    message: "Companion skills ref (tag/branch/commit)",
    default: inputValues.skillsRefFromOption ?? inputValues.skillsRefDefault,
  });

  return {
    installDir: selectedInstallDir,
    shouldSyncSkills,
    skillsRepo,
    skillsRef,
  };
}
