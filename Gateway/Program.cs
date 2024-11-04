using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Multiplexer;
using System.Net;
using System.Text.Json;
using Consul;
using Microsoft.Extensions.Configuration;
using Ocelot.Provider.Consul;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json");

builder.Services.AddOcelot().AddConsul();

var app = builder.Build();

app.UseOcelot();

app.Run();