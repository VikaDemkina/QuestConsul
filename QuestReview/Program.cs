using System.Text.Json;
using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IApplicationLifetime = Microsoft.Extensions.Hosting.IApplicationLifetime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IQuestReviewService, QuestReviewService>();
builder.Services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
{
    consulConfig.Address = new Uri("http://localhost:8500");
}));

builder.Services.Configure<ServiceDiscoveryConfig>(builder.Configuration.GetSection("ServiceDiscoveryConfig"));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseConsul();

app.MapGet("/reviews", async (HttpContext httpContext, IQuestReviewService questReviewService) =>
{
    var reviews = questReviewService.GetQuestReviews();
    httpContext.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(httpContext.Response.Body, reviews);
});

app.Run();

public interface IQuestReviewService
{
    List<QuestReview> GetQuestReviews();
}

public class QuestReviewService : IQuestReviewService
{
    private readonly List<QuestReview> _reviews = new()
    {
        new QuestReview { QuestId = 1, Comment = "Amazing adventure!", Rating = 5 },
        new QuestReview { QuestId = 1, Comment = "Too scary", Rating = 3 },
        new QuestReview { QuestId = 2, Comment = "Great puzzles", Rating = 4 }
    };

    public List<QuestReview> GetQuestReviews()
    {
        return _reviews;
    }
}

public static class ConsulBuilderExtensions
{
    public static IApplicationBuilder UseConsul(this IApplicationBuilder app)
    {

        var consulClient = app.ApplicationServices.GetRequiredService<IConsulClient>();
        var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
        
        var settings = app.ApplicationServices.GetRequiredService<IOptions<ServiceDiscoveryConfig>>();

        var serviceName = settings.Value.NameOfService;
        var serviceId = settings.Value.IdOfService;
        var uri = new Uri($"http://{settings.Value.Host}:{settings.Value.Port}");

        var registration = new AgentServiceRegistration()
        {
            ID = serviceId,
            Name = serviceName,
            Address = $"{settings.Value.Host}",
            Port = uri.Port,
            Tags = new[] { $"urlprefix-/{settings.Value.IdOfService}" }
        };

        var result= consulClient.Agent.ServiceDeregister(registration.ID).Result;
        result = consulClient.Agent.ServiceRegister(registration).Result;

        lifetime.ApplicationStopping.Register(() =>
        {
            consulClient.Agent.ServiceDeregister(registration.ID).Wait();
        });

        return app;
    }
}

