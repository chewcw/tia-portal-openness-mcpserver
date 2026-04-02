import { CommandContext } from "../types.js";
import { getRepositoryFromEnv, ReleaseClient } from "../services/releases.js";

function printListHelp(): void {
  process.stdout.write(
    [
      "List available server releases",
      "",
      "Usage:",
      "  @bizarreaster/tia-portal-openness-mcpserver list [options]",
      "",
      "Options:",
      "  --help                  Show help",
      "  --version               Show version",
    ].join("\n") + "\n"
  );
}

export async function listCommand(context: CommandContext): Promise<number> {
  if (context.parsed.options.help) {
    printListHelp();
    return 0;
  }
  const repository = getRepositoryFromEnv();
  const token = process.env.GITHUB_TOKEN;
  const client = new ReleaseClient({
    repository,
    ...(token ? { token } : {}),
  });

  const releases = await client.listReleases();

  if (releases.length === 0) {
    process.stdout.write("No releases found.\n");
    return 0;
  }

  const lines = releases.map((release) => {
    const kind = release.prerelease ? "prerelease" : "stable";
    const published = release.publishedAt ? ` published=${release.publishedAt}` : "";
    return `${release.tagName} (${kind}) assets=${release.assets.length}${published}`;
  });

  process.stdout.write(lines.join("\n") + "\n");
  void context;
  return 0;
}
