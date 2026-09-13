import path from "node:path";
import fs from "node:fs";
import { AVAILABLE_SKILLS, DEFAULT_COMPANION_SKILL_PATHS } from "../constants.js";

export interface CompanionSkillsInstallOptions {
  skillPaths?: string[];
  environment: "global" | "local";
}

export async function installCompanionSkills(
  options: CompanionSkillsInstallOptions
): Promise<void> {
  const root = options.environment === "global"
    ? process.env.HOME || process.env.USERPROFILE || process.cwd()
    : process.cwd();

  const skillPaths = options.skillPaths || DEFAULT_COMPANION_SKILL_PATHS;

  if (AVAILABLE_SKILLS.length === 0) {
    process.stdout.write("No companion skills are configured to install.\n");
    return;
  }

  const sourceBase = path.join(__dirname, "../../assets/skills");

  for (const location of skillPaths) {
    const destination = path.resolve(root, location);
    process.stdout.write(`Installing companion skills to ${destination}\n`);
    await fs.promises.mkdir(destination, { recursive: true });

    for (const skill of AVAILABLE_SKILLS) {
      const src = path.join(sourceBase, skill.localPath);
      const dest = path.join(destination, skill.localPath);
      if (fs.existsSync(src)) {
        await fs.promises.cp(src, dest, { recursive: true, force: true });
      }
    }
  }

  process.stdout.write("Companion skills installation complete.\n");
}
