using AIbillingRAGBuilder.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Workers
{
    public class AzureBackgroundWorkerService
    {
        private const string QueueName_High = "work-items-high";
        private const string QueueName_Normal = "work-items-normal";
        private const string storageConnectionName = "AzureWebJobsStorage";
        private readonly ILogger<AzureBackgroundWorkerService> _logger;
        private readonly IAISearchKeywordAndVectorPushService _searchKeywordAndVectorPushService;

        private static readonly SemaphoreSlim _lowqueueSemaphore = new SemaphoreSlim(1, 1);

        public AzureBackgroundWorkerService(
            ILogger<AzureBackgroundWorkerService> logger,
            IAISearchKeywordAndVectorPushService aiSearchService)
        {
            _logger = logger;
            _searchKeywordAndVectorPushService = aiSearchService;
        }

        [Function("ProcessLowWorkQueue")]
        public async Task RunLow(
            [QueueTrigger(QueueName_Normal,
                Connection = storageConnectionName)]
            string message,
            CancellationToken cancellationToken)
        {
            await _lowqueueSemaphore.WaitAsync(cancellationToken);

            _logger.LogInformation(
                "Received: {Message}",
                message);

            WorkItem? item;

            try
            {
                try
                {
                    item = JsonSerializer.Deserialize<WorkItem>(
                        message,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to deserialize queue message: {Message}",
                        message);

                    return;
                }

                if (item == null)
                {
                    _logger.LogWarning(
                        "Queue message could not be converted to WorkItem: {Message}",
                        message);

                    return;
                }

                _logger.LogInformation(
                    "Processing work item {Id} of type {Type}",
                    item.Id,
                    item.WorkType);

                _logger.LogInformation("Processing work item {Id} of type {Type}", item.Id, item.WorkType);

                await ProcessHelper(item, cancellationToken);
            }
            finally
            {
                _lowqueueSemaphore.Release();
            }
        }


        [Function("ProcessHighWorkQueue")]
        public async Task RunHigh(
            [QueueTrigger(QueueName_High,
                Connection = storageConnectionName)]
            string message,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Received: {Message}",
                message);

            WorkItem? item;

            try
            {
                item = JsonSerializer.Deserialize<WorkItem>(
                    message,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to deserialize queue message: {Message}",
                    message);

                return;
            }

            if (item == null)
            {
                _logger.LogWarning(
                    "Queue message could not be converted to WorkItem: {Message}",
                    message);

                return;
            }

            _logger.LogInformation(
                "Processing work item {Id} of type {Type}",
                item.Id,
                item.WorkType);

            _logger.LogInformation("Processing work item {Id} of type {Type}", item.Id, item.WorkType);

            await ProcessHelper(item, cancellationToken);
        }

        private async Task ProcessHelper(WorkItem item, CancellationToken cancellationToken)
        { 
            // Placeholder implementations for different work types.
            switch (item.WorkType)
            {
                case WorkType.LoadData:
                    await _searchKeywordAndVectorPushService.LoadDataWorker();
                    _logger.LogInformation("Load work done");
                    break;
                case WorkType.DeleteIndex:
                    await _searchKeywordAndVectorPushService.DeleteIndexWorker(item.Payload ?? "AMPMDefault");
                    _logger.LogInformation("Delete index {name}", item.Payload ?? "AMPMDefault");
                    break;
                case WorkType.BuildSearchEngine:
                    await _searchKeywordAndVectorPushService.BuildAISearchWorker(item.Payload ?? "AMPMDefault");
                    _logger.LogInformation("Search engine is completed for {Id}", item.Id);
                    break;
                case WorkType.AddHistoryData:
                    var json = JsonDocument.Parse(item.Payload!);

                    var serviceName = json.RootElement.GetProperty("ServiceName").GetString();
                    var bs = DateTime.TryParse(json.RootElement.GetProperty("StartDate").GetString(), out var startDate);
                    var be = DateTime.TryParse(json.RootElement.GetProperty("EndDate").GetString(), out var endDate);

                    if (string.IsNullOrEmpty(serviceName) || !bs || !be)
                    {
                        _logger.LogWarning("Invalid payload for AddData work item {Id}: {Payload}", item.Id, item.Payload);
                        return;
                    }
                    await _searchKeywordAndVectorPushService.AddOrdersToAISearchWorker(serviceName, startDate, endDate, cancellationToken);
                    _logger.LogInformation($"*********** Adding history data from {startDate} to {endDate}", item.Id);


                    break;
                case WorkType.AddData:
                    json = JsonDocument.Parse(item.Payload!);

                    serviceName = json.RootElement.GetProperty("ServiceName").GetString();
                    var orderId = json.RootElement.GetProperty("OrderId").GetString();

                    if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(orderId))
                    {
                        _logger.LogWarning("Invalid payload for AddData work item {Id}: {Payload}", item.Id, item.Payload);
                        return;
                    }

                    await _searchKeywordAndVectorPushService.AddDataWorker(serviceName, orderId);
                    _logger.LogInformation("Adding data is completed for {Id}", item.Id);
                    break;
                case WorkType.BillingDecision:
                    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                    _logger.LogInformation("BillingDecision completed for {Id}", item.Id);
                    break;
                default:
                    _logger.LogWarning("Unknown work type for item {Id}", item.Id);
                    break;
            }
        }
    }
}
