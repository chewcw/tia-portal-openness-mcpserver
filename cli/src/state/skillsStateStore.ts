import { SkillsState } from "./schema.js";
import { getDefaultStateFilePath, loadCliState, saveCliState } from "./installStateStore.js";

export async function saveSkillsState(skillsState: SkillsState, stateFilePath?: string): Promise<string> {
  const pathValue = stateFilePath ?? getDefaultStateFilePath();
  const state = await loadCliState(pathValue);
  state.skills = skillsState;
  await saveCliState(pathValue, state);
  return pathValue;
}
