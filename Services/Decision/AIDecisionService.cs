
using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Models;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIbillingRAGBuilder.Services.Decision
{
    public interface IAIDecisionService
    {
        Task<BillingDecisionResponse> DecideAsync(
            WorkOrderDto order, string servicename,
            CancellationToken cancellationToken = default);
    }

    public class AIDecisionService : IAIDecisionService
    { // Azure Search service configuration
        //private const string AZURE_SEARCH_SERVICE_URL = "https://crcautobillingsearchservice.search.windows.net";

        ////Azure AI Foundry configuration
        //private const string AI_FOUNDRY_AI_SERVICES_URL = "https://crc-autobilling-aisearch.cognitiveservices.azure.com/";

        private SearchClient _searchClient;
        private ChatClient _chatClient;
        private AIDecisionOptions _options;

        private readonly ILogger<OrderRagIndexFunction> _logger;
        private readonly IBillingPromptBuilder _billingPromptBuilder;

        private readonly AzureOpenAIClient azureOpenAIClient;

        private readonly AzureSearchOptions _searchOptions;
        private readonly FoundryOptions _foundryOptions;

        public AIDecisionService(
            IOptions<AzureSearchOptions> searchOptions,
            IOptions<FoundryOptions> foundryOptions,
            IBillingPromptBuilder billingPromptBuilder,
            ILogger<OrderRagIndexFunction> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _searchOptions = searchOptions.Value;
            _foundryOptions = foundryOptions.Value;

            _logger = logger;
            _billingPromptBuilder = billingPromptBuilder;

            azureOpenAIClient = new AzureOpenAIClient(
                new Uri(_foundryOptions.ServiceUrl),
                new AzureKeyCredential(_foundryOptions.ApiKey));
            
        }

        /// <summary>
        /// Determines whether a new work order or billable item should be charged.
        /// </summary>
        public async Task<BillingDecisionResponse> DecideAsync(
            WorkOrderDto order, string servicename,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Starting AI charge decision for customer {CustomerName}, work order {WorkOrderId}.",
                order.StoreName,
                order.WorkOrderId);
            servicename = servicename.ToLower();

            _searchClient = new SearchClient(
                new Uri(_searchOptions.ServiceUrl),
                $"{servicename}-index",
                new AzureKeyCredential(_searchOptions.ApiKey));

            var options = Options.Create(new AIDecisionOptions
            {
                ChatDeploymentName = "zhb-gpt-5.6-sol",
                SemanticConfigurationName =
                $"{servicename}-semantic",
                VectorFieldName = "content_vector",
                ContractResultCount = 3,
                HistoricalResultCount = 5
            });

            _options = options.Value;

            if (string.IsNullOrWhiteSpace(_options.ChatDeploymentName))
            {
                throw new ArgumentException(
                    "ChatDeploymentName must be configured.",
                    nameof(_options));
            }

            _chatClient = azureOpenAIClient.GetChatClient(
                _options.ChatDeploymentName);

            var result = await GenerateDecisionAsync(
                order,
                cancellationToken);

            _logger.LogInformation(
                "AI decision completed for work order {WorkOrderId}. Decision={Decision}, confidence={Confidence}.",
                order.WorkOrderId,
                result.Billing_Status,
                result.Confidence);

            return result;
        }


        private async Task<BillingDecisionResponse> GenerateDecisionAsync(
            WorkOrderDto order, 
            CancellationToken cancellationToken)
        {
            var historyOrders = await SearchHistoricalOrdersAsync(order, cancellationToken);
            _logger.LogInformation($"History orders search done. Count: {historyOrders.Count}");

            var specialRules = await SearchSpecialBillingRulesAsync(order, cancellationToken);
            _logger.LogInformation($"Special billing rules search done. Count: {specialRules.Count}");

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(_billingPromptBuilder.BuildSystemPrompt()),

                new UserChatMessage(_billingPromptBuilder.BuildUserPrompt(order,specialRules, historyOrders))
            };

            var completionOptions = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            ChatCompletion completion;

            try
            {
                completion = await _chatClient.CompleteChatAsync(
                    messages,
                    completionOptions,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Azure OpenAI failed while deciding work order {WorkOrderId}.",
                    order.WorkOrderId);

                throw;
            }

            var responseText = string.Concat(
                completion.Content.Select(part => part.Text));

            if (string.IsNullOrWhiteSpace(responseText))
            {
                throw new InvalidOperationException(
                    "Azure OpenAI returned an empty decision.");
            }

            try
            {
                var decision = JsonSerializer.Deserialize<BillingDecisionResponse>(
                    responseText,
                    JsonOptions);

                if (decision is null)
                {
                    throw new JsonException(
                        "The AI decision response was null.");
                }

                return decision;
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Could not parse AI decision JSON. Response: {Response}",
                    responseText);

                return new BillingDecisionResponse
                {
                };
            }
        }

        private static readonly JsonSerializerOptions JsonOptions =
            new(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };
    
    
        private async Task<List<SpecialBillingRule>> SearchSpecialBillingRulesAsync(
            WorkOrderDto order,
            CancellationToken cancellationToken = default)
        {
            var queryText = _billingPromptBuilder.BuildOrderSearchText(order);

            var matchedRules = new List<SpecialBillingRule>();
            SpecialBillingRules.All
                .Where(rule =>
                    rule.Region.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                    rule.Region.Equals(order.CountryString, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .ForEach(rule =>
                {
                    if (rule.TriggerPhrases.Any(phrase =>
                        order.OrderContent.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
                    {
                        matchedRules.Add(rule);
                    }
                });
            return matchedRules;
        }

        private async Task<List<HistoricalOrderEvidence>> SearchHistoricalOrdersAsync(
                                WorkOrderDto order,
                                CancellationToken cancellationToken = default)
        {
            var queryText = _billingPromptBuilder.BuildOrderSearchText(order);

            var options = new SearchOptions
            {
                Size = 10,
                QueryType = SearchQueryType.Semantic,
                SemanticSearch = new SemanticSearchOptions
                {
                    SemanticConfigurationName = _options.SemanticConfigurationName,
                    /*
                     * The semantic ranker uses this text to rerank
                     * the hybrid search candidates.
                     */
                    SemanticQuery = queryText
                },
                VectorSearch = new VectorSearchOptions
                {
                    /*
                     * Apply document type, status and current-order
                     * exclusion before vector nearest-neighbor search.
                     */
                    FilterMode = VectorFilterMode.PreFilter
                },
                
                Filter =
                    $"document_type eq 'Order' " +
                    $"and work_order_id ne '{EscapeOData(order.WorkOrderId.ToString())}' " +
                    "and (billing_status eq 'Billable' or billing_status eq 'Free')"
            };

            // Keyword/BM25 portion of the hybrid query.
            options.SearchFields.Add("merged_text");

            options.Select.Add("document_id");
            options.Select.Add("document_name");
            options.Select.Add("document_type");
            options.Select.Add("work_order_id");
            options.Select.Add("billing_status");
            options.Select.Add("store_id");
            options.Select.Add("store_name");
            options.Select.Add("chunk_number");
            options.Select.Add("merged_text");

            options.VectorSearch.Queries.Add(
                new VectorizableTextQuery(queryText)
                {
                    KNearestNeighborsCount = 20,
                    Fields = { "content_vector" }
                });
            

            var response =
                await _searchClient.SearchAsync<DocumentUnifiedSearchDto>(
                    searchText: queryText,
                    options,
                    cancellationToken);

            var history = new List<HistoricalOrderEvidence>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var document = result.Document;

                history.Add(new HistoricalOrderEvidence
                {
                    DocumentId = document.DocumentId,
                    WorkOrderId = document.WorkOrderId ?? string.Empty,

                    // Only do this mapping if it matches your real business rule.
                    Decision = document.BillingStatus.ToString(),

                    RetrievalScore = result.Score,
                    RerankerScore = result.SemanticSearch?.RerankerScore,
                    Content = document.MergedText
                });
            }

            return history.OrderByDescending(x => x.RerankerScore).ToList();
        }
        public static string EscapeOData(string value)
        {
            return value.Replace("'", "''");
        }
    }
}