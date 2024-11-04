using System.Text.Json;
using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IQuestCatalogService, QuestCatalogServiceClient>();
builder.Services.AddHttpClient<IQuestReviewService, QuestReviewServiceClient>();

builder.Services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
{
    consulConfig.Address = new Uri("http://localhost:8500");
}));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/quests-reviews", async (HttpContext httpContext, IQuestCatalogService questCatalogService, IQuestReviewService questReviewService) =>
{
    var quests = questCatalogService.GetQuests();
    var reviews = questReviewService.GetQuestReviews();

    var questsWithReviews = quests.Select(quest => new
    {
        quest.Id,
        quest.Title,
        quest.Description,
        Reviews = reviews.Where(review => review.QuestId == quest.Id)
    });

    httpContext.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(httpContext.Response.Body, questsWithReviews);
});

app.Run();

public interface IQuestCatalogService
{
    List<Quest> GetQuests();
}

public interface IQuestReviewService
{
    List<QuestReview> GetQuestReviews();
}

public class QuestCatalogServiceClient : IQuestCatalogService
{
    private readonly HttpClient _httpClient;
    private readonly IConsulClient _consulClient;

    public QuestCatalogServiceClient(HttpClient httpClient, IConsulClient consulClient)
    {
        _httpClient = httpClient;
        _consulClient = consulClient;
    }

    public List<Quest> GetQuests()
    {
        var services = _consulClient.Agent.Services().Result.Response;
        var catalog = services.Values.FirstOrDefault(s => s.Service.Equals("quest-catalog"));
        var response = _httpClient.GetStringAsync($"http://{catalog.Address}:{catalog.Port}/quests").Result;
        var quests = JsonSerializer.Deserialize<List<Quest>>(response);
        return quests;
    }
}

public class QuestReviewServiceClient : IQuestReviewService
{
    private readonly HttpClient _httpClient;
    private readonly IConsulClient _consulClient;

    public QuestReviewServiceClient(HttpClient httpClient, IConsulClient consulClient)
    {
        _httpClient = httpClient;
        _consulClient = consulClient;
    }

    public List<QuestReview> GetQuestReviews()
    {
        var services = _consulClient.Agent.Services().Result.Response;
        var review = services.Values.FirstOrDefault(s => s.Service.Equals("quest-review"));
        var response = _httpClient.GetStringAsync($"http://{review.Address}:{review.Port}/reviews").Result;
        var questReviews = JsonSerializer.Deserialize<List<QuestReview>>(response);
        return questReviews;
    }
}

public record Quest
{
    public int Id { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
}

public record QuestReview
{
    public int QuestId { get; init; }
    public string Comment { get; init; }
    public int Rating { get; init; }
}
