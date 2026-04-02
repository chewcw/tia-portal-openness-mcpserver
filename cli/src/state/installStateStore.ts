import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { CliState, createEmptyState, isCliState } from "./schema.js";
import { getStateFilePath } from "../services/agentPathResolver.js";

export function getDefaultStateFilePath(): string {
  return getStateFilePath();
}

export async function loadCliState(filePath: string): Promise<CliState> {
  try {
    const json = await readFile(filePath, "utf8");
    const parsed = JSON.parse(json) as unknown;

    if (!isCliState(parsed)) {
      throw new Error(`Invalid state schema at ${filePath}`);
    }

    return parsed;
  } catch (error: unknown) {
    if ((error as NodeJS.ErrnoException).code === "ENOENT") {
      return createEmptyState();
    }

    throw error;
  }
}

export async function saveCliState(filePath: string, state: CliState): Promise<void> {
  if (!isCliState(state)) {
    throw new Error("Refusing to persist invalid state.");
  }

  await mkdir(path.dirname(filePath), { recursive: true });
  await writeFile(filePath, JSON.stringify(state, null, 2) + "\n", "utf8");
}
