using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AIbillingRAGBuilder.Services.Decision
{
    public class AIDecisionConsumer
    {
       
        private readonly ILogger<OrderRagIndexFunction> _logger;
        private readonly IAIDecisionService _aiDecisionService;
        private readonly IDatabaseService _databaseService;
        private readonly IBillingDecisionService _billingDecisionService;

        private readonly AzureSearchOptions _azureSearchOptions;

        public AIDecisionConsumer(ILogger<OrderRagIndexFunction> logger, IAIDecisionService aIDecisionService, 
            IDatabaseService databaseService,
            IBillingDecisionService billingDecisionService,
            IOptions<AzureSearchOptions> azureSearchOptions)
        {
            this._logger = logger;
            _aiDecisionService = aIDecisionService;
            _databaseService = databaseService;
            _billingDecisionService = billingDecisionService;
            _azureSearchOptions = azureSearchOptions.Value;
        }


        public async Task ConsumeDecision(string orderId)
        {
            var serviceName = _azureSearchOptions.ServiceName;
            await ConsumeDecision(serviceName, orderId);
        }

        public async Task ConsumeDecision(string serviceName, string orderId)
        {
            _logger.LogInformation($"Consuming decision for order {orderId} using service {serviceName}");

            var order = await _databaseService.GetFullOrderBySqlAsync(orderId, default);
            if (order == null)
            {
                _logger.LogInformation($"Order {orderId} not found in the database.");
                return;
            }

            var result = await _aiDecisionService.DecideAsync(order, serviceName, default);
            if (string.IsNullOrWhiteSpace(result.OrderId))
            {
                result.OrderId = order.WorkOrderId.ToString();
            }

            await _billingDecisionService.WriteDecisionAsync(result);

            var resultJson = JsonSerializer.Serialize(
                        result,
                        new JsonSerializerOptions { WriteIndented = true });

                                _logger.LogInformation(
                                    """
    
                        **********************************
                        This order TRUE status: {BillingStatus}
                        **********************************
                        {Result}
                        **********************************
    
                        """,
                        order.BillingStatus,
                        resultJson);
        }


        public async Task ConsumeDecisions(string serviceName)
        {
            var orders = await _databaseService.Get100FullOrdersAsync(DateTime.Now.AddDays(-30), DateTime.Now);
            int correctCount = 0;
            int totalCount = 0;
            foreach (var order in orders)
            {
                var result = await _aiDecisionService.DecideAsync(order, serviceName, default);
                if (string.IsNullOrWhiteSpace(result.OrderId))
                {
                    result.OrderId = order.WorkOrderId.ToString();
                }

                await _billingDecisionService.WriteDecisionAsync(result);

                if(result.Billing_Status.ToString() == order.BillingStatus.ToString())
                {
                    correctCount++;
                }
                totalCount++;

                var resultJson = JsonSerializer.Serialize(
                        result,
                        new JsonSerializerOptions { WriteIndented = true });

                _logger.LogInformation(
                    """
    
                        **********************************
                        This order TRUE status: {BillingStatus}
                        **********************************
                        {Result}
                        **********************************
    
                        """,
                    order.BillingStatus,
                    resultJson);

                _logger.LogInformation($"Correctly identified {correctCount} out of {totalCount} orders as billable.");
                Thread.Sleep(1000); // Sleep for 1 second to avoid overwhelming the AI service
            }

        }
    }
}
