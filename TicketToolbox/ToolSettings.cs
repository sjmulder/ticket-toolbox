using System.Text.RegularExpressions;

namespace TicketToolbox;

class ToolSettings
{
    const string JiraUserKey = "JIRA_USER";
    const string JiraSecretKey = "JIRA_SECRET";
    const string JiraBaseUrlKey = "jira.baseUrl";
    const string JiraIssueRegexKey = "jira.issueRegex";
    const string JiraCommitLinkFormatKey = "jira.commitLinkFormat";

    const string AdoPatKey = "ADO_PAT";
    const string AdoBaseUrlKey = "ado.baseUrl";

    public string? JiraUser { get; set; }
    public string? JiraSecret { get; set; }
    public Uri? JiraBaseUrl { get; set; }
    public Regex? IssueRegex { get; set; }
    public string? CommitLinkFormat { get; set; }

    public string? AdoPat { get; set; }
    public Uri? AdoBaseUrl { get; set; }

    public void ValidateForJiraAccess()
    {
        if (JiraUser == null)
            throw new UsageException($"{JiraUserKey} must be set");
        if (JiraSecret == null)
            throw new UsageException($"{JiraSecretKey} must be set");
        if (JiraBaseUrl == null)
            throw new UsageException($"{JiraBaseUrlKey} must be set");
    }

    public void ValidateForAdoAccess()
    {
        if (AdoPat == null)
            throw new UsageException($"{AdoPatKey} must be set");
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

    public void ValidateForSyncingLinks()
    {
        if (IssueRegex == null)
            throw new UsageException($"{JiraIssueRegexKey} must be set");
    }

    public void AddJiraSecret()
    {
        JiraSecret = Program.GetSecret(JiraSecretKey, "Jira client secret");
    }

    public void AddAdoSecret()
    {
        AdoPat = Program.GetSecret(AdoPatKey, "ADO PAT");
    }

    public static ToolSettings LoadOrFail()
    {
        var settings = new ToolSettings
        {
            JiraUser = Environment.GetEnvironmentVariable(JiraUserKey),
            JiraBaseUrl = Git.GetConfig(JiraBaseUrlKey)?.ToUri(),
            IssueRegex = Git.GetConfig(JiraIssueRegexKey)?.ToRegex(),
            CommitLinkFormat = Git.GetConfig(JiraCommitLinkFormatKey),

            AdoBaseUrl = Git.GetConfig(AdoBaseUrlKey)?.ToUri(),
        };

        return settings;
    }
}
