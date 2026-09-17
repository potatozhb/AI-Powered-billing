using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Services
{
    public interface IDocumentService
    {
        Task<DocumentUnifiedSearchDto> GetOrderDocumentAsync(string orderId, CancellationToken cancellationToken = default);
        Task<List<DocumentUnifiedSearchDto>> GetOrderDocumentsAsync(CancellationToken cancellationToken = default);
        Task<List<DocumentUnifiedSearchDto>> GetOrderDocumentsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<List<DocumentUnifiedSearchDto>> GetContractDocumentsAsync(CancellationToken cancellationToken = default);
        Task<List<DocumentUnifiedSearchDto>> GetContractDocumentsAsync(List<string> storeIds, CancellationToken cancellationToken = default);
        Task<List<DocumentUnifiedSearchDto>> GetSpecialRuleDocumentsAsync(CancellationToken cancellationToken = default);

    }

    public class DocumentService : IDocumentService
    {
        private readonly ILogger<OrderRagIndexFunction> _logger;
        private readonly IDatabaseService _databaseService;

        public DocumentService(ILogger<OrderRagIndexFunction> logger, IDatabaseService databaseService)
        {
            _logger = logger;
            _databaseService = databaseService;
        }

        public async Task<List<DocumentUnifiedSearchDto>> GetSpecialRuleDocumentsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var docSpecialRules = new List<DocumentUnifiedSearchDto>();
                var rules = SpecialBillingRules.All;

                foreach (var rule in rules)
                {
                    var doc = new DocumentUnifiedSearchDto
                    {
                        // Common document identity
                        DocumentId = $"{rule.RuleId}",
                        DocumentName = $"SpecialRule:{rule.RuleId}",
                        DocumentType = DocumentType.SpecialRule.ToString(),

                        // Special rule fields
                        RuleType = rule.RuleType.ToString(),
                        RuleRegion = rule.Region,
                        RuleAction = rule.Action.ToString(),
                        RuleReason = rule.Reason,

                        MergedText = rule.MergedText
                    };
                    docSpecialRules.Add(doc);
                }

                _logger.LogInformation($"Total documents processed: {docSpecialRules.Count}");

                return docSpecialRules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading order documents from the database.");
                return new List<DocumentUnifiedSearchDto>();
            }
        }

        public async Task<DocumentUnifiedSearchDto> GetOrderDocumentAsync(string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                var order = await _databaseService.GetFullOrderBySqlAsync(orderId);
                if (order == null)
                {
                    _logger.LogWarning("Order with ID {OrderId} not found.", orderId);
                    return null;
                }
                var documents = await BuildOrderDocumentBatchAsync(new List<WorkOrderDto> { order }, cancellationToken);
                return documents.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading order document from the database for Order ID {OrderId}.", orderId);
                return null;
            }
        }

        public async Task<List<DocumentUnifiedSearchDto>> GetOrderDocumentsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var orders = await _databaseService.GetFullOrdersAsync(startDate, endDate);
            return await GetOrderDocumentsHelperAsync(orders, cancellationToken);
        }

        public async Task<List<DocumentUnifiedSearchDto>> GetOrderDocumentsAsync(CancellationToken cancellationToken = default)
        {
            var orders = await _databaseService.GetFullOrdersAsync(DateTime.Now.AddDays(-10), DateTime.Now);
            return await GetOrderDocumentsHelperAsync(orders, cancellationToken);
        }

        private async Task<List<DocumentUnifiedSearchDto>> GetOrderDocumentsHelperAsync (List<WorkOrderDto> orders, CancellationToken cancellationToken = default)
        { 
            const int batchSize = 1000;
            const int maxParallelBatches = 8;

            var orderDocumentBatches = new ConcurrentBag<List<DocumentUnifiedSearchDto>>();
            try
            {
                // include billed or closed
                var closedOrders = orders.Where(o => o.Status != OrderStatus.Canceled && o.Status != OrderStatus.Pending && o.StoreId != 0).ToList();

                var batches = orders.Chunk(batchSize).ToArray();

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallelBatches,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(batches, parallelOptions,
                    async (orderBatch, token) =>
                    {
                        var documents = await BuildOrderDocumentBatchAsync(orderBatch, cancellationToken);
                        orderDocumentBatches.Add(documents);

                        _logger.LogInformation("Processed batch containing {DocumentCount} documents.", documents.Count);
                    });
                var orderDocuments = orderDocumentBatches
                                        .SelectMany(batch => batch)
                                        .OrderBy(document => document.DocumentId)
                                        .ToList();

                _logger.LogInformation($"Total documents processed: {orderDocuments.Count}");

                return orderDocuments.OrderBy(x => x.WorkOrderId).ThenBy(x => x.ChunkNumber).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading order documents from the database.");
                return new List<DocumentUnifiedSearchDto>();
            }
        }
        
        public async Task<List<DocumentUnifiedSearchDto>> GetContractDocumentsAsync(List<string> storeIds, CancellationToken cancellationToken = default)
        {
            var contracts = await _databaseService.GetFullContractsAsync(storeIds);
            return await GetContractDocumentsHelperAsync(contracts, cancellationToken);
        }

        public async Task<List<DocumentUnifiedSearchDto>> GetContractDocumentsAsync(CancellationToken cancellationToken = default)
        {
            var contracts = await _databaseService.GetFullContractsAsync();
            return await GetContractDocumentsHelperAsync(contracts, cancellationToken);
        }

        private async Task<List<DocumentUnifiedSearchDto>> GetContractDocumentsHelperAsync(List<ContractDto> contracts, CancellationToken cancellationToken = default)
        {
            const int batchSize = 1000;
            const int maxParallelBatches = 8;
            try
            {
                var totalWatch = Stopwatch.StartNew();

                var contractDocumentBatches = new ConcurrentBag<List<DocumentUnifiedSearchDto>>();

                // include billed or closed
                var batches = contracts.Chunk(batchSize).ToArray();

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxParallelBatches,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(batches, parallelOptions,
                    async (batch, token) =>
                    {
                        var documents = await BuildContractDocumentBatchAsync(batch, cancellationToken);
                        contractDocumentBatches.Add(documents);

                        _logger.LogInformation("Processed batch containing {DocumentCount} documents.", documents.Count);
                    });
                var contractDocuments = contractDocumentBatches
                                        .SelectMany(batch => batch)
                                        .OrderBy(document => document.DocumentId)
                                        .ToList();

                totalWatch.Stop();

                _logger.LogInformation($"Total documents processed: {contractDocuments.Count}, time: {totalWatch.Elapsed.TotalSeconds} s");

                return contractDocuments.OrderBy(x => x.ContractId).ThenBy(x => x.ChunkNumber).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading order documents from the database.");
                return new List<DocumentUnifiedSearchDto>();
            }
        }

        private async Task<List<DocumentUnifiedSearchDto>> BuildOrderDocumentBatchAsync(
            IReadOnlyCollection<WorkOrderDto> orders,
            CancellationToken cancellationToken = default)
        {

            var result = new List<DocumentUnifiedSearchDto>();

            foreach (var order in orders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (order.WorkOrderLines == null || order.WorkOrderLines.Count == 0)
                    {
                        continue;
                    }

                    var chunkNumber = 1;

                    foreach (var itemChunk in order.WorkOrderLines.Chunk(5))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var chunkItems = itemChunk.ToList();

                        var mergedText = order.OrderMetaContent + string.Join(
                            Environment.NewLine,
                            chunkItems.Select(item => item.CombinedContent));

                        result.Add(new DocumentUnifiedSearchDto
                        {
                            DocumentId = order.WorkOrderId.ToString() + "-" + chunkNumber,
                            DocumentName = DocumentType.Order.ToString() + ":" + order.WorkOrderId.ToString(),
                            DocumentType = DocumentType.Order.ToString(),
                            StoreId = order.StoreId.ToString(),
                            ChunkNumber = chunkNumber.ToString(),
                            MergedText = mergedText,
                            WorkOrderId = order.WorkOrderId.ToString(),
                            BillingStatus = order.BillingStatus.ToString(),
                            CountryString = order.CountryString,
                            ProjectId = order.ProjectId.ToString(),
                            InitialSeverity = order.InitialSeverity.ToString(),
                            CurrentSeverity = order.CurrentSeverity.ToString(),
                        });

                        chunkNumber++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to build search documents for order {OrderId}.",
                        order.WorkOrderId);
                }
            }

            return result;
            
        }

        private async Task<List<DocumentUnifiedSearchDto>> BuildContractDocumentBatchAsync(
            IReadOnlyCollection<ContractDto> contracts,
            CancellationToken cancellationToken = default)
        {
            var result = new List<DocumentUnifiedSearchDto>();

            foreach (var contract in contracts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (contract.Items == null || contract.Items.Count == 0)
                    {
                        continue;
                    }

                    var chunkNumber = 1;

                    foreach (var itemChunk in contract.Items.Chunk(5))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var chunkItems = itemChunk.ToList();

                        var mergedText = contract.MetaContent + string.Join(
                            Environment.NewLine,
                            chunkItems.Select(item => item.Content));

                        result.Add(new DocumentUnifiedSearchDto
                        {
                            DocumentId = contract.Id.ToString() + "-" + chunkNumber,
                            DocumentName = DocumentType.Contract.ToString() + ":" + contract.Id.ToString(),
                            DocumentType = DocumentType.Contract.ToString(),
                            StoreId = contract.StoreId.ToString(),
                            ChunkNumber = chunkNumber.ToString(),
                            MergedText = mergedText,
                            ContractId = contract.Id.ToString(),
                            ContractStatus = contract.Status.ToString(),
                            ContractItemIds = contract.Items?.Select(x => x.Id.ToString()).ToList(),
                            ProductNames = contract.Items?.Select(x => x.ProductName ?? string.Empty).ToList(),
                        });

                        chunkNumber++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to build search documents for contract {ContractId}.",
                        contract.Id);
                }
            }

            return result;
        }

    }
}
