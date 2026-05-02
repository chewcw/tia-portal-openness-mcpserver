export const SCHEMA_VERSION = 1;

export interface InstalledServerState {
  schemaVersion: number;
  serverVersion: string;
  installPath: string;
  executablePath: string;
  installedAtUtc: string;
  lastUpdatedAtUtc?: string;
  rollbackCachePath?: string;
}

export interface SkillsState {
  schemaVersion: number;
  repoUrl: string;
  ref: string;
  localPath: string;
  selectedSkills: string[];
  selectedPaths: string[];
  syncedAtUtc: string;
  serverVersion: string;
}

export interface CliState {
  schemaVersion: number;
  installedServer?: InstalledServerState;
  skills?: SkillsState;
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0;
}

export function isInstalledServerState(value: unknown): value is InstalledServerState {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as Partial<InstalledServerState>;

  return (
    candidate.schemaVersion === SCHEMA_VERSION &&
    isNonEmptyString(candidate.serverVersion) &&
    isNonEmptyString(candidate.installPath) &&
    isNonEmptyString(candidate.executablePath) &&
    isNonEmptyString(candidate.installedAtUtc) &&
    (candidate.lastUpdatedAtUtc === undefined || isNonEmptyString(candidate.lastUpdatedAtUtc)) &&
    (candidate.rollbackCachePath === undefined || isNonEmptyString(candidate.rollbackCachePath))
  );
}

export function isSkillsState(value: unknown): value is SkillsState {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as Partial<SkillsState>;

  return (
    candidate.schemaVersion === SCHEMA_VERSION &&
    isNonEmptyString(candidate.repoUrl) &&
    isNonEmptyString(candidate.ref) &&
    isNonEmptyString(candidate.localPath) &&
    Array.isArray(candidate.selectedSkills) &&
    candidate.selectedSkills.length > 0 &&
    candidate.selectedSkills.every((entry) => isNonEmptyString(entry)) &&
    Array.isArray(candidate.selectedPaths) &&
    candidate.selectedPaths.length > 0 &&
    candidate.selectedPaths.every((entry) => isNonEmptyString(entry)) &&
    isNonEmptyString(candidate.syncedAtUtc) &&
    isNonEmptyString(candidate.serverVersion)
  );
}

export function isCliState(value: unknown): value is CliState {
  if (!value || typeof value !== "object") {
    return false;
  }

  const candidate = value as Partial<CliState>;

  if (candidate.schemaVersion !== SCHEMA_VERSION) {
    return false;
  }

  if (candidate.installedServer !== undefined && !isInstalledServerState(candidate.installedServer)) {
    return false;
  }

  if (candidate.skills !== undefined && !isSkillsState(candidate.skills)) {
    return false;
  }

  return true;
}

export function createEmptyState(): CliState {
  return {
    schemaVersion: SCHEMA_VERSION,
  };
}
