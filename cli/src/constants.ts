export const TIA_PORTAL_MCP_REPO = "chewcw/tia-portal-openness-mcpserver";
export const AGENT_SKILLS_REPO = "chewcw/agent-skills";

export interface SkillDefinition {
  name: string;
  repoUrl: string;
}

export const AVAILABLE_SKILLS: SkillDefinition[] = [
  {
    name: 'siemens-awl-stl-programmer',
    repoUrl: 'https://github.com/chewcw/agent-skills/siemens-awl-stl-programmer',
  },
  {
    name: 'siemens-tia-portal-integrator',
    repoUrl: 'https://github.com/chewcw/agent-skills/siemens-tia-portal-integrator',
  },
];

export const COMMON_SKILLS_REPO = 'https://github.com/chewcw/agent-skills';
