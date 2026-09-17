using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Services;
using AIbillingRAGBuilder.Services.Decision;
using AIbillingRAGBuilder.Services.Interfaces;
using Azure.Messaging.EventHubs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AIbillingRAGBuilder
{
    public class OrderRagIndexFunction
    {
        private readonly ILogger<OrderRagIndexFunction> _logger;
        private readonly IServiceProvider _services;
        private readonly AIDecisionConsumer _aiDecisionConsumer;
        //private const string serviceName = "zhbTestSearchVectorPushService";

        public OrderRagIndexFunction(
            ILogger<OrderRagIndexFunction> logger,
            IServiceProvider services,
            AIDecisionConsumer aiDecisionConsumer)
        {
            _logger = logger;
            _logger.LogInformation("Initializing Order RAG Index Function");
            _services = services;
            _aiDecisionConsumer = aiDecisionConsumer;
        }

        [Function("Connection")]
        public async Task<IActionResult> ConnectionTest([HttpTrigger(AuthorizationLevel.Anonymous, "get",
            Route = "connection")] HttpRequest req)
        {
            _logger.LogInformation("Service connection test. ");

            return new OkObjectResult("Service connection test done.");
        }

        [Function("LoadData")]
        public async Task<IActionResult> LoadData([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("Load data");
            var searchService =
                _services.GetRequiredService<IAISearchKeywordAndVectorPushService>();

            await searchService.LoadData();

            return new OkObjectResult("Load data done.");
        }


        [Function("AddData")]
        public async Task<IActionResult> AddData([HttpTrigger(AuthorizationLevel.Function, "Post",
            Route = "index/{indexName}")] HttpRequest req, string indexName)
        {
            _logger.LogInformation($"Add data into index {indexName}");

            var orderId = req.Query["orderid"].ToString();
            var searchService =
                _services.GetRequiredService<IAISearchKeywordAndVectorPushService>();

            await searchService.AddData(indexName, orderId);

            return new OkObjectResult("Add data into index done.");
        }


        [Function("DeleteIndex")]
        public async Task<IActionResult> DeleteIndex([HttpTrigger(AuthorizationLevel.Function, "delete",
            Route = "index/{indexName}")] HttpRequest req, string indexName)
        {
            _logger.LogInformation($"Delete index {indexName}");
            var searchService =
                _services.GetRequiredService<IAISearchKeywordAndVectorPushService>();

            await searchService.DeleteIndex(indexName);

            return new OkObjectResult("Delete index done.");
        }


        [Function("SearchOrder")]
        public async Task<IActionResult> SearchOrder([HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "searchorder/{serviceName}/{orderId}")] HttpRequest req, string serviceName, string orderId)
        {// for testing the RAG index search
            _logger.LogInformation("Search Order RAG");

            //var decisionService =
            //    _services.GetRequiredService<IAIDecisionService>();
            //var databaseService =
            //    _services.GetRequiredService<IDatabaseService>();
            //var aidecisionService =
            //    new AIDecisionConsumer(_logger, decisionService, databaseService);

            await _aiDecisionConsumer.ConsumeDecision(serviceName, orderId);

            // { Priority = WorkPriority.High}

            return new OkObjectResult("Search Order done.");
        }


        [Function("SearchBatchOrder")]
        public async Task<IActionResult> SearchBatchOrder([HttpTrigger(AuthorizationLevel.Function, "get",
            Route = "searchorder/{serviceName}")] HttpRequest req, string serviceName)
        {// for testing the RAG index search
            _logger.LogInformation("Search Order RAG");

            //var decisionService =
            //    _services.GetRequiredService<IAIDecisionService>();
            //var databaseService =
            //    _services.GetRequiredService<IDatabaseService>();
            //var aidecisionService =
            //    new AIDecisionConsumer(_logger, decisionService, databaseService);

            await _aiDecisionConsumer.ConsumeDecision(serviceName);

            // { Priority = WorkPriority.High}

            return new OkObjectResult("Search Order done.");
        }


        [Function("BuildAIService")]
        public async Task<IActionResult> BuildAIService([HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "buildaiservice/{serviceName}")] HttpRequest req, string serviceName)
        {
            _logger.LogInformation("Build AI Service");
            // Retrieve all related tables data
            //var aiService = new AISearchKeywordOnlyPullService(_logger);
            //await aiService.BuildAISearchService("zhbTestSearchService");

            //var aiVectorService = new AISearchKeywordAndVectorPullService(_logger);
            //await aiVectorService.BuildAISearchService("zhbTestSearchVectorService");

            var searchService =
                _services.GetRequiredService<IAISearchKeywordAndVectorPushService>();

            await searchService.StartBuildingAISearch(serviceName);

            return new OkObjectResult("Order RAG is building.");
        }


        [Function("AddHistoryData")]
        public async Task<IActionResult> AddHistoryDataForAIService([HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "addhistorydata/{serviceName}")] HttpRequest req, string serviceName)
        {
            _logger.LogInformation("Adding history data for AI Service");
            
            var searchService =
                _services.GetRequiredService<IAISearchKeywordAndVectorPushService>();

            await searchService.AddHistoryData(serviceName);

            return new OkObjectResult($"Adding history data for AI Service {serviceName} done.");
        }


        [Function("AIServiceStatusFunction")]
        public async Task<IActionResult> GetAIService([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
        {
            _logger.LogInformation("Get AI Service status");
            
            var searchService =
                _services.GetRequiredService<IAISearchKeywordAndVectorPushService>();
            var results = await searchService.GetIndexingResults();

            if(results.Count == 0)
                return new OkObjectResult("Setting is not done...");

            foreach (var result in results)
            {
                _logger.LogInformation($"{result}");
            }


            return new OkObjectResult("Get status done.");
        }

        [Function("ClosedOrderConsumer")]
        public async Task ClosedOrderConsumerRun(
                    [EventHubTrigger(
                        "closedorders",
                        Connection = "ClosedOrderEventHubConnection",
                        ConsumerGroup = "$Default")]
                    EventData[] events)
        {
           
            foreach (var eventData in events)
            {
                string body = eventData.EventBody.ToString();

                _logger.LogInformation(
                    "Received closed order event: {Body}",
                    body);
                var workOrderEvent =
                    JsonSerializer.Deserialize<ClosedOrderEvent>(body);

                await _aiDecisionConsumer.ConsumeDecision(workOrderEvent?.WorkOrderId);
            }
        }

        //[Function("RebuildOrderRagTimer")]
        //public async Task RebuildOrderRagTimer([TimerTrigger("* */5 * * * *")] TimerInfo timer)
        //{
        //    _logger.LogInformation("Timer started.");

        //    // Find orders needing indexing
        //    // Build missing indexes
        //    // Retry failed indexes

        //    _logger.LogInformation("Timer finished.");
        //}
    }
}
