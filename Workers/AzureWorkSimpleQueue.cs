using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIbillingRAGBuilder.Workers
{
    public class AzureWorkSimpleQueue: IAzureWorkQueue
    {
        private const string storageConnectionName = "AzureWebJobsStorage";
        private const string QueueName_High = "work-items-high";
        private const string QueueName_Normal = "work-items-normal";
        private readonly QueueClient _highQueue;
        private readonly QueueClient _lowQueue;
        private readonly ILogger<AzureWorkSimpleQueue> _logger;


        public AzureWorkSimpleQueue(IConfiguration configuration,
            ILogger<AzureWorkSimpleQueue> logger)
        {
            _logger = logger;

            var connectionString =
                configuration[storageConnectionName];

            var options = new QueueClientOptions
            {
                MessageEncoding = QueueMessageEncoding.Base64
            };

            _highQueue = new QueueClient(
                connectionString,
                QueueName_High,
                options);
            _lowQueue = new QueueClient(
                connectionString,
                QueueName_Normal,
                options);
        }

        public async Task EnqueueAsync(WorkItem item)
        {
            QueueClient queue =
                item.Priority == WorkPriority.High
                    ? _highQueue
                    : _lowQueue;

            await queue.CreateIfNotExistsAsync();

            var json = JsonSerializer.Serialize(item);

            var response = await queue.SendMessageAsync(json);

        }


    }
}
