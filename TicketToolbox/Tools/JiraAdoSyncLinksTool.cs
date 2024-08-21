using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace TicketToolbox.Tools;

// ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
class JiraAdoSyncLinksTool (string[] args, ToolSettings settings) : ITool
{
    public async Task RunAsync()
    {
        if (args.Length != 0)
            throw new UsageException("unexpected arguments");

        settings.AddJiraSecret();
        settings.AddAdoSecret();

        settings.ValidateForJiraAccess();
        settings.ValidateForAdoAccess();
        settings.ValidateForSyncingLinks();

        using var ado = new VssConnection(
                settings.AdoBaseUrl,
                new VssBasicCredential(string.Empty, settings.AdoPat))
            .GetClient<WorkItemTrackingHttpClient>();

        var adoTickets = await ListAdoTicketsAsync(ado);

        Console.WriteLine($"{adoTickets.Count} ADO tickets");

        var jira = new JiraClient(
            settings.JiraBaseUrl!,
            settings.JiraUser!,
            settings.JiraSecret!);

        var tickets = jira.SearchAsync(fields: ["issuelinks"])
            .Where(x => settings.IssueRegex!.IsMatch(x.Key));

        await foreach (var ticket in tickets)
        {
            var links = ticket.Fields!.IssueLinks!
                .Where(x => x.OutwardIssue != null)
                .ToList();

            if (links.Count == 0)
                continue;

            var adoFrom = FindByJiraKey(adoTickets, ticket.Key);

            if (adoFrom == null)
            {
                Console.WriteLine(ticket.Key);

                // ReSharper disable once MethodHasAsyncOverload
                Console.Error.WriteLine(
                    $"ticket-toolbox: no ADO match for " +
                    $"{ticket.Key}");
                continue;
            }

            Console.WriteLine($"{ticket.Key} (#{adoFrom.Id})");

            foreach (var link in links)
            {
                var adoTo = FindByJiraKey(adoTickets, link.OutwardIssue!.Key);

                if (adoTo == null)
                {
                    // ReSharper disable once MethodHasAsyncOverload
                    Console.Error.WriteLine(
                        $"ticket-toolbox: no ADO match for " +
                        $"{link.OutwardIssue.Key} ({link.LinkType.Outward})");

                    continue;
                }

                var rel = link.LinkType.Outward switch
                {
                    "clones" => "System.LinkTypes.Duplicate-Forward",
                    "duplicates" => "System.LinkTypes.Duplicate-Forward",
                    "blocks" => "System.LinkTypes.Successor",
                    _ => "System.LinkTypes.Related",
                };

                var existing = (adoFrom
                    .Relations ?? [])
                    .FirstOrDefault(x => x.Rel == rel && x.Url == adoTo.Url);

                string desc =
                    $"{rel} #{adoTo.Id} " +
                    $"({link.LinkType.Outward} {link.OutwardIssue!.Key})";

                // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                if (existing != null)
                {
                    Console.WriteLine($"  keep:  {desc}");
                }
                else
                {
                    Console.WriteLine($"  add:  {desc}");
                }
            }
        }
    }

    WorkItem? FindByJiraKey(IEnumerable<WorkItem> adoTickets, string key)
    {
        return adoTickets.FirstOrDefault(x => ((string)x
            .Fields["System.Title"])
            .Contains(key));
    }

    async Task<List<WorkItem>> ListAdoTicketsAsync(
        WorkItemTrackingHttpClient ado)
    {
        var query = new Wiql
        {
            Query = "select [Id] from WorkItems"
        };

        if (Program.Verbose)
            Console.WriteLine("ADO: get ticket IDs");

        var ids = (await ado.QueryByWiqlAsync(query))
            .WorkItems
            .Select(x => x.Id);

        var tickets = new List<WorkItem>();

        foreach (var (i, batch) in ids.Batch(100).Ordinate())
        {
            if (Program.Verbose)
                Console.WriteLine($"ADO: get ticket batch {i}");

            tickets.AddRange(await ado.GetWorkItemsAsync(batch, expand: WorkItemExpand.All));
        }

        return tickets;
    }
}
