export interface HttpJsonOptions {
  token?: string;
  userAgent?: string;
}

export async function getJson<T>(url: string, options: HttpJsonOptions = {}): Promise<T> {
  const headers = new Headers();
  headers.set("Accept", "application/vnd.github+json");
  headers.set("User-Agent", options.userAgent ?? "tia-mcp-cli");

  if (options.token) {
    headers.set("Authorization", `Bearer ${options.token}`);
  }

  const response = await fetch(url, {
    method: "GET",
    headers,
  });

  if (!response.ok) {
    const body = await response.text();
    if (response.status === 401) {
      throw new Error(`HTTP 401 for ${url}: Authentication required. Set GITHUB_TOKEN environment variable with a valid GitHub personal access token (public_repo scope for public repositories).`);
    }
    throw new Error(`HTTP ${response.status} for ${url}: ${body}`);
  }

  return (await response.json()) as T;
}
