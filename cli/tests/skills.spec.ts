import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  readdirMock: vi.fn(),
  syncSkillsRepositoryMock: vi.fn(),
  listDirectoryEntriesAtRefMock: vi.fn(),
  getSkillsPathMock: vi.fn(() => "C:/skills"),
  loadSkillsManifestMock: vi.fn(),
  saveSkillsManifestMock: vi.fn(),
  loadServerManifestMock: vi.fn(),
}));

vi.mock("node:fs/promises", () => ({
  readdir: mocks.readdirMock,
}));

vi.mock("../src/services/skillsRepo.js", () => ({
  syncSkillsRepository: mocks.syncSkillsRepositoryMock,
  listDirectoryEntriesAtRef: mocks.listDirectoryEntriesAtRefMock,
}));

vi.mock("../src/state/skillsManifest.js", () => ({
  loadSkillsManifest: mocks.loadSkillsManifestMock,
  saveSkillsManifest: mocks.saveSkillsManifestMock,
  SKILLS_MANIFEST_VERSION: 1,
}));

vi.mock("../src/state/serverManifest.js", () => ({
  loadServerManifest: mocks.loadServerManifestMock,
}));

vi.mock("../src/services/agentPathResolver.js", () => ({
  getSkillsPath: mocks.getSkillsPathMock,
}));

import { skillsCommand } from "../src/commands/skills.js";

function baseOptions(overrides?: Partial<{ skillsRepo: string; skillsRef: string; skills: string[]; allSkills: boolean }>) {
  return {
    help: false,
    version: false,
    yes: false,
    nonInteractive: false,
    verbose: false,
    allSkills: false,
    ...overrides,
  };
}

describe("skillsCommand", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.syncSkillsRepositoryMock.mockResolvedValue({
      destinationPath: "C:/skills",
      ref: "main",
      sparsePaths: [],
    });
  });

  it("status reports when skills metadata is missing", async () => {
    mocks.loadSkillsManifestMock.mockResolvedValue(null);

    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["status"],
        options: baseOptions(),
      },
    });

    expect(result).toBe(0);
    expect(stdoutSpy).toHaveBeenCalledWith("No skills metadata found. Run 'skills sync' first.\n");

    stdoutSpy.mockRestore();
  });

  it("status prints persisted metadata", async () => {
    mocks.loadSkillsManifestMock.mockResolvedValue({
      schemaVersion: 1,
      repoUrl: "https://github.com/acme/skills",
      ref: "main",
      selectedSkills: ["a", "b"],
      syncedAtUtc: "2026-03-31T00:00:00.000Z",
      serverVersion: "v1.2.3",
    });

    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: [],
        options: baseOptions(),
      },
    });

    expect(result).toBe(0);
    expect(stdoutSpy).toHaveBeenCalledWith(expect.stringContaining("selected_skills=a,b"));

    stdoutSpy.mockRestore();
  });

  it("sync resolves repo and ref precedence from flags", async () => {
    mocks.loadSkillsManifestMock.mockResolvedValue({
      schemaVersion: 1,
      repoUrl: "https://github.com/old/repo",
      ref: "old-ref",
      selectedSkills: ["old"],
      syncedAtUtc: "2026-03-30T00:00:00.000Z",
      serverVersion: "v1.0.0",
    });
    mocks.loadServerManifestMock.mockResolvedValue({
      schemaVersion: 1,
      serverVersion: "v9.9.9",
      installedAtUtc: "2026-03-31T00:00:00.000Z",
    });

    mocks.listDirectoryEntriesAtRefMock.mockResolvedValue(["alpha", "beta"]);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["sync"],
        options: baseOptions({
          skillsRepo: "https://github.com/new/repo",
          skillsRef: "feature/ref",
          skills: ["beta", "alpha"],
        }),
      },
    });

    expect(result).toBe(0);
    expect(mocks.listDirectoryEntriesAtRefMock).toHaveBeenCalledWith("C:/skills", "feature/ref");
    expect(mocks.syncSkillsRepositoryMock).toHaveBeenNthCalledWith(2, {
      repoUrl: "https://github.com/new/repo",
      ref: "feature/ref",
      destinationPath: "C:/skills",
      sparsePaths: ["alpha", "beta"],
    });
  });

  it("sync falls back to installed server version when no ref provided", async () => {
    mocks.loadSkillsManifestMock.mockResolvedValue({
      schemaVersion: 1,
      repoUrl: "https://github.com/acme/skills",
      ref: "main",
      selectedSkills: ["a"],
      syncedAtUtc: "2026-03-30T00:00:00.000Z",
      serverVersion: "v1.0.0",
    });
    mocks.loadServerManifestMock.mockResolvedValue({
      schemaVersion: 1,
      serverVersion: "v2.1.0",
      installedAtUtc: "2026-03-31T00:00:00.000Z",
    });

    mocks.listDirectoryEntriesAtRefMock.mockResolvedValue(["alpha"]);

    const result = await skillsCommand({
      parsed: {
        name: "sync",
        args: ["sync"],
        options: baseOptions({
          skillsRepo: "https://github.com/acme/skills/tree/main",
          skillsRef: "",
        }),
      },
    });

    expect(result).toBe(0);
    expect(mocks.syncSkillsRepositoryMock).toHaveBeenNthCalledWith(1, {
      repoUrl: "https://github.com/acme/skills",
      ref: "main",
      destinationPath: "C:/skills",
    });
  });

  it("sync rejects unknown skill names", async () => {
    mocks.loadSkillsManifestMock.mockResolvedValue({
      schemaVersion: 1,
      repoUrl: "https://github.com/acme/skills",
      ref: "main",
      selectedSkills: ["a"],
      syncedAtUtc: "2026-03-30T00:00:00.000Z",
      serverVersion: "v1.0.0",
    });

    mocks.listDirectoryEntriesAtRefMock.mockResolvedValue(["alpha", "beta"]);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["sync"],
        options: baseOptions({
          skills: ["missing"],
        }),
      },
    });

    expect(result).toBe(1);
    expect(mocks.saveSkillsManifestMock).not.toHaveBeenCalled();
  });

  it("list returns discovered folders", async () => {
    mocks.readdirMock.mockResolvedValue([
      { isDirectory: () => true, name: "diag" },
      { isDirectory: () => true, name: "tia" },
      { isDirectory: () => false, name: "README.md" },
    ]);

    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["list"],
        options: baseOptions(),
      },
    });

    expect(result).toBe(0);
    expect(stdoutSpy).toHaveBeenCalledWith("diag\ntia\n");

    stdoutSpy.mockRestore();
  });
});
