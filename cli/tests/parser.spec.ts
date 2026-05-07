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
});