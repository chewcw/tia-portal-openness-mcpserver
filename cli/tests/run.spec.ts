import { EventEmitter } from "node:events";
import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  accessMock: vi.fn(),
  spawnMock: vi.fn(),
  getDefaultStateFilePathMock: vi.fn(() => "C:/state/cli-state.json"),
  loadCliStateMock: vi.fn(),
}));

vi.mock("node:fs/promises", () => ({
  access: mocks.accessMock,
}));

vi.mock("node:child_process", () => ({
  spawn: mocks.spawnMock,
}));

vi.mock("../src/state/installStateStore.js", () => ({
  getDefaultStateFilePath: mocks.getDefaultStateFilePathMock,
  loadCliState: mocks.loadCliStateMock,
}));

import { runCommand } from "../src/commands/run.js";

class MockChildProcess extends EventEmitter {
  public readonly stderr = new EventEmitter();
  public readonly stdout = new EventEmitter();
}

describe("runCommand", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("fails when no install exists", async () => {
    mocks.loadCliStateMock.mockResolvedValue({ schemaVersion: 1 });

    const stderrSpy = vi.spyOn(process.stderr, "write").mockImplementation(() => true);

    const result = await runCommand({
      parsed: {
        name: "run",
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
    expect(stderrSpy).toHaveBeenCalledWith("No installed server found. Run install first.\n");

    stderrSpy.mockRestore();
  });

  it("propagates child process exit code", async () => {
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

    const child = new MockChildProcess();
    mocks.spawnMock.mockReturnValue(child);

    const runPromise = runCommand({
      parsed: {
        name: "run",
        args: ["--sample"],
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

    await new Promise<void>((resolve) => {
      setImmediate(() => {
        child.emit("close", 7);
        resolve();
      });
    });

    const result = await runPromise;

    expect(result).toBe(7);
    expect(mocks.spawnMock).toHaveBeenCalledWith(
      "C:/server/current/TiaPortalMcpServer.exe",
      ["--sample"],
      { stdio: "inherit" }
    );
  });
});
