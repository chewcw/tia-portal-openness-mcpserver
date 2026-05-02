export class CliError extends Error {
  public readonly code: string;

  public constructor(code: string, message: string) {
    super(message);
    this.name = "CliError";
    this.code = code;
  }
}

export class MissingDependencyError extends CliError {
  public constructor(dependencyName: string, hint?: string) {
    super(
      "MISSING_DEPENDENCY",
      hint
        ? `Missing dependency '${dependencyName}'. ${hint}`
        : `Missing dependency '${dependencyName}'.`
    );
    this.name = "MissingDependencyError";
  }
}

export class ExternalCommandError extends CliError {
  public constructor(commandName: string, detail: string) {
    super("EXTERNAL_COMMAND_FAILED", `${commandName} failed: ${detail}`);
    this.name = "ExternalCommandError";
  }
}
