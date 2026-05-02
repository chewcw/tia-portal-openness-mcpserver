import extract from "extract-zip";
import { mkdir, mkdtemp } from "node:fs/promises";
import os from "node:os";
import path from "node:path";

export interface ExtractResult {
  stagingRoot: string;
  extractedPath: string;
}

export async function extractZipToStaging(zipPath: string, tempRoot?: string): Promise<ExtractResult> {
  const baseRoot = tempRoot ?? path.join(os.tmpdir(), "tia-mcp-cli");
  await mkdir(baseRoot, { recursive: true });

  const stagingRoot = await mkdtemp(path.join(baseRoot, "extract-"));
  const extractedPath = path.join(stagingRoot, "content");

  await mkdir(extractedPath, { recursive: true });
  await extract(zipPath, { dir: extractedPath });

  return {
    stagingRoot,
    extractedPath,
  };
}
