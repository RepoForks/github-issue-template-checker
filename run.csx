using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Octokit;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info($"C# HTTP trigger function processed a request. RequestUri={req.RequestUri}");

    dynamic data = await req.Content.ReadAsAsync<object>();
    //log.Info($"Data. {data}");
    await ProcessIssueAsync(data);   
   
    return req.CreateResponse(HttpStatusCode.OK);
}

private static async Task ProcessIssueAsync(dynamic data)
{
    if(data?.action != "opened")
        return;
    
    var creator = (string)data.issue.user.login; 
    var owner = (string)data.repository.owner.login;
    var repository = (string)data.repository.name;    
    var repositoryId = (long)data.repository.id;
    var branch = (string)data.repository.default_branch; 

    var issueLines = GetLines((string)data.issue.body);
    var templateLines = (await GetTemplateElements(owner, repository, branch)).ToArray();

    var matchingQuote = CheckIssueWithTemplate(issueLines, templateLines);
    var message = GetMessage(creator, matchingQuote);
    await CreateCommentAsync(repositoryId, (int)data.issue.number, message);
}

private static async Task<IEnumerable<string>> GetTemplateElements(string user, string repository, string branch)
{
    var url = $"https://raw.githubusercontent.com/{user}/{repository}/{branch}/ISSUE_TEMPLATE_CHECK.md";

    try
    {
        var httpClient = new HttpClient();
        var check = await httpClient.GetStringAsync(url);
        return GetLines(check)
            .Where(line => !string.IsNullOrWhiteSpace(line));
    }
    catch { }
    return Enumerable.Empty<string>();
}

private static double CheckIssueWithTemplate(string[] issueLines, string[] templateLines)
{
    if (templateLines.Length == 0)
        return 1;

    var templateList = templateLines.ToList();
    foreach (var issueLine in issueLines)
    {
        var found = templateList.FirstOrDefault(tpl => issueLine.StartsWith(tpl));

        if (!string.IsNullOrEmpty(found))
        {
            templateList.Remove(found);
        }
    }
    var matches = templateLines.Length - templateList.Count;
    return matches / (double)templateLines.Length;
}

static string GetMessage(string userName, double matchingQuote)
{
    string message; 


    if (matchingQuote > 0.9)
    {
        message = "Thanks for using the issue template :kissing_heart:\n" +
                    "I appreciate it very much. I'm sure, the maintainers of this repository will answer, soon.";
    }
    else
    {
        message = $"It seems like ({(1 - matchingQuote):P}) you haven't used our issue template :cry: " +
                    $"I think it is very frustrating for the repository owners, if you ignore them.\n\n" +
                    $"If you think it's fine to make an exception, just ignore this message.\n" +
                    $"**But if you think it was a mistake to delete the template, please close the issue and create a new one.**\n\n" +
                    $"Thanks!";
    }                       

    return $"Hi @{userName},\n\n" +
            $"I'm the friendly issue checker.\n" +
            message;
}

private static async Task CreateCommentAsync(long repositoryId, int issueNumber, string message)
{
    var client = new GitHubClient(new ProductHeaderValue("github-issue-checker"));
    var tokenAuth = new Credentials(<github auth token>);
    client.Credentials = tokenAuth;
    var comment = await client.Issue.Comment.Create(repositoryId, issueNumber, message);
}

private static string[] GetLines(string value)
{
    return Regex.Split(value, @"\r?\n|\r");
}
