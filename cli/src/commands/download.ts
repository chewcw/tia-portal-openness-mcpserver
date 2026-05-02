import { CommandContext } from "../types.js";
import path from "node:path";
import { downloadAsset } from "../services/downloader.js";
import { extractZipToStaging } from "../services/extract.js";
import { getRepositoryFromEnv, ReleaseClient } from "../services/releases.js";

export async function downloadCommand(context: CommandContext): Promise<number> {
  const repository = getRepositoryFromEnv();
  const versionFromArg = context.parsed.args[0];
  const version = context.parsed.options.serverVersion ?? versionFromArg;
  const token = process.env.GITHUB_TOKEN;

  const client = new ReleaseClient({
    repository,
    ...(token ? { token } : {}),
  });

  const release = await client.resolveRelease(version);
  const asset = client.resolveAsset(release);
  const outputDir = context.parsed.options.installDir ?? path.join(process.cwd(), "downloads");
  const downloaded = await downloadAsset(asset, outputDir);
  const extracted = await extractZipToStaging(downloaded.filePath);

  process.stdout.write(
    [
      `repository=${repository}`,
      `tag=${release.tagName}`,
      `asset=${asset.name}`,
      `size=${downloaded.size}`,
      `url=${asset.browserDownloadUrl}`,
      `downloaded=${downloaded.filePath}`,
      `staging=${extracted.extractedPath}`,
    ].join("\n") + "\n"
  );

  return 0;
}
