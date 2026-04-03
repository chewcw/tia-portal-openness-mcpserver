import path from "node:path";
import { CommandContext } from "../types.js";
import { saveServerManifest, resolveInstallDir, SERVER_MANIFEST_VERSION } from "../state/serverManifest.js";
import { downloadAsset } from "../services/downloader.js";
import { extractZipToStaging } from "../services/extract.js";
import { installExtractedContent } from "../services/installTransaction.js";
import { getRepositoryFromEnv, ReleaseClient } from "../services/releases.js";
import { collectInstallPromptResult } from "../ui/prompts.js";

function printInstallHelp(): void {
  process.stdout.write(
    [
      "Install latest or selected server release",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver install [options] [version]",
      "",
      "Arguments:",
      "  version  Specific version to install (defaults to latest)",
      "",
      "Options:",
      "  --server-version <tag>  Specific version to install",
      "  --install-dir <path>    Installation directory",
      "  --help                  Show help",
      "  --version               Show version",
    ].join("\n") + "\n"
  );
}

export async function installCommand(context: CommandContext): Promise<number> {
  if (context.parsed.options.help) {
    printInstallHelp();
    return 0;
  }
  const repository = getRepositoryFromEnv();
  const versionFromArgs = context.parsed.options.serverVersion ?? context.parsed.args[0];
  const token = process.env.GITHUB_TOKEN;

  const releaseClient = new ReleaseClient({
    repository,
    ...(token ? { token } : {}),
  });

  const latestRelease = versionFromArgs
    ? undefined
    : await releaseClient.resolveRelease().catch(() => undefined);

  const promptResult = await collectInstallPromptResult({
    installDirDefault: resolveInstallDir(),
    ...(latestRelease ? { latestServerVersion: latestRelease.tagName } : {}),
    ...(versionFromArgs ? { serverVersionFromOption: versionFromArgs } : {}),
    ...(context.parsed.options.installDir ? { installDirFromOption: context.parsed.options.installDir } : {}),
  });

  if (!promptResult.serverVersion) {
    throw new Error("Server version is required. Use --server-version or specify in prompt.");
  }

  const version = promptResult.serverVersion;
  const normalizedVersion = version.startsWith('v') ? version : `v${version}`;

  const asset = {
    name: `TiaPortalMcpServer-${normalizedVersion}.zip`,
    browserDownloadUrl: `https://github.com/${repository}/releases/download/${normalizedVersion}/TiaPortalMcpServer-${normalizedVersion}.zip`,
    size: 0,
  };

  const installRoot = promptResult.installDir;

  const downloaded = await downloadAsset(asset, path.join(installRoot, "downloads"));
  const extracted = await extractZipToStaging(downloaded.filePath, path.join(installRoot, "tmp"));
  const transaction = await installExtractedContent(extracted.extractedPath, installRoot);

  const executablePath = path.join(transaction.activePath, "TiaPortalMcpServer.exe");

  await saveServerManifest({
    schemaVersion: SERVER_MANIFEST_VERSION,
    serverVersion: normalizedVersion,
    installedAtUtc: new Date().toISOString(),
  }, installRoot);

  process.stdout.write(
    [
      `repository=${repository}`,
      `tag=${normalizedVersion}`,
      `asset=${asset.name}`,
      `installed=${transaction.activePath}`,
      `executable=${executablePath}`,
    ].join("\n") + "\n"
  );

  return 0;
}
