using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

const int exUsage = 64;         // EX_USAGE from sysexits.h
const int exSubprocess = 123;   // xargs' convention

bool verbose = false;
bool dryRun = false;

if (args.Length == 0 || args[0] == "-?")
    Usage("no command given");
if (args[0] != "link-issues")
    Usage($"bad command: {args[0]}");

args = args[1..];

for (; args.Length > 0 && args[0].StartsWith('-'); args = args[1..])
{
    if (args[0] == "-v" || args[0] == "--interactive")
        verbose = true;
    else if (args[0] == "-n" || args[0] == "--dry-run")
        dryRun = true;
    else
        Fail($"bad option: {args[0]}");
}

var refs = args;

if (refs.FirstOrDefault(x => x.StartsWith("-")) is { } badRef)
    Usage($"bad ref name: {badRef}");

string jiraUser = GetEnvOrFail("JIRA_USER");
string jiraToken = GetEnvOrFail("JIRA_TOKEN");

string jiraBaseUrlStr = GetGitConfigOrFail("jira.baseUrl");
string issueRegexStr = GetGitConfigOrFail("jira.issueRegex");
string commitLinkFormat = GetGitConfigOrFail("jira.commitLinkFormat");

var jiraBaseUrl = DoOrFail(() => new Uri(jiraBaseUrlStr), "jira.baseUrl");
var issueRegex = DoOrFail(() => new Regex(issueRegexStr), "jira.issueRegex");

if (!commitLinkFormat.Contains("{commitHash}"))
    Usage("jira.commitLinkFormat must contain {commitHash}");

string repoName = Regex.Replace(GetGitOriginOrFail(), "\\.git$", "")
    .Split('/').Last();

var jira = new JiraClient(jiraBaseUrl, jiraUser, jiraToken);
jira.Verbose = verbose;

foreach (var group in ReadMentions().GroupBy(x => x.IssueKey))
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

    if (verbose)
    {
        Console.WriteLine();
        Console.WriteLine(comment);
    }

    if (dryRun)
        continue;

    await jira.PostCommentAsync(issue.Key, comment.ToString());
}

void Usage(string message)
{
    Console.Error.WriteLine($"git-jira: {message}");
    Console.Error.WriteLine("Usage: git-jira link-issues [refs]");
    Environment.Exit(exUsage);
}

IEnumerable<Mention> ReadMentions()
{
    using var git = StartGitOrFail(new[] { "log" }.Concat(refs).ToArray());

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
        Fail($"git exited with status code {git.ExitCode}", exSubprocess);
}

T DoOrFail<T>(Func<T> fn, string message)
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

string GetGitOriginOrFail()
{
    using var git = StartGitOrFail("remote", "get-url", "origin");

    string? output = git.StandardOutput.ReadLine();

    git.WaitForExit();

    if (string.IsNullOrWhiteSpace(output) || git.ExitCode != 0)
        Fail($"can't get 'origin' remote");

    return output;
}

string GetGitConfigOrFail(string name)
{
    using var git = StartGitOrFail("config", name);

    string? output = git.StandardOutput.ReadLine();

    git.WaitForExit();

    if (string.IsNullOrWhiteSpace(output) || git.ExitCode != 0)
        Fail($"'{name}' must be set in git config");

    return output;
}

Process StartGitOrFail(params string[] args)
{
    var git = new Process();
    git.StartInfo.FileName = "git";
    git.StartInfo.UseShellExecute = false;
    git.StartInfo.CreateNoWindow = true;
    git.StartInfo.RedirectStandardOutput = true;

    foreach (var arg in args)
        git.StartInfo.ArgumentList.Add(arg);

    if (verbose)
        Console.WriteLine($"+ git {string.Join(" ", args)}");

    git.Start();

    return git;
}

string GetEnvOrFail(string name)
{
    string? value = Environment.GetEnvironmentVariable(name);
    if (value == null)
        Fail($"{name} must be set");

    return value;
}

[DoesNotReturn]
void Fail(string message, int exitCode = 1)
{
    Console.Error.WriteLine($"git-jira: {message}");
    Environment.Exit(exitCode);
}

class Commit : IEquatable<Commit>
{
    public Commit(string hash, string? title = null)
    {
        Hash = hash;
        Title = title;
    }

    public string Hash { get; }
    public string ShortHash => Hash[..8];
    public string? Title { get; set; }

    public override string ToString()
        => Title == null ? ShortHash : $"{ShortHash} {Title}";
    public bool Equals(Commit? other)
        => Hash == other?.Hash;
    public override bool Equals(object? obj)
        => obj is Commit commit && Equals(commit);
    public override int GetHashCode()
        => Hash.GetHashCode();
}

record Mention(Commit Commit, string IssueKey);
