using System.Text;
using System.Text.RegularExpressions;

namespace TicketToolbox.Tools;

class LinkCommitsTool(string[] args, ToolSettings settings)
{
    public async Task RunAsync()
    {
        var refs = args;

        if (refs.FirstOrDefault(x => x.StartsWith("-")) is { } badRef)
            throw new UsageException($"bad ref name: {badRef}");

        if (!settings.CommitLinkFormat.Contains("{commitHash}"))
            throw new UsageException(
                "jira.commitLinkFormat must contain {commitHash}");

        string repoName = Regex
            .Replace(Git.GetOriginOrFail(), "\\.git$", "")
            .Split('/').Last();

        var jira = new JiraClient(settings.JiraBaseUrl, settings.JiraUser, settings.JiraSecret);
        jira.Verbose = Program.Verbose;

        foreach (var group in ReadMentions(refs, settings.IssueRegex).GroupBy(x => x.IssueKey))
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
                comment.Append(settings.CommitLinkFormat.Replace("{commitHash}", commit.Hash));
                comment.AppendLine("]");
            }

            if (Program.Verbose)
            {
                Console.WriteLine();
                Console.WriteLine(comment);
            }

            if (Program.DryRun)
                continue;

            await jira.PostCommentAsync(issue.Key, comment.ToString());
        }
    }

    IEnumerable<Mention> ReadMentions(IEnumerable<string> refs, Regex issueRegex)
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

        if (git.ExitCode != 0)
            Util.Fail($"git exited with status code {git.ExitCode}", Util.ExSubprocess);
    }
}

record Mention(Commit Commit, string IssueKey);
