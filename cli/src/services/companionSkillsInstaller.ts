import path from "node:path";
import { syncSkillsRepository } from "./skillsRepo.js";
import { COMMON_SKILLS_REPO, AVAILABLE_SKILLS } from "../constants.js";

export interface CompanionSkillsInstallOptions {
  skillPaths: string[];
  environment: "global" | "local";
}

export async function installCompanionSkills(
  options: CompanionSkillsInstallOptions
): Promise<void> {
  const root = options.environment === "global"
    ? process.env.HOME || process.env.USERPROFILE || process.cwd()
    : process.cwd();

  const skillNames = AVAILABLE_SKILLS.map((skill) => skill.name);
  if (skillNames.length === 0) {
    process.stdout.write("No companion skills are configured to install.\n");
    return;
  }

  for (const location of options.skillPaths) {
    const destination = path.resolve(root, location);
    process.stdout.write(`Installing companion skills to ${destination}\n`);
    await syncSkillsRepository({
      repoUrl: COMMON_SKILLS_REPO,
      ref: "main",
      destinationPath: destination,
      sparsePaths: skillNames,
    });
  }

  process.stdout.write("Companion skills installation complete.\n");
}
