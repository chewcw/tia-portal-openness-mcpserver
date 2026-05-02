import { mkdir, readFile, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";

export const SERVER_MANIFEST_VERSION = 1;

export interface ServerManifest {
  schemaVersion: number;
  serverVersion: string;
  installedAtUtc: string;
}

export function getDefaultInstallRoot(): string {
  const appData = process.env.APPDATA ?? path.join(os.homedir(), "AppData", "Roaming");
  return path.join(appData, "TiaPortalMcpServerCli", "server");
}

export function getInstallDirFromEnv(): string | undefined {
  const envPath = process.env.TIA_MCP_INSTALL_DIR;
  if (envPath && envPath.length > 0) {
    return envPath;
  }
  return undefined;
}

export function resolveInstallDir(customPath?: string): string {
  return customPath ?? getInstallDirFromEnv() ?? getDefaultInstallRoot();
}

export function resolveServerManifestPath(installDir?: string): string {
  const dir = resolveInstallDir(installDir);
  return path.join(dir, "current", "manifest.json");
}

export async function loadServerManifest(installDir?: string): Promise<ServerManifest | null> {
  const manifestPath = resolveServerManifestPath(installDir);
  try {
    const json = await readFile(manifestPath, "utf8");
    const parsed = JSON.parse(json) as unknown;
    if (!isServerManifest(parsed)) {
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

export async function saveServerManifest(
  manifest: ServerManifest,
  installDir?: string
): Promise<string> {
  const manifestPath = resolveServerManifestPath(installDir);
  const dir = path.dirname(manifestPath);
  await mkdir(dir, { recursive: true });
  await writeFile(manifestPath, JSON.stringify(manifest, null, 2) + "\n", "utf8");
  return manifestPath;
}

function isServerManifest(value: unknown): value is ServerManifest {
  if (!value || typeof value !== "object") {
    return false;
  }
  const candidate = value as Partial<ServerManifest>;
  return (
    candidate.schemaVersion === SERVER_MANIFEST_VERSION &&
    typeof candidate.serverVersion === "string" &&
    candidate.serverVersion.length > 0 &&
    typeof candidate.installedAtUtc === "string" &&
    candidate.installedAtUtc.length > 0
  );
}
