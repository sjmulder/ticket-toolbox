using System.Text.RegularExpressions;

namespace TicketToolbox;

class ToolSettings
{
    public required string JiraUser { get; set; }
    public required string JiraSecret { get; set; }
    public required Uri JiraBaseUrl { get; set; }
    public required Regex IssueRegex { get; set; }
    public required string CommitLinkFormat { get; set; }

    public static ToolSettings LoadOrFail()
    {
        string jiraBaseUrlStr = Git.GetRequiredConfig("jira.baseUrl");
        string issueRegexStr = Git.GetRequiredConfig("jira.issueRegex");

        string? jiraUser = Environment.GetEnvironmentVariable("JIRA_USER");
        if (jiraUser == null)
            throw new UsageException("JIRA_USER must be set");

        var settings = new ToolSettings
        {
            JiraUser = jiraUser,
            JiraSecret = Program.GetSecret("JIRA_SECRET", "Jira client secret"),
            CommitLinkFormat = Git.GetRequiredConfig("jira.commitLinkFormat"),
            JiraBaseUrl = new Uri(jiraBaseUrlStr),
            IssueRegex = new Regex(issueRegexStr)
        };

        return settings;
    }
}
