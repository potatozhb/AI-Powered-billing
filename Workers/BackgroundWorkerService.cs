using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AIbillingRAGBuilder.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIbillingRAGBuilder.Workers
{
    /// <summary>
    /// Hosted background worker that pulls work items from IWorkQueue and processes them.
    /// Register as a singleton IWorkQueue and hosted service in DI (Program.cs) to use.
    /// </summary>
    public class BackgroundWorkerService : IHostedService, IDisposable
    {
        private readonly IWorkQueue _queue;
        private readonly ILogger<BackgroundWorkerService> _logger;
        private CancellationTokenSource? _cts;
        private Task? _executingTask;
        private IAISearchKeywordAndVectorPushService _searchKeywordAndVectorPushService;

        public BackgroundWorkerService(IWorkQueue queue, ILogger<BackgroundWorkerService> logger,
            IAISearchKeywordAndVectorPushService aISearchKeywordAndVectorPushService)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _searchKeywordAndVectorPushService = aISearchKeywordAndVectorPushService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackgroundWorkerService starting.");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = Task.Run(() => ExecuteAsync(_cts.Token));

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackgroundWorkerService stopping.");

            if (_cts == null)
                return;

            _cts.Cancel();

            if (_executingTask != null)
            {
                await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);
            }
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background worker loop started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var item = await _queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
                    await ProcessItemAsync(item, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing background work item.");
                }
            }

            _logger.LogInformation("Background worker loop stopped.");
        }

        private async Task ProcessItemAsync(WorkItem item, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing work item {Id} of type {Type}", item.Id, item.WorkType);

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
                    await _searchKeywordAndVectorPushService.AddOrdersToAISearchWorker(serviceName, startDate, endDate);
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
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("BillingDecision completed for {Id}", item.Id);
                    break;
                default:
                    _logger.LogWarning("Unknown work type for item {Id}", item.Id);
                    break;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
