import { mkdir, readFile, writeFile, access } from "node:fs/promises";
import path from "node:path";
import { getSkillsPath } from "../services/agentPathResolver.js";

export const SKILLS_MANIFEST_VERSION = 1;

export interface SkillsManifest {
  schemaVersion: number;
  repoUrl: string;
  ref: string;
  selectedSkills: string[];
  syncedAtUtc: string;
  serverVersion: string;
}

const MANIFEST_FILENAME = ".tia-portal-skills-manifest.json";

export function resolveSkillsManifestPath(skillsPath?: string): string {
  return path.join(skillsPath ?? getSkillsPathFromEnvOrDefault(), MANIFEST_FILENAME);
}

function getSkillsPathFromEnvOrDefault(): string {
  const envPath = process.env.TIA_MCP_SKILLS_PATH;
  if (envPath && envPath.length > 0) {
    return envPath;
  }
  return getSkillsPath();
}

export async function loadSkillsManifest(skillsPath?: string): Promise<SkillsManifest | null> {
  const manifestPath = resolveSkillsManifestPath(skillsPath);
  try {
    const json = await readFile(manifestPath, "utf8");
    const parsed = JSON.parse(json) as unknown;
    if (!isSkillsManifest(parsed)) {
      return null;
    }
    return parsed;
  } catch (error: unknown) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code === "ENOENT") {
      return null;
    }
    throw error;
  }
}

export async function saveSkillsManifest(
  manifest: SkillsManifest,
  skillsPath?: string
): Promise<string> {
  const manifestPath = resolveSkillsManifestPath(skillsPath);
  const dir = path.dirname(manifestPath);
  await mkdir(dir, { recursive: true });
  await writeFile(manifestPath, JSON.stringify(manifest, null, 2) + "\n", "utf8");
  return manifestPath;
}

function isSkillsManifest(value: unknown): value is SkillsManifest {
  if (!value || typeof value !== "object") {
    return false;
  }
  const candidate = value as Partial<SkillsManifest>;
  return (
    candidate.schemaVersion === SKILLS_MANIFEST_VERSION &&
    typeof candidate.repoUrl === "string" &&
    candidate.repoUrl.length > 0 &&
    typeof candidate.ref === "string" &&
    candidate.ref.length > 0 &&
    Array.isArray(candidate.selectedSkills) &&
    candidate.selectedSkills.every((s) => typeof s === "string" && s.length > 0) &&
    typeof candidate.syncedAtUtc === "string" &&
    candidate.syncedAtUtc.length > 0 &&
    typeof candidate.serverVersion === "string" &&
    candidate.serverVersion.length > 0
  );
}

export async function skillsManifestExists(skillsPath?: string): Promise<boolean> {
  const manifestPath = resolveSkillsManifestPath(skillsPath);
  try {
    await access(manifestPath);
    return true;
  } catch {
    return false;
  }
}
