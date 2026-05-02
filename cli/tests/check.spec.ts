import { beforeEach, describe, expect, it, vi } from "vitest";
import { MissingDependencyError } from "../src/errors.js";

const mocks = vi.hoisted(() => ({
  accessMock: vi.fn(),
  ensureGitAvailableMock: vi.fn(),
  getDefaultStateFilePathMock: vi.fn(() => "C:/state/cli-state.json"),
  loadCliStateMock: vi.fn(),
}));

vi.mock("node:fs/promises", () => ({
  access: mocks.accessMock,
}));

vi.mock("../src/services/prereqCheck.js", () => ({
  ensureGitAvailable: mocks.ensureGitAvailableMock,
}));

vi.mock("../src/state/installStateStore.js", () => ({
  getDefaultStateFilePath: mocks.getDefaultStateFilePathMock,
  loadCliState: mocks.loadCliStateMock,
}));

import { checkCommand } from "../src/commands/check.js";

describe("checkCommand", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("returns 0 when state exists but server is not installed", async () => {
    mocks.loadCliStateMock.mockResolvedValue({
      schemaVersion: 1,
    });
    mocks.ensureGitAvailableMock.mockRejectedValue(new MissingDependencyError("git"));

    const stdoutSpy = vi.spyOn(process.stdout, "write").mockImplementation(() => true);

    const result = await checkCommand({
      parsed: {
        name: "check",
        args: [],
        options: {
          help: false,
          version: false,
          yes: false,
          nonInteractive: false,
          verbose: false,
          allSkills: false,
        },
      },
    });

    expect(result).toBe(0);
    expect(stdoutSpy).toHaveBeenCalledWith(expect.stringContaining("install: INFO"));

    stdoutSpy.mockRestore();
  });

  it("returns 0 when installed executable exists", async () => {
    mocks.loadCliStateMock.mockResolvedValue({
      schemaVersion: 1,
      installedServer: {
        schemaVersion: 1,
        serverVersion: "v1.0.0",
        installPath: "C:/server/current",
        executablePath: "C:/server/current/TiaPortalMcpServer.exe",
        installedAtUtc: "2026-03-31T00:00:00.000Z",
      },
    });
    mocks.accessMock.mockResolvedValue(undefined);
    mocks.ensureGitAvailableMock.mockResolvedValue(undefined);

    const result = await checkCommand({
      parsed: {
        name: "check",
        args: [],
        options: {
          help: false,
          version: false,
          yes: false,
          nonInteractive: false,
          verbose: false,
          allSkills: false,
        },
      },
    });

    expect(result).toBe(0);
    expect(mocks.accessMock).toHaveBeenCalledWith("C:/server/current/TiaPortalMcpServer.exe");
  });

  it("returns 1 when state load fails", async () => {
    mocks.loadCliStateMock.mockRejectedValue(new Error("Invalid state schema"));
    mocks.ensureGitAvailableMock.mockResolvedValue(undefined);

    const result = await checkCommand({
      parsed: {
        name: "check",
        args: [],
        options: {
          help: false,
          version: false,
          yes: false,
          nonInteractive: false,
          verbose: false,
          allSkills: false,
        },
      },
    });

    expect(result).toBe(1);
  });

  it("returns 1 when installed executable is missing", async () => {
    mocks.loadCliStateMock.mockResolvedValue({
      schemaVersion: 1,
      installedServer: {
        schemaVersion: 1,
        serverVersion: "v1.0.0",
        installPath: "C:/server/current",
        executablePath: "C:/server/current/TiaPortalMcpServer.exe",
        installedAtUtc: "2026-03-31T00:00:00.000Z",
      },
    });
    mocks.accessMock.mockRejectedValue(new Error("missing"));
    mocks.ensureGitAvailableMock.mockResolvedValue(undefined);

    const result = await checkCommand({
      parsed: {
        name: "check",
        args: [],
        options: {
          help: false,
          version: false,
          yes: false,
          nonInteractive: false,
          verbose: false,
          allSkills: false,
        },
      },
    });

    expect(result).toBe(1);
  });
});
