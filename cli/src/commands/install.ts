import { CommandContext } from "../types.js";
import os from "node:os";
import path from "node:path";
import { downloadAsset } from "../services/downloader.js";
import { extractZipToStaging } from "../services/extract.js";
import { installExtractedContent } from "../services/installTransaction.js";
import { getRepositoryFromEnv, ReleaseClient } from "../services/releases.js";
import { syncSkillsRepository } from "../services/skillsRepo.js";
import { getDefaultStateFilePath, loadCliState, saveCliState } from "../state/installStateStore.js";
import { saveSkillsState } from "../state/skillsStateStore.js";
import { SCHEMA_VERSION } from "../state/schema.js";
import { collectInstallPromptResult } from "../ui/prompts.js";

function getDefaultInstallRoot(): string {
  const appData = process.env.APPDATA ?? path.join(os.homedir(), "AppData", "Roaming");
  return path.join(appData, "TiaPortalMcpServerCli", "server");
}

function getDefaultSkillsPath(): string {
  const appData = process.env.APPDATA ?? path.join(os.homedir(), "AppData", "Roaming");
  return path.join(appData, "TiaPortalMcpServerCli", "skills");
}

export async function installCommand(context: CommandContext): Promise<number> {
  const repository = getRepositoryFromEnv();
  const version = context.parsed.options.serverVersion ?? context.parsed.args[0];
  const token = process.env.GITHUB_TOKEN;

  const releaseClient = new ReleaseClient({
    repository,
    ...(token ? { token } : {}),
  });

  const release = await releaseClient.resolveRelease(version);
  const promptResult = await collectInstallPromptResult({
    installDirDefault: getDefaultInstallRoot(),
    skillsRefDefault: release.tagName,
    nonInteractive: context.parsed.options.nonInteractive,
    yes: context.parsed.options.yes,
    ...(process.env.TIA_MCP_SKILLS_REPO ? { skillsRepoDefault: process.env.TIA_MCP_SKILLS_REPO } : {}),
    ...(context.parsed.options.installDir ? { installDirFromOption: context.parsed.options.installDir } : {}),
    ...(context.parsed.options.skillsRepo ? { skillsRepoFromOption: context.parsed.options.skillsRepo } : {}),
    ...(context.parsed.options.skillsRef ? { skillsRefFromOption: context.parsed.options.skillsRef } : {}),
  });

  const installRoot = promptResult.installDir;

  const asset = releaseClient.resolveAsset(release);
  const downloaded = await downloadAsset(asset, path.join(installRoot, "downloads"));
  const extracted = await extractZipToStaging(downloaded.filePath, path.join(installRoot, "tmp"));
  const transaction = await installExtractedContent(extracted.extractedPath, installRoot);

  const stateFilePath = getDefaultStateFilePath();
  const state = await loadCliState(stateFilePath);

  state.installedServer = {
    schemaVersion: SCHEMA_VERSION,
    serverVersion: release.tagName,
    installPath: transaction.activePath,
    executablePath: path.join(transaction.activePath, "TiaPortalMcpServer.exe"),
    installedAtUtc: new Date().toISOString(),
    ...(transaction.rollbackPath ? { rollbackCachePath: transaction.rollbackPath } : {}),
  };

  await saveCliState(stateFilePath, state);

  let syncedSkillsPath: string | undefined;

  if (promptResult.shouldSyncSkills) {
    const repoUrl = promptResult.skillsRepo?.trim();
    const ref = promptResult.skillsRef?.trim() ?? release.tagName;

    if (!repoUrl) {
      throw new Error("Skills sync selected but no skills repository URL was provided.");
    }

    const skillsResult = await syncSkillsRepository({
      repoUrl,
      ref,
      destinationPath: getDefaultSkillsPath(),
    });

    syncedSkillsPath = skillsResult.destinationPath;

    await saveSkillsState(
      {
        schemaVersion: SCHEMA_VERSION,
        repoUrl,
        ref,
        localPath: skillsResult.destinationPath,
        selectedSkills: ["all"],
        selectedPaths: ["."],
        syncedAtUtc: new Date().toISOString(),
        serverVersion: release.tagName,
      },
      stateFilePath
    );
  }

  process.stdout.write(
    [
      `repository=${repository}`,
      `tag=${release.tagName}`,
      `asset=${asset.name}`,
      `installed=${transaction.activePath}`,
      `state=${stateFilePath}`,
      `skills_selected=${promptResult.shouldSyncSkills}`,
      ...(promptResult.skillsRepo ? [`skills_repo=${promptResult.skillsRepo}`] : []),
      ...(promptResult.skillsRef ? [`skills_ref=${promptResult.skillsRef}`] : []),
      ...(syncedSkillsPath ? [`skills_path=${syncedSkillsPath}`] : []),
    ].join("\n") + "\n"
  );

  return 0;
}
