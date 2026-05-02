import { CommandContext } from "../types.js";
import path from "node:path";
import { downloadAsset } from "../services/downloader.js";
import { extractZipToStaging } from "../services/extract.js";
import { installExtractedContent } from "../services/installTransaction.js";
import { getRepositoryFromEnv, ReleaseClient } from "../services/releases.js";
import { getDefaultStateFilePath, loadCliState, saveCliState } from "../state/installStateStore.js";
import { SCHEMA_VERSION } from "../state/schema.js";

export async function updateCommand(context: CommandContext): Promise<number> {
  const stateFilePath = getDefaultStateFilePath();
  const state = await loadCliState(stateFilePath);

  if (!state.installedServer) {
    process.stderr.write("No installed server found. Run install first.\n");
    return 1;
  }

  const repository = getRepositoryFromEnv();
  const token = process.env.GITHUB_TOKEN;
  const requestedVersion = context.parsed.options.serverVersion ?? context.parsed.args[0];

  const client = new ReleaseClient({
    repository,
    ...(token ? { token } : {}),
  });

  const release = await client.resolveRelease(requestedVersion);
  const previousVersion = state.installedServer.serverVersion;

  if (release.tagName === previousVersion) {
    process.stdout.write(`Already up to date: ${release.tagName}\n`);
    return 0;
  }

  const asset = client.resolveAsset(release);
  const installRoot = path.dirname(state.installedServer.installPath);
  const downloaded = await downloadAsset(asset, path.join(installRoot, "downloads"));
  const extracted = await extractZipToStaging(downloaded.filePath, path.join(installRoot, "tmp"));
  const transaction = await installExtractedContent(extracted.extractedPath, installRoot);

  state.installedServer = {
    schemaVersion: SCHEMA_VERSION,
    serverVersion: release.tagName,
    installPath: transaction.activePath,
    executablePath: path.join(transaction.activePath, "TiaPortalMcpServer.exe"),
    installedAtUtc: state.installedServer.installedAtUtc,
    lastUpdatedAtUtc: new Date().toISOString(),
    ...(transaction.rollbackPath ? { rollbackCachePath: transaction.rollbackPath } : {}),
  };

  await saveCliState(stateFilePath, state);

  process.stdout.write(
    [
      `repository=${repository}`,
      `from=${previousVersion}`,
      `to=${release.tagName}`,
      `installed=${transaction.activePath}`,
      `state=${stateFilePath}`,
    ].join("\n") + "\n"
  );

  return 0;
}
