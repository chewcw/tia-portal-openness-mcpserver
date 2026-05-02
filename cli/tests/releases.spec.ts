import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { getRepositoryFromEnv, ReleaseClient, type ReleaseInfo } from "../src/services/releases.js";

describe("ReleaseClient", () => {
  it("prefers exact asset naming pattern", () => {
    const release: ReleaseInfo = {
      tagName: "v1.0.0",
      name: "v1.0.0",
      draft: false,
      prerelease: false,
      publishedAt: "",
      assets: [
        {
          name: "something-else.zip",
          browserDownloadUrl: "https://example/other.zip",
          size: 10,
        },
        {
          name: "TiaPortalMcpServer-v1.0.0.zip",
          browserDownloadUrl: "https://example/expected.zip",
          size: 20,
        },
      ],
    };

    const client = new ReleaseClient({ repository: "owner/repo" });
    const selected = client.resolveAsset(release);

    expect(selected.name).toBe("TiaPortalMcpServer-v1.0.0.zip");
  });

  it("falls back to first zip asset when exact name missing", () => {
    const release: ReleaseInfo = {
      tagName: "v2.0.0",
      name: "v2.0.0",
      draft: false,
      prerelease: false,
      publishedAt: "",
      assets: [
        {
          name: "fallback.zip",
          browserDownloadUrl: "https://example/fallback.zip",
          size: 11,
        },
      ],
    };

    const client = new ReleaseClient({ repository: "owner/repo" });
    const selected = client.resolveAsset(release);

    expect(selected.name).toBe("fallback.zip");
  });

  it("throws when no zip asset exists", () => {
    const release: ReleaseInfo = {
      tagName: "v2.0.0",
      name: "v2.0.0",
      draft: false,
      prerelease: false,
      publishedAt: "",
      assets: [
        {
          name: "binary.tar.gz",
          browserDownloadUrl: "https://example/binary.tar.gz",
          size: 11,
        },
      ],
    };

    const client = new ReleaseClient({ repository: "owner/repo" });

    expect(() => client.resolveAsset(release)).toThrow("No zip asset found for release v2.0.0.");
  });
});

describe("getRepositoryFromEnv", () => {
  it("returns the hardcoded repository", () => {
    expect(getRepositoryFromEnv()).toBe("chewcw/tia-portal-openness-mcpserver");
  });
});
