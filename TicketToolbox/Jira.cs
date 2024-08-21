using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace TicketToolbox;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global

class JiraClient
{
    readonly HttpClient _http;

    public JiraClient(Uri baseUrl, string username, string token)
    {
        string key = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{token}"));

        _http = new HttpClient();
        _http.BaseAddress = baseUrl;
        _http.DefaultRequestHeaders.Add("Authorization", $"Basic {key}");
    }

    public async Task<JiraIssue?> GetIssueAsync(string key)
    {
        string path = $"rest/api/2/issue/{key}?fields=summary,description,comment";

        if (Program.Verbose)
            Console.WriteLine($"> GET {_http.BaseAddress}{path}");

        var response = await _http.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            await RaiseError(response, $"Failed to get {key}");

        return await response.Content.ReadFromJsonAsync<JiraIssue>();
    }

    public async Task PostCommentAsync(string key, string body)
    {
        string path = $"rest/api/2/issue/{key}/comment";

        if (Program.Verbose)
            Console.WriteLine($"> POST {_http.BaseAddress}{path}");

        var comment = new NewJiraComment();
        comment.Body = body;

        var response = await _http.PostAsJsonAsync(path, comment);
        if (!response.IsSuccessStatusCode)
            await RaiseError(response, $"Failed to post comment on {key}");
    }

    [DoesNotReturn]
    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    async Task RaiseError(HttpResponseMessage response, string message)
    {
        Console.Error.WriteLine($"< HTTP {(int)response.StatusCode}");
        Console.Error.WriteLine("<");

        var body = await response.Content.ReadAsStringAsync();

        if (Verbose)
            foreach (var line in body.Split('\n'))
                Console.Error.WriteLine($"< {line}");

        throw new JiraException(message, body);
    }

    public bool Verbose { get; set; }
}

class JiraIssue
{
    public required string Id { get; set; }
    public required string Key { get; set; }
    public required JiraIssueFields Fields { get; set; }

    public IEnumerable<string> GetAllText()
    {
        if (Fields.Description != null)
            yield return Fields.Description;

        if (Fields.Comment != null)
            foreach (var comment in Fields.Comment.Comments)
                yield return comment.Body;
    }
}

class JiraIssueFields
{
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public JiraCommentField? Comment { get; set; }
}

class JiraCommentField
{
    public required JiraComment[] Comments { get; set; }
}

class JiraComment
{
    public required string Body { get; set; }
}

class NewJiraComment
{
    public string? Body { get; set; }
}

class JiraException(
    string message,
    string? responseBody = null) : Exception(message)
{
    public string? ResponseBody { get; set; } = responseBody;
}
