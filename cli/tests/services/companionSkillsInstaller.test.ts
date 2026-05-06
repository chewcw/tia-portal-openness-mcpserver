import { describe, expect, it, vi } from "vitest";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";

vi.mock("../../src/services/skillsRepo.js", () => {
  const { mkdir } = require("node:fs/promises");
  const syncSkillsRepository = vi.fn(async (options) => {
    await mkdir(options.destinationPath, { recursive: true });
    return {
      destinationPath: options.destinationPath,
      ref: options.ref,
      sparsePaths: options.sparsePaths ?? [],
    };
  });

  return { syncSkillsRepository };
});

import { installCompanionSkills } from "../../src/services/companionSkillsInstaller.js";
import { syncSkillsRepository } from "../../src/services/skillsRepo.js";

describe("installCompanionSkills", () => {
  it("syncs companion skills into selected local skill directories", async () => {
    const originalCwd = process.cwd();
    const tmpDir = path.join(os.tmpdir(), `companion-skills-test-${Date.now()}`);
    fs.mkdirSync(tmpDir, { recursive: true });

    try {
      process.chdir(tmpDir);
      await installCompanionSkills({
        skillPaths: [".agents/skills"],
        environment: "local",
      });

      expect(syncSkillsRepository).toHaveBeenCalledWith({
        repoUrl: "https://github.com/chewcw/agent-skills",
        ref: "main",
        destinationPath: path.resolve(tmpDir, ".agents/skills"),
        sparsePaths: expect.any(Array),
      });
      expect(fs.existsSync(path.join(tmpDir, ".agents", "skills"))).toBe(true);
    } finally {
      process.chdir(originalCwd);
      fs.rmSync(tmpDir, { recursive: true, force: true });
    }
  });
});
