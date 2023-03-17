using System.Text;
using System.Text.RegularExpressions;

namespace GitJira;

public static class Program
{
    public static bool Verbose { get; private set; }
    public static bool DryRun { get; private set; }

    public static async Task Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "-?")
            Usage("no command given");
        if (args[0] != "link-commits")
            Usage($"bad command: {args[0]}");

        args = args[1..];

        for (; args.Length > 0 && args[0].StartsWith('-'); args = args[1..])
        {
            if (args[0] == "-v" || args[0] == "--interactive")
                Verbose = true;
            else if (args[0] == "-n" || args[0] == "--dry-run")
                DryRun = true;
            else
                Util.Fail($"bad option: {args[0]}");
        }

        var refs = args;

        if (refs.FirstOrDefault(x => x.StartsWith("-")) is { } badRef)
            Usage($"bad ref name: {badRef}");

        var config = GitJiraConfig.LoadOrFail();

        if (!config.CommitLinkFormat.Contains("{commitHash}"))
            Usage("jira.commitLinkFormat must contain {commitHash}");

        string repoName = Regex.Replace(Git.GetOriginOrFail(), "\\.git$", "")
            .Split('/').Last();

        var jira = new JiraClient(config.JiraBaseUrl, config.JiraUser, config.JiraSecret);
        jira.Verbose = Verbose;

        foreach (var group in ReadMentions(refs, config.IssueRegex).GroupBy(x => x.IssueKey))
        {
            var issue = await jira.GetIssueAsync(group.Key);

            if (issue == null)
            {
                Console.WriteLine($"{group.Key} (not found)");
                continue;
            }

            Console.WriteLine($"{group.Key} {issue.Fields.Summary ?? "(no title)"}");

            var commits = group.Select(x => x.Commit).Distinct();
            var toMention = new List<Commit>();

            foreach (var commit in commits)
            {
                if (issue.GetAllText().Any(x => x.Contains(commit.ShortHash)))
                    Console.WriteLine($"  already mentioned: {commit}");
                else
                    toMention.Add(commit);
            }

            if (toMention.Count == 0)
            {
                Console.WriteLine("  nothing to do for issue");
                continue;
            }

            var comment = new StringBuilder();
            comment.AppendLine($"Related commits in {repoName}:");
            comment.AppendLine();

            foreach (var commit in toMention)
            {
                Console.WriteLine($"  {commit}");

                comment.Append(" * [");
                comment.Append(commit.ShortHash);
                comment.Append(" - ");
                comment.Append(commit.Title);
                comment.Append('|');
                comment.Append(config.CommitLinkFormat.Replace("{commitHash}", commit.Hash));
                comment.AppendLine("]");
            }

            if (Verbose)
            {
                Console.WriteLine();
                Console.WriteLine(comment);
            }

            if (DryRun)
                continue;

            await jira.PostCommentAsync(issue.Key, comment.ToString());
        }
    }

    static void Usage(string message)
    {
        Console.Error.WriteLine($"git-jira: {message}");
        Console.Error.WriteLine("Usage: git-jira link-commits [refs]");
        Environment.Exit(Util.ExUsage);
    }

    static IEnumerable<Mention> ReadMentions(IEnumerable<string> refs, Regex issueRegex)
    {
        using var git = Git.Run(new[] { "log" }.Concat(refs).ToArray());

        Commit? commit = null;

        while (git.StandardOutput.ReadLine() is { } line)
        {
            if (line.StartsWith("commit "))
            {
                commit = new Commit(line.Split(" ", 2)[1]);
            }
            else if (line.StartsWith("    ") && commit != null)
            {
                commit.Title ??= line.Length > 76
                    ? line[4..76] + "..."
                    : line[4..];

                foreach (var match in issueRegex.Matches(line).Cast<Match>())
                    yield return new Mention(commit, match.Value);
            }
        }

        git.WaitForExit();

        if (git.ExitCode != 0) Util.Fail($"git exited with status code {git.ExitCode}", Util.ExSubprocess);
    }
}

record Mention(Commit Commit, string IssueKey);
