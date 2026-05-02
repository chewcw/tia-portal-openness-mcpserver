import os from "node:os";
import path from "node:path";

export type AgentType = "opencode" | "claude" | "cursor" | "generic";

let cliAgentType: AgentType | null = null;

export function setAgentType(type: AgentType): void {
  cliAgentType = type;
}

export function detectAgentType(): AgentType {
  if (process.env.CLAUDE_AGENT_METADATA_FILE) {
    return "claude";
  }
  if (process.env.OPENCODE_SESSION_ID || process.env.OPENCODE_ROOT) {
    return "opencode";
  }
  if (process.env.CURSOR_SESSION_ID) {
    return "cursor";
  }
  return "generic";
}

function getAgentType(): AgentType {
  if (cliAgentType) {
    return cliAgentType;
  }
  const envAgentType = process.env.TIA_MCP_AGENT_TYPE;
  if (envAgentType === "opencode" || envAgentType === "claude" || envAgentType === "cursor" || envAgentType === "generic") {
    return envAgentType;
  }
  return detectAgentType();
}

export function resolveSkillsPath(agentType: AgentType): string {
  const home = os.homedir();
  switch (agentType) {
    case "opencode":
      return path.join(home, ".config", "opencode", "skills");
    case "claude":
      return path.join(home, ".claude", "skills");
    case "cursor":
      return path.join(home, ".cursor", "skills");
    case "generic":
      return path.join(home, ".agents", "skills");
  }
}

export function getSkillsPath(): string {
  const envPath = process.env.TIA_MCP_SKILLS_PATH;
  if (envPath && envPath.length > 0) {
    return envPath;
  }
  const agentType = getAgentType();
  return resolveSkillsPath(agentType);
}
