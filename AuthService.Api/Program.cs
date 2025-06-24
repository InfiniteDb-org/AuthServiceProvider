using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AuthService.Api.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITokenServiceClient, TokenServiceClient>();
builder.Services.AddSingleton<IAuthService, AuthService.Api.Services.AuthService>();

builder.Build().Run();