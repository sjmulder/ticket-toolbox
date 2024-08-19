using System.Text.RegularExpressions;

namespace TicketToolbox;

public class ToolSettings
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

        var settings = new ToolSettings
        {
            JiraUser = Util.GetEnvOrFail("JIRA_USER"),
            JiraSecret = Util.GetSecret("JIRA_SECRET", "Jira client secret"),
            CommitLinkFormat = Git.GetConfigOrFail("jira.commitLinkFormat"),
            JiraBaseUrl = Util.DoOrFail(() => new Uri(jiraBaseUrlStr), "jira.baseUrl"),
            IssueRegex = Util.DoOrFail(() => new Regex(issueRegexStr), "jira.issueRegex")
        };

        return settings;
    }
}
