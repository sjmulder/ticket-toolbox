using System.Text.RegularExpressions;

namespace TicketToolbox;

class ToolSettings
{
    public string JiraUser { get; set; } = null!;
    public string JiraSecret { get; set; } = null!;
    public Uri JiraBaseUrl { get; set; } = null!;
    public Regex IssueRegex { get; set; } = null!;
    public string CommitLinkFormat { get; set; } = null!;

    public static ToolSettings LoadOrFail()
    {
        string jiraBaseUrlStr = Git.GetConfigOrFail("jira.baseUrl");
        string issueRegexStr = Git.GetConfigOrFail("jira.issueRegex");

        string? jiraUser = Environment.GetEnvironmentVariable("JIRA_USER");
        if (jiraUser == null)
            throw new UsageException("JIRA_USER must be set");

        var settings = new ToolSettings
        {
            JiraUser = jiraUser,
            JiraSecret = Program.GetSecret("JIRA_SECRET", "Jira client secret"),
            CommitLinkFormat = Git.GetConfigOrFail("jira.commitLinkFormat"),
            JiraBaseUrl = new Uri(jiraBaseUrlStr),
            IssueRegex = new Regex(issueRegexStr)
        };

        return settings;
    }
}
