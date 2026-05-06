import { describe, expect, it, vi } from "vitest";

vi.mock("@inquirer/prompts", () => ({
  confirm: vi.fn(),
  input: vi.fn(),
  select: vi.fn(),
  checkbox: vi.fn(),
}));

import * as prompts from "@inquirer/prompts";
import { collectInstallPromptResult } from "../../src/ui/prompts.js";

describe("collectInstallPromptResult", () => {
  it("returns companion skill selections when the user opts in", async () => {
    (prompts.select as unknown as ReturnType<typeof vi.fn>)
      .mockResolvedValueOnce("v1.0.0")
      .mockResolvedValueOnce("local");
    (prompts.checkbox as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce([".agents/skills"]);
    (prompts.input as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce("./install");
    (prompts.confirm as unknown as ReturnType<typeof vi.fn>).mockResolvedValueOnce(true);

    const result = await collectInstallPromptResult({
      installDirDefault: "./install",
    });

    expect(result.installCompanionSkills).toBe(true);
    expect(result.companionSkillPaths).toEqual([".agents/skills"]);
    expect(result.companionSkillsEnv).toBe("local");
  });
});
