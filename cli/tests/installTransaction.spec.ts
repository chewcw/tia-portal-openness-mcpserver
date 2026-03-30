import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { installExtractedContent } from "../src/services/installTransaction.js";

const tempRoots: string[] = [];

async function createTempDir(prefix: string): Promise<string> {
  const { mkdtemp } = await import("node:fs/promises");
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

describe("installExtractedContent", () => {
  it("activates extracted content when no previous install exists", async () => {
    const root = await createTempDir("tia-cli-install-");
    const extracted = path.join(root, "extracted");
    const installRoot = path.join(root, "server");

    await mkdir(extracted, { recursive: true });
    await writeFile(path.join(extracted, "version.txt"), "v1", "utf8");

    const result = await installExtractedContent(extracted, installRoot);
    const content = await readFile(path.join(result.activePath, "version.txt"), "utf8");

    expect(content).toBe("v1");
    expect(result.rollbackPath).toBeUndefined();
  });

  it("creates rollback when replacing an existing active install", async () => {
    const root = await createTempDir("tia-cli-install-");
    const installRoot = path.join(root, "server");
    const active = path.join(installRoot, "current");
    const extracted = path.join(root, "extracted");

    await mkdir(active, { recursive: true });
    await writeFile(path.join(active, "version.txt"), "v1", "utf8");

    await mkdir(extracted, { recursive: true });
    await writeFile(path.join(extracted, "version.txt"), "v2", "utf8");

    const result = await installExtractedContent(extracted, installRoot);
    const current = await readFile(path.join(result.activePath, "version.txt"), "utf8");

    expect(current).toBe("v2");
    expect(result.rollbackPath).toBeTruthy();

    if (result.rollbackPath) {
      const previous = await readFile(path.join(result.rollbackPath, "version.txt"), "utf8");
      expect(previous).toBe("v1");
    }
  });
});
