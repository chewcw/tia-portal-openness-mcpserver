import { cp, mkdir, rename, rm } from "node:fs/promises";
import path from "node:path";

export interface InstallTransactionResult {
  activePath: string;
  rollbackPath?: string;
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
  const rollbackRoot = path.join(installRoot, "rollback");
  await mkdir(rollbackRoot, { recursive: true });

  let rollbackPath: string | undefined;
  const timestamp = new Date().toISOString().replace(/[:.]/g, "-");

  try {
    await removeIfExists(path.join(installRoot, "incoming"));
    await movePath(extractedPath, path.join(installRoot, "incoming"));

    try {
      await removeIfExists(path.join(installRoot, "incoming_previous"));
      await movePath(activePath, path.join(installRoot, "incoming_previous"));
      rollbackPath = path.join(rollbackRoot, timestamp);
      await movePath(path.join(installRoot, "incoming_previous"), rollbackPath);
    } catch (error: unknown) {
      const code = (error as NodeJS.ErrnoException).code;
      if (code !== "ENOENT") {
        throw error;
      }
    }

    await removeIfExists(activePath);
    await movePath(path.join(installRoot, "incoming"), activePath);

    return rollbackPath
      ? {
          activePath,
          rollbackPath,
        }
      : {
          activePath,
        };
  } catch (error) {
    await removeIfExists(path.join(installRoot, "incoming"));

    if (rollbackPath) {
      await removeIfExists(activePath);
      await movePath(rollbackPath, activePath);
    }

    throw error;
  }
}
