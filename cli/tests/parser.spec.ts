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
    const parsed = parseArgs(["--verbose", "list"]);

    expect(parsed.name).toBe("list");
    expect(parsed.options.verbose).toBe(true);
  });

  it("throws for missing value flags", () => {
    expect(() => parseArgs(["download", "--server-version"]))
      .toThrow("Missing value for flag --server-version");
  });
});
