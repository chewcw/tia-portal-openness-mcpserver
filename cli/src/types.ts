export interface GlobalOptions {
  help: boolean;
  version: boolean;
  yes: boolean;
  nonInteractive: boolean;
  verbose: boolean;
  allSkills: boolean;
  serverVersion?: string;
  installDir?: string;
  skillsRepo?: string;
  skillsRef?: string;
  skills?: string[];
  agentType?: string;
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
