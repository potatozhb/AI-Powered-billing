using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using AIbillingRAGBuilder.Services.Interfaces;
using AIbillingRAGBuilder.Services;
using AIbillingRAGBuilder.Workers;
using AIbillingRAGBuilder.Services.Decision;
using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Repo;
using Microsoft.Extensions.Logging;


var builder = FunctionsApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddConsole();

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IAISearchKeywordAndVectorPushService, AISearchKeywordAndVectorPushService>();

builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IBillingPromptBuilder, BillingPromptBuilder>();
builder.Services.AddScoped<IAIDecisionService, AIDecisionService>();
builder.Services.AddScoped<IBillingDecisionRepository, BillingDecisionRepository>();
builder.Services.AddScoped<IBillingDecisionService, BillingDecisionService>();
builder.Services.AddScoped<AIDecisionConsumer>();

// Background worker DI registrations
builder.Services.AddSingleton<IWorkQueue, WorkQueue>(); 
builder.Services.AddHostedService<BackgroundWorkerService>();
builder.Services.AddSingleton<IAzureWorkQueue, AzureWorkSimpleQueue>();

//builder.Services
//    .AddOptions<EventOptions>()
//    .Bind(builder.Configuration.GetSection(EventOptions.SectionName))
//    .Validate(
//        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
//        "EventHubs:ConnectionString is required.")
//    .Validate(
//        options => !string.IsNullOrWhiteSpace(options.ClosedOrderEventHubName),
//        "EventHubs:ClosedOrderEventHubName is required.");

builder.Services
    .AddOptions<SqlOptions>()
    .Bind(builder.Configuration.GetSection(SqlOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Sql:ConnectionString is required.");

builder.Services
    .AddOptions<AzureSearchOptions>()
    .Bind(builder.Configuration.GetSection(AzureSearchOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(
            options.ServiceUrl,
            UriKind.Absolute,
            out _),
        "AzureSearch:ServiceUrl must be a valid absolute URL.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "AzureSearch:ApiKey is required.")
    .Validate(
        options => options.VectorDimensions > 0,
        "AzureSearch:VectorDimensions must be greater than zero.");

builder.Services
    .AddOptions<FoundryOptions>()
    .Bind(builder.Configuration.GetSection(FoundryOptions.SectionName))
    .Validate(
        options => Uri.TryCreate(
            options.ServiceUrl,
            UriKind.Absolute,
            out _),
        "Foundry:ServiceUrl must be a valid absolute URL.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "Foundry:ApiKey is required.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(
            options.EmbeddingDeploymentName),
        "Foundry:EmbeddingDeploymentName is required.");

builder.Build().Run();
