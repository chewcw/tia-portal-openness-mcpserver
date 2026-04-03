import { cp, mkdir, rename, rm } from "node:fs/promises";
import path from "node:path";

export interface InstallTransactionResult {
  activePath: string;
}

async function removeIfExists(targetPath: string): Promise<void> {
  await rm(targetPath, { recursive: true, force: true });
}

async function movePath(sourcePath: string, destinationPath: string): Promise<void> {
  try {
    await rename(sourcePath, destinationPath);
  } catch (error: unknown) {
    const code = (error as NodeJS.ErrnoException).code;
    if (code !== "EXDEV") {
      throw error;
    }

    await cp(sourcePath, destinationPath, { recursive: true });
    await rm(sourcePath, { recursive: true, force: true });
  }
}

export async function installExtractedContent(extractedPath: string, installRoot: string): Promise<InstallTransactionResult> {
  await mkdir(installRoot, { recursive: true });

  const activePath = path.join(installRoot, "current");

  await removeIfExists(path.join(installRoot, "incoming"));
  await movePath(extractedPath, path.join(installRoot, "incoming"));

  await removeIfExists(activePath);
  await movePath(path.join(installRoot, "incoming"), activePath);

  return { activePath };
}
