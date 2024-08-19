using System.Text.RegularExpressions;

namespace TicketToolbox;

class ToolSettings
{
    const string JiraUserKey = "JIRA_USER";
    const string JiraSecretKey = "JIRA_SECRET";
    const string JiraBaseUrlKey = "jira.baseUrl";
    const string JiraIssueRegexKey = "jira.issueRegex";
    const string JiraCommitLinkFormatKey = "jira.commitLinkFormat";

    public string? JiraUser { get; set; }
    public string? JiraSecret { get; set; }
    public Uri? JiraBaseUrl { get; set; }
    public Regex? IssueRegex { get; set; }
    public string? CommitLinkFormat { get; set; }

    public void ValidateForJiraAccess()
    {
        if (JiraUser == null)
            throw new UsageException($"{JiraUserKey} must be set");
        if (JiraSecret == null)
            throw new UsageException($"{JiraSecretKey} must be set");
        if (JiraBaseUrl == null)
            throw new UsageException($"{JiraBaseUrlKey} must be set");
    }

    public void ValidateForJiraLinking()
    {
        if (IssueRegex == null)
            throw new UsageException($"{JiraIssueRegexKey} must be set");
        if (CommitLinkFormat == null)
            throw new UsageException($"{JiraCommitLinkFormatKey} must be set");
        if (!CommitLinkFormat.Contains("{commitHash}"))
            throw new UsageException(
                $"{JiraCommitLinkFormatKey} must contain {{commitHash}}");
    }

    public void AddJiraSecret()
    {
        JiraSecret = Program.GetSecret(JiraSecretKey, "Jira client secret");
    }

    public static ToolSettings LoadOrFail()
    {
        var settings = new ToolSettings
        {
            JiraUser = Environment.GetEnvironmentVariable(JiraUserKey),
            JiraBaseUrl = Git.GetConfig(JiraBaseUrlKey)?.ToUri(),
            CommitLinkFormat = Git.GetConfig(JiraCommitLinkFormatKey),
            IssueRegex = Git.GetConfig(JiraIssueRegexKey)?.ToRegex()
        };

        return settings;
    }
}
