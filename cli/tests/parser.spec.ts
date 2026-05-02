import { describe, expect, it } from "vitest";
import { parseArgs } from "../src/parser.js";

describe("parseArgs", () => {
  it("parses command and global flags", () => {
    const parsed = parseArgs([
      "install",
      "--server-version",
      "v1.2.3",
      "--install-dir",
      "C:/tmp/server",
    ]);

    expect(parsed.name).toBe("install");
    expect(parsed.options.serverVersion).toBe("v1.2.3");
    expect(parsed.options.installDir).toBe("C:/tmp/server");
  });

  it("accepts command in any token position", () => {
    const parsed = parseArgs(["--server-version", "v1.0.0", "install"]);

    expect(parsed.name).toBe("install");
    expect(parsed.options.serverVersion).toBe("v1.0.0");
  });

  it("throws for missing value flags", () => {
    expect(() => parseArgs(["download", "--server-version"]))
      .toThrow("Missing value for flag --server-version");
  });

  it("parses skills install with required options", () => {
    const parsed = parseArgs(["skills", "install", "--skills", "siemens-stl-awl-programmer", "--agent-type", "opencode"]);

    expect(parsed.name).toBe("skills");
    expect(parsed.args).toEqual(["install"]);
    expect(parsed.options.skills).toBe("siemens-stl-awl-programmer");
    expect(parsed.options.agentType).toBe("opencode");
  });

  it("parses comma-separated agent-type values", () => {
    const parsed = parseArgs(["skills", "install", "--skills", "siemens-stl-awl-programmer", "--agent-type", "generic, claude"]);

    expect(parsed.name).toBe("skills");
    expect(parsed.args).toEqual(["install"]);
    expect(parsed.options.skills).toBe("siemens-stl-awl-programmer");
    expect(parsed.options.agentType).toBe("generic, claude");
  });

  it("throws for empty --skills value", () => {
    expect(() => parseArgs(["skills", "install", "--skills", ""]))
      .toThrow("Missing value for flag --skills");
  });
});
