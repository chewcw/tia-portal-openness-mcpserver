import { mkdtemp, rm } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { loadCliState, saveCliState } from "../src/state/installStateStore.js";
import { SCHEMA_VERSION } from "../src/state/schema.js";

const tempRoots: string[] = [];

async function createTempDir(prefix: string): Promise<string> {
  const dir = await mkdtemp(path.join(os.tmpdir(), prefix));
  tempRoots.push(dir);
  return dir;
}

afterEach(async () => {
  while (tempRoots.length > 0) {
    const dir = tempRoots.pop();
    if (dir) {
      await rm(dir, { recursive: true, force: true });
    }
  }
});

describe("installStateStore", () => {
  it("returns empty state when file does not exist", async () => {
    const root = await createTempDir("tia-cli-state-");
    const statePath = path.join(root, "missing.json");

    const state = await loadCliState(statePath);

    expect(state.schemaVersion).toBe(SCHEMA_VERSION);
    expect(state.installedServer).toBeUndefined();
  });

  it("persists and reloads state", async () => {
    const root = await createTempDir("tia-cli-state-");
    const statePath = path.join(root, "state", "cli-state.json");

    await saveCliState(statePath, {
      schemaVersion: SCHEMA_VERSION,
      installedServer: {
        schemaVersion: SCHEMA_VERSION,
        serverVersion: "v1.0.0",
        installPath: "C:/server/current",
        executablePath: "C:/server/current/TiaPortalMcpServer.exe",
        installedAtUtc: "2026-03-30T00:00:00.000Z",
      },
    });

    const loaded = await loadCliState(statePath);

    expect(loaded.installedServer?.serverVersion).toBe("v1.0.0");
  });
});
