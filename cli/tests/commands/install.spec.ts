import { describe, expect, it, vi } from "vitest";
import path from "node:path";
import { installCommand } from "../../src/commands/install.js";
import * as prompts from "../../src/ui/prompts.js";
import * as installer from "../../src/services/companionSkillsInstaller.js";
import * as downloader from "../../src/services/downloader.js";
import * as extract from "../../src/services/extract.js";
import * as installTransaction from "../../src/services/installTransaction.js";
import * as state from "../../src/state/serverManifest.js";

describe("installCommand", () => {
  it("calls companion skill installer when user selects yes", async () => {
    vi.spyOn(prompts, "collectInstallPromptResult").mockResolvedValue({
      installDir: ".",
      serverVersion: "v1.0.0",
      installCompanionSkills: true,
      companionSkillPaths: [".agents/skills"],
      companionSkillsEnv: "local",
    } as never);
    vi.spyOn(installer, "installCompanionSkills").mockResolvedValue();
    vi.spyOn(downloader, "downloadAsset").mockResolvedValue({ filePath: path.join(".", "downloads", "dummy.zip") } as never);
    vi.spyOn(extract, "extractZipToStaging").mockResolvedValue({ extractedPath: path.join(".", "tmp", "extracted") } as never);
    vi.spyOn(installTransaction, "installExtractedContent").mockResolvedValue({ activePath: path.join(".", "server") } as never);
    vi.spyOn(state, "saveServerManifest").mockResolvedValue();

    const exitCode = await installCommand({
      parsed: {
        name: "install",
        args: [],
        options: {
          help: false,
          version: false,
          serverVersion: "v1.0.0",
          installDir: ".",
        },
      },
    });

    expect(exitCode).toBe(0);
    expect(installer.installCompanionSkills).toHaveBeenCalledWith({
      skillPaths: [".agents/skills"],
      environment: "local",
    });
  });
});
