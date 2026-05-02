import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  syncSkillsRepositoryMock: vi.fn(),
  getSkillsPathMock: vi.fn(() => "C:/skills"),
  loadSkillsManifestMock: vi.fn(),
  saveSkillsManifestMock: vi.fn(),
  loadServerManifestMock: vi.fn(),
  setAgentTypeMock: vi.fn(),
  resolveSkillsPathMock: vi.fn((type: string) => `C:/${type}-skills`),
}));

vi.mock("../src/services/skillsRepo.js", () => ({
  syncSkillsRepository: mocks.syncSkillsRepositoryMock,
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
  setAgentType: mocks.setAgentTypeMock,
  resolveSkillsPath: mocks.resolveSkillsPathMock,
}));

import { skillsCommand } from "../src/commands/skills.js";

function baseOptions(overrides?: Partial<{ skills: string; agentType: string; help: boolean }>) {
  return {
    help: false,
    version: false,
    serverVersion: undefined,
    installDir: undefined,
    skills: undefined,
    agentType: undefined,
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
    mocks.loadServerManifestMock.mockResolvedValue({
      schemaVersion: 1,
      serverVersion: "v1.0.0",
      installedAtUtc: "2026-03-31T00:00:00.000Z",
    });
  });

  it("install requires --agent-type", async () => {
    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["install"],
        options: baseOptions({
          skills: "siemens-awl-stl-programmer",
          agentType: "",
        }),
      },
    });

    expect(result).toBe(1);
    expect(mocks.setAgentTypeMock).not.toHaveBeenCalled();
  });

  it("install requires --skills", async () => {
    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["install"],
        options: baseOptions({
          skills: "",
          agentType: "opencode",
        }),
      },
    });

    expect(result).toBe(1);
    expect(mocks.syncSkillsRepositoryMock).not.toHaveBeenCalled();
  });

  it("install rejects unknown skill names", async () => {
    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["install"],
        options: baseOptions({
          skills: "unknown-skill",
          agentType: "opencode",
        }),
      },
    });

    expect(result).toBe(1);
    expect(mocks.syncSkillsRepositoryMock).not.toHaveBeenCalled();
  });

  it("install succeeds with valid skills", async () => {
    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["install"],
        options: baseOptions({
          skills: "siemens-awl-stl-programmer,siemens-tia-portal-integrator",
          agentType: "opencode",
        }),
      },
    });

    expect(result).toBe(0);
    expect(mocks.setAgentTypeMock).toHaveBeenCalledWith("opencode");
    expect(mocks.syncSkillsRepositoryMock).toHaveBeenCalledWith({
      repoUrl: "https://github.com/chewcw/agent-skills",
      ref: "main",
      destinationPath: "C:/opencode-skills",
      sparsePaths: ["siemens-awl-stl-programmer", "siemens-tia-portal-integrator"],
    });
    expect(mocks.saveSkillsManifestMock).toHaveBeenCalled();

    stdoutSpy.mockRestore();
  });

  it("install succeeds with multiple agent types", async () => {
    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: ["install"],
        options: baseOptions({
          skills: "siemens-awl-stl-programmer",
          agentType: "generic, claude",
        }),
      },
    });

    expect(result).toBe(0);
    expect(mocks.setAgentTypeMock).toHaveBeenCalledTimes(2);
    expect(mocks.setAgentTypeMock).toHaveBeenNthCalledWith(1, "generic");
    expect(mocks.setAgentTypeMock).toHaveBeenNthCalledWith(2, "claude");
    expect(mocks.syncSkillsRepositoryMock).toHaveBeenCalledTimes(2);
    expect(mocks.syncSkillsRepositoryMock).toHaveBeenNthCalledWith(1, {
      repoUrl: "https://github.com/chewcw/agent-skills",
      ref: "main",
      destinationPath: "C:/generic-skills",
      sparsePaths: ["siemens-awl-stl-programmer"],
    });
    expect(mocks.syncSkillsRepositoryMock).toHaveBeenNthCalledWith(2, {
      repoUrl: "https://github.com/chewcw/agent-skills",
      ref: "main",
      destinationPath: "C:/claude-skills",
      sparsePaths: ["siemens-awl-stl-programmer"],
    });
    expect(mocks.saveSkillsManifestMock).toHaveBeenCalledTimes(2);

    stdoutSpy.mockRestore();
  });

  it("install works without explicit subcommand", async () => {
    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: [],
        options: baseOptions({
          skills: "siemens-awl-stl-programmer",
          agentType: "claude",
        }),
      },
    });

    expect(result).toBe(0);
    expect(mocks.setAgentTypeMock).toHaveBeenCalledWith("claude");

    stdoutSpy.mockRestore();
  });

  it("shows help when --help is provided", async () => {
    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await skillsCommand({
      parsed: {
        name: "skills",
        args: [],
        options: baseOptions({ help: true }),
      },
    });

    expect(result).toBe(0);
    expect(stdoutSpy).toHaveBeenCalledWith(expect.stringContaining("--skills"));
    expect(stdoutSpy).toHaveBeenCalledWith(expect.stringContaining("--agent-type"));

    stdoutSpy.mockRestore();
  });
});
