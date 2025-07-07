using AuthService.Api.Services;
using AuthServiceClass = AuthService.Api.Services.AuthService;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITokenServiceClient, TokenServiceClient>();
builder.Services.AddSingleton<IAuthService, AuthServiceClass>();

builder.Build().Run();