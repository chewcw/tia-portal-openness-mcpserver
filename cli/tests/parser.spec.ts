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
      "--yes",
      "--verbose",
    ]);

    expect(parsed.name).toBe("install");
    expect(parsed.options.serverVersion).toBe("v1.2.3");
    expect(parsed.options.installDir).toBe("C:/tmp/server");
    expect(parsed.options.yes).toBe(true);
    expect(parsed.options.verbose).toBe(true);
  });

  it("accepts command in any token position", () => {
    const parsed = parseArgs(["--verbose", "install"]);

    expect(parsed.name).toBe("install");
    expect(parsed.options.verbose).toBe(true);
  });

  it("throws for missing value flags", () => {
    expect(() => parseArgs(["download", "--server-version"]))
      .toThrow("Missing value for flag --server-version");
  });

  it("parses skills CSV and --all flags", () => {
    const parsed = parseArgs(["skills", "sync", "--skills", "alpha,beta", "--all"]);

    expect(parsed.name).toBe("skills");
    expect(parsed.args).toEqual(["sync"]);
    expect(parsed.options.skills).toEqual(["alpha", "beta"]);
    expect(parsed.options.allSkills).toBe(true);
  });

  it("throws for empty --skills value", () => {
    expect(() => parseArgs(["skills", "sync", "--skills", ",,"]))
      .toThrow("Missing skill names for flag --skills");
  });
});
