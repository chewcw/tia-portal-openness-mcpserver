export interface GlobalOptions {
  help: boolean;
  version: boolean;
  serverVersion?: string;
  installDir?: string;
  skills?: string;
  agentType?: string;
  companionSkills?: string[];
  companionSkillsPath?: string;
  companionSkillsEnv?: "global" | "local";
}

export interface ParsedCommand {
  name: string;
  args: string[];
  options: GlobalOptions;
}

export interface CommandContext {
  parsed: ParsedCommand;
}

export type CommandHandler = (context: CommandContext) => Promise<number>;
