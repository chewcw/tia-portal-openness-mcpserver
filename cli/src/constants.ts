export const TIA_PORTAL_MCP_REPO = "chewcw/tia-portal-openness-mcpserver";

export interface SkillDefinition {
  name: string;
  localPath: string;
}

export const AVAILABLE_SKILLS: SkillDefinition[] = [
  {
    name: 'siemens-awl-stl-programmer',
    localPath: 'siemens-awl-stl-programmer',
  },
  {
    name: 'siemens-tia-portal-integrator',
    localPath: 'siemens-tia-portal-integrator',
  },
];

export const DEFAULT_COMPANION_SKILL_PATHS = [
  ".agents/skills",
];
