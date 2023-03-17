using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GitJira;

public static class Program
{
    const int ExUsage = 64; // EX_USAGE from sysexits.h
    const int ExSubprocess = 123; // xargs' convention

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
                Fail($"bad option: {args[0]}");
        }

        var refs = args;

        if (refs.FirstOrDefault(x => x.StartsWith("-")) is { } badRef)
            Usage($"bad ref name: {badRef}");

        string jiraUser = GetEnvOrFail("JIRA_USER");
        string jiraSecret = GetSecret("JIRA_SECRET", "Jira client secret");

        string jiraBaseUrlStr = Git.GetConfigOrFail("jira.baseUrl");
        string issueRegexStr = Git.GetConfigOrFail("jira.issueRegex");
        string commitLinkFormat = Git.GetConfigOrFail("jira.commitLinkFormat");

        var jiraBaseUrl = DoOrFail(() => new Uri(jiraBaseUrlStr), "jira.baseUrl");
        var issueRegex = DoOrFail(() => new Regex(issueRegexStr), "jira.issueRegex");

        if (!commitLinkFormat.Contains("{commitHash}"))
            Usage("jira.commitLinkFormat must contain {commitHash}");

        string repoName = Regex.Replace(Git.GetOriginOrFail(), "\\.git$", "")
            .Split('/').Last();

        var jira = new JiraClient(jiraBaseUrl, jiraUser, jiraSecret);
        jira.Verbose = Verbose;

        foreach (var group in ReadMentions(refs, issueRegex).GroupBy(x => x.IssueKey))
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
                comment.Append(commitLinkFormat.Replace("{commitHash}", commit.Hash));
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

    [DoesNotReturn]
    public static void Fail(string message, int exitCode = 1)
    {
        Console.Error.WriteLine($"git-jira: {message}");
        Environment.Exit(exitCode);
    }

    public static T DoOrFail<T>(Func<T> fn, string message)
    {
        try
        {
            return fn();
        }
        catch (Exception e)
        {
            Fail($"{message}: {e.Message}");
            throw; // not reached
        }
    }

    public static string GetSecret(string envName, string friendlyName)
    {
        string? secret = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(secret))
            return secret;

        string? command = Environment.GetEnvironmentVariable($"{envName}_COMMAND");
        if (command != null)
        {
            using var process = new Process();
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process.StartInfo.FileName = "cmd";
                process.StartInfo.Arguments = $"/c {command}";
            }
            else
            {
                process.StartInfo.FileName = "sh";
                process.StartInfo.ArgumentList.Add("-c");
                process.StartInfo.ArgumentList.Add(command);
            }

            if (Verbose)
                Console.WriteLine($"+ {command}");

            process.Start();

            secret = process.StandardOutput.ReadLine();

            process.WaitForExit();

            if (process.ExitCode != 0)
                Console.Error.WriteLine(
                    $"git-jira: '{command}' returned non-zero exit code " +
                    $"{process.ExitCode}");
            else if (string.IsNullOrWhiteSpace(secret))
                Console.Error.WriteLine($"git-jira: '{command}' returned no data");
            else
                return secret;
        }

        while (string.IsNullOrWhiteSpace(secret))
        {
            Console.Write($"{friendlyName}: ");
            Console.Out.Flush();

            secret = Console.ReadLine();
        }

        return secret;
    }

    static void Usage(string message)
    {
        Console.Error.WriteLine($"git-jira: {message}");
        Console.Error.WriteLine("Usage: git-jira link-commits [refs]");
        Environment.Exit(ExUsage);
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

        if (git.ExitCode != 0)
            Fail($"git exited with status code {git.ExitCode}", ExSubprocess);
    }

    static string GetEnvOrFail(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (value == null)
            Fail($"{name} must be set");

        return value;
    }
}

record Mention(Commit Commit, string IssueKey);
