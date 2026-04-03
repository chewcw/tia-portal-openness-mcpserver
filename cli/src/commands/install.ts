import path from "node:path";
import { CommandContext } from "../types.js";
import { saveServerManifest, resolveInstallDir, SERVER_MANIFEST_VERSION } from "../state/serverManifest.js";
import { saveSkillsManifest, resolveSkillsManifestPath } from "../state/skillsManifest.js";
import { downloadAsset } from "../services/downloader.js";
import { extractZipToStaging } from "../services/extract.js";
import { installExtractedContent } from "../services/installTransaction.js";
import { getRepositoryFromEnv, ReleaseClient } from "../services/releases.js";
import { syncSkillsRepository } from "../services/skillsRepo.js";
import { collectInstallPromptResult } from "../ui/prompts.js";
import { detectAgentType, getSkillsPath, setAgentType } from "../services/agentPathResolver.js";
import { COMMON_SKILLS_REPO } from "../constants.js";

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
      "  --skills-repo <url>     Skills repository URL",
      "  --skills-ref <ref>      Git reference (branch, tag, commit)",
      "  --skills <name[,name...]>  Specific skills to install",
      "  --all                   Install all skills",
      "  --yes                   Accept prompt defaults",
      "  --non-interactive       Disable prompts",
      "  --verbose               Verbose output",
      "  --agent-type <type>     Agent type (opencode|claude|cursor|generic)",
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

  const hasExplicitOptions =
    context.parsed.options.serverVersion !== undefined ||
    context.parsed.options.installDir !== undefined ||
    context.parsed.options.skillsRepo !== undefined ||
    context.parsed.options.skillsRef !== undefined ||
    context.parsed.options.skills !== undefined ||
    context.parsed.options.allSkills ||
    context.parsed.options.agentType !== undefined ||
    context.parsed.args.length > 0;

  const promptResult = await collectInstallPromptResult({
    installDirDefault: resolveInstallDir(),
    skillsRefDefault: latestRelease?.tagName ?? "main",
    nonInteractive: context.parsed.options.nonInteractive || hasExplicitOptions,
    yes: context.parsed.options.yes,
    detectedAgentType: detectAgentType(),
    ...(context.parsed.options.agentType ? { agentTypeFromOption: context.parsed.options.agentType } : {}),
    ...(latestRelease ? { latestServerVersion: latestRelease.tagName } : {}),
    ...(versionFromArgs ? { serverVersionFromOption: versionFromArgs } : {}),
    ...(context.parsed.options.installDir ? { installDirFromOption: context.parsed.options.installDir } : {}),
    ...(context.parsed.options.skills ? { skillsFromOption: context.parsed.options.skills } : {}),
    allSkillsFromOption: context.parsed.options.allSkills,
  });

  setAgentType(promptResult.agentType);

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

  let syncedSkillsPath: string | undefined;

  if (promptResult.selectedSkills.length > 0) {
    const repoUrl = promptResult.skillsRepo ?? COMMON_SKILLS_REPO;
    const ref = promptResult.skillsRef ?? "main";
    const sparsePaths = promptResult.selectedSkills.map(s => s);

    const skillsResult = await syncSkillsRepository({
      repoUrl,
      ref,
      destinationPath: getSkillsPath(),
      sparsePaths,
    });

    syncedSkillsPath = skillsResult.destinationPath;

    const skillsManifestPath = await saveSkillsManifest({
      schemaVersion: 1,
      repoUrl,
      ref,
      selectedSkills: promptResult.selectedSkills,
      syncedAtUtc: new Date().toISOString(),
      serverVersion: normalizedVersion,
    });
  }

  process.stdout.write(
    [
      `repository=${repository}`,
      `tag=${normalizedVersion}`,
      `asset=${asset.name}`,
      `installed=${transaction.activePath}`,
      `executable=${executablePath}`,
      `skills_selected=${promptResult.selectedSkills.join(',')}`,
      ...(syncedSkillsPath ? [`skills_path=${syncedSkillsPath}`] : []),
    ].join("\n") + "\n"
  );

  return 0;
}
