import { mkdir, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { ReleaseAsset } from "./releases.js";

function sanitizeAssetName(name: string): string {
  return name.replace(/[\\/]+/g, "_");
}

export interface DownloadResult {
  filePath: string;
  size: number;
}

export async function downloadAsset(asset: ReleaseAsset, outputDir: string): Promise<DownloadResult> {
  await mkdir(outputDir, { recursive: true });

  const filePath = path.join(outputDir, sanitizeAssetName(asset.name));
  const response = await fetch(asset.browserDownloadUrl, {
    method: "GET",
    headers: {
      "User-Agent": "tia-mcp-cli",
      Accept: "application/octet-stream",
    },
  });

  if (!response.ok) {
    throw new Error(`Download failed: HTTP ${response.status} from ${asset.browserDownloadUrl}`);
  }

  const data = Buffer.from(await response.arrayBuffer());
  await writeFile(filePath, data);

  const info = await stat(filePath);

  if (info.size <= 0) {
    throw new Error(`Downloaded file is empty: ${filePath}`);
  }

  if (asset.size > 0 && info.size !== asset.size) {
    throw new Error(`Downloaded size mismatch for ${asset.name}: expected ${asset.size}, actual ${info.size}`);
  }

  return {
    filePath,
    size: info.size,
  };
}
