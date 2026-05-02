import { getJson } from "./http.js";

export interface ReleaseAsset {
  name: string;
  browserDownloadUrl: string;
  size: number;
}

export interface ReleaseInfo {
  tagName: string;
  name: string;
  draft: boolean;
  prerelease: boolean;
  publishedAt: string;
  assets: ReleaseAsset[];
}

export interface ReleaseClientOptions {
  repository: string;
  token?: string;
}

interface GitHubAsset {
  name: string;
  browser_download_url: string;
  size: number;
}

interface GitHubRelease {
  tag_name: string;
  name: string | null;
  draft: boolean;
  prerelease: boolean;
  published_at: string | null;
  assets: GitHubAsset[];
}

function normalizeRelease(source: GitHubRelease): ReleaseInfo {
  return {
    tagName: source.tag_name,
    name: source.name ?? source.tag_name,
    draft: source.draft,
    prerelease: source.prerelease,
    publishedAt: source.published_at ?? "",
    assets: source.assets.map((asset) => ({
      name: asset.name,
      browserDownloadUrl: asset.browser_download_url,
      size: asset.size,
    })),
  };
}

function normalizeTagInput(tag: string): string {
  const trimmed = tag.trim();
  if (trimmed.length === 0) {
    throw new Error("Server version tag cannot be empty.");
  }
  return trimmed.startsWith("v") ? trimmed : `v${trimmed}`;
}

export class ReleaseClient {
  private readonly repository: string;
  private readonly token: string | undefined;

  public constructor(options: ReleaseClientOptions) {
    if (!options.repository.includes("/")) {
      throw new Error("Repository must use the format owner/repo.");
    }

    this.repository = options.repository;
    this.token = options.token;
  }

  public async listReleases(): Promise<ReleaseInfo[]> {
    const url = `https://api.github.com/repos/${this.repository}/releases?per_page=100`;
    const releases = await getJson<GitHubRelease[]>(url, this.token ? { token: this.token } : {});

    return releases
      .map(normalizeRelease)
      .filter((release) => !release.draft)
      .sort((left, right) => right.tagName.localeCompare(left.tagName));
  }

  public async resolveRelease(tag?: string): Promise<ReleaseInfo> {
    if (tag) {
      const normalizedTag = normalizeTagInput(tag);
      const url = `https://api.github.com/repos/${this.repository}/releases/tags/${normalizedTag}`;
      const release = await getJson<GitHubRelease>(url, this.token ? { token: this.token } : {});
      return normalizeRelease(release);
    }

    const url = `https://api.github.com/repos/${this.repository}/releases/latest`;
    const latest = await getJson<GitHubRelease>(url, this.token ? { token: this.token } : {});
    return normalizeRelease(latest);
  }

  public resolveAsset(release: ReleaseInfo): ReleaseAsset {
    const expectedName = `TiaPortalMcpServer-${release.tagName}.zip`;
    const exact = release.assets.find((asset) => asset.name === expectedName);

    if (exact) {
      return exact;
    }

    const fallback = release.assets.find((asset) => asset.name.toLowerCase().endsWith(".zip"));

    if (!fallback) {
      throw new Error(`No zip asset found for release ${release.tagName}.`);
    }

    return fallback;
  }
}

export function getRepositoryFromEnv(): string {
  const fromCliEnv = process.env.TIA_MCP_GITHUB_REPOSITORYhhh;
  const fromGitHubEnv = process.env.GITHUB_REPOSITORY;
  const rawValue = (fromCliEnv ?? fromGitHubEnv ?? "").trim();

  const value = rawValue
    .replace(/^https?:\/\/github\.com\//i, "")
    .replace(/\.git$/i, "")
    .replace(/^\/+/, "")
    .replace(/\/+$/, "");

  if (!value) {
    throw new Error(
      "Missing repository. Set TIA_MCP_GITHUB_REPOSITORY to owner/repo (for example: org/tia-portal-openness-mcpserver)."
    );
  }

  return value;
}
