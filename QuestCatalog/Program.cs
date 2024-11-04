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

builder.Services.AddSingleton<IQuestService, QuestService>();
builder.Services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(consulConfig =>
{
    consulConfig.Address = new Uri("http://localhost:8500");
}));

builder.Services.Configure<ServiceDiscoveryConfig>(builder.Configuration.GetSection("ServiceDiscoveryConfig"));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseConsul();

app.MapGet("/quests", async (HttpContext httpContext, IQuestService questService) =>
{
    var quests = questService.GetQuests();
    httpContext.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(httpContext.Response.Body, quests);
});

app.Run();

public interface IQuestService
{
    List<Quest> GetQuests();
}

public class QuestService : IQuestService
{
    private readonly List<Quest> _quests = new()
    {
        new Quest { Id = 1, Title = "Haunted Mansion", Description = "Find the hidden treasure" },
        new Quest { Id = 2, Title = "Pirate Treasure Hunt", Description = "Escape the haunted house" }
    };

    public List<Quest> GetQuests()
    {
        return _quests;
    }
}

public record ServiceDiscoveryConfig
{
    public string NameOfService { get; init; }
    public string IdOfService { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
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


