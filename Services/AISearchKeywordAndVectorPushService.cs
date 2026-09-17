using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Workers;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AIbillingRAGBuilder.Services
{
    public interface IAISearchKeywordAndVectorPushService
    {
        Task LoadData();
        Task LoadDataWorker();
        Task StartBuildingAISearch(string serviceName);
        Task<IList<string>> GetIndexingResults();
        Task BuildAISearchWorker(string serviceName);

        Task AddHistoryData(string serviceName);
        Task AddOrdersToAISearchWorker(string serviceName, DateTime startDate = default, DateTime endDate = default, CancellationToken cancellationToken = default);
        Task DeleteIndex(string serviceName);
        Task DeleteIndexWorker(string serviceName);
        Task AddData(string serviceName, string orderId);
        Task AddDataWorker(string serviceName, string orderId);
    }

    public class AISearchKeywordAndVectorPushService : IAISearchKeywordAndVectorPushService
    {
        //push model to build the indexer and skillset for the Azure Search service

        //// Azure Search service configuration
        //private const string AZURE_SEARCH_SERVICE_URL = "https://crcautobillingsearchservice.search.windows.net";
        
        //// Azure AI Foundry configuration
        //private const string AI_FOUNDRY_AI_SERVICES_URL = "https://crc-autobilling-aisearch.cognitiveservices.azure.com/";

        //// Azure AI Foundry embedding model configuration
        //private const string FOUNDRY_EMBEDDING_DEPLOYMENT_NAME = "text-embedding-3-small-code-vector";
        //private const string FOUNDRY_EMBEDDING_MODEL_NAME = "text-embedding-3-small";

        //private const int VECTOR_DIMENSIONS = 1536; // Dimension of the embedding vector
        //private const string VECTOR_SEARCH_PROFILE_NAME = "vector-profile"; // Name of the vector search profile
        //private const string VECTOR_SEARCH_ALGORITHM_CONFIG_NAME = "myHnsw";
        //private const string VECTOR_SEARCH_VECTORIZER_NAME = "myFoundry"; // option includes: OpenAI, Foundry, Custom, None

        private readonly ILogger<AISearchKeywordAndVectorPushService> _logger;

        private readonly EmbeddingClient _embeddingClient;
        private readonly IDocumentService _documentService;
        private readonly IWorkQueue _workQueue;
        private readonly IAzureWorkQueue _azureWorkSimpleQueue;
        private IReadOnlyList<IndexingResult> _indexingResults;
        private readonly AzureSearchOptions _searchOptions;
        private readonly FoundryOptions _foundryOptions;
        private static readonly SemaphoreSlim _embeddingSemaphore = new SemaphoreSlim(10, 10);

        public AISearchKeywordAndVectorPushService(ILogger<AISearchKeywordAndVectorPushService> logger,
            IDocumentService documentService, IWorkQueue workQueue, IAzureWorkQueue azureQueue,
            IOptions<AzureSearchOptions> searchOptions,
            IOptions<FoundryOptions> foundryOptions)
        {
            _logger = logger;

            _documentService = documentService;
            _workQueue = workQueue;
            _azureWorkSimpleQueue = azureQueue; 
            _searchOptions = searchOptions.Value;
            _foundryOptions = foundryOptions.Value;

            _indexingResults = new List<IndexingResult>();
            
            // create embedding client
            var openAIClient = new AzureOpenAIClient(new Uri(_foundryOptions.ServiceUrl), new AzureKeyCredential(_foundryOptions.ApiKey));
            _embeddingClient = openAIClient.GetEmbeddingClient(_foundryOptions.EmbeddingDeploymentName);
        }

        public async Task LoadData()
        {
            await _azureWorkSimpleQueue.EnqueueAsync(new WorkItem(WorkType.LoadData));
            //await _workQueue.Enqueue(new WorkItem(WorkType.LoadData));
        }

        public async Task DeleteIndex(string serviceName)
        {
            await _azureWorkSimpleQueue.EnqueueAsync(new WorkItem(WorkType.DeleteIndex, serviceName) { Priority = WorkPriority.High});
        }

        public async Task StartBuildingAISearch(string serviceName)
        {
            await _azureWorkSimpleQueue.EnqueueAsync(new WorkItem(WorkType.BuildSearchEngine, serviceName));
        }

        public async Task AddHistoryData(string serviceName)
        {
            var start = new DateTime(2025, 1, 1);
            var end = start.AddDays(5);
            int count = 0;
            while (start < DateTime.Now)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    ServiceName = serviceName,
                    StartDate = start,
                    EndDate = end,
                });

                await _azureWorkSimpleQueue.EnqueueAsync(new WorkItem(WorkType.AddHistoryData, payload));
                start = end;
                end = start.AddDays(5);
                count++;
            }
            _logger.LogInformation($"Added history task to the queue, count: {count}");
        }

        public async Task AddData(string serviceName, string orderId)
        {
            var payload = JsonSerializer.Serialize(new
            {
                ServiceName = serviceName,
                OrderId = orderId
            });

            await _azureWorkSimpleQueue.EnqueueAsync(new WorkItem(WorkType.AddData, payload));
        }

        public async Task AddDataWorker(string serviceName, string orderId)
        {
            var doc = await _documentService.GetOrderDocumentAsync(orderId);
            // create search client
            var searchClient = new SearchClient(new Uri(_searchOptions.ServiceUrl), serviceName + "-index", new AzureKeyCredential(_searchOptions.ApiKey));

            // Upload documents to the index
            // Upload in batches of 100, rate limit
            var ruleResults = await UploadInBatchesAsync(
                    searchClient, new List<DocumentUnifiedSearchDto> { doc} );
        }

        public async Task LoadDataWorker()
        {
            //load data
            var documents = await _documentService.GetOrderDocumentsAsync();
            var doc1000 = documents.ToList();
        }

        public async Task<IList<string>> GetIndexingResults()
        {
            var res = new List<string>();

            foreach (var item in _indexingResults)
            {
                res.Add($"{item.Key}  Success={item.Succeeded}");
            }
            return res;
        }

        public async Task DeleteIndexWorker(string serviceName)
        {
            serviceName = serviceName.ToLower();
            _logger.LogInformation($"Delete AI Search service <{serviceName}>...");
            var credential = new AzureKeyCredential(_searchOptions.ApiKey);
            var index_name = $"{serviceName}-index";
            var indexClient = new SearchIndexClient(new Uri(_searchOptions.ServiceUrl), credential);
            await indexClient.DeleteIndexAsync(index_name, cancellationToken: new CancellationToken());

        }

        public async Task BuildAISearchWorker(string serviceName)
        {
            _indexingResults = new List<IndexingResult>();

            serviceName = serviceName.ToLower();
            _logger.LogInformation($"Building AI Search service <{serviceName}>...");
            // create a credential object with the admin key
            var credential = new AzureKeyCredential(_searchOptions.ApiKey);
            var index_name = $"{serviceName}-index";
            var data_source_name = $"{serviceName}-datasource";
            var indexer_name = $"{serviceName}-indexer";
            var semanticConfigName = $"{serviceName}-semantic";

            var index_description = $"Index for service <{serviceName}> searching keywords in the database";
            var indexer_description = $"Indexer for service <{serviceName}> indexing keywords in the database";

            // create a search index
            var indexClient = new SearchIndexClient(new Uri(_searchOptions.ServiceUrl), credential);
            var fields = new SearchField[]
            {
                new SearchField("document_id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true },
                new SearchField("document_name", SearchFieldDataType.String) { IsSearchable = true, IsFacetable = true },
                new SearchField("document_type", SearchFieldDataType.String) { IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("store_id", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
                new SearchField("store_name", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true, AnalyzerName=LexicalAnalyzerName.StandardLucene },
                new SearchField("chunk_number", SearchFieldDataType.String) { IsSearchable = true, IsSortable = true },

                new SearchField("work_order_id", SearchFieldDataType.String) { IsSearchable = true },
                new SearchField("billing_status", SearchFieldDataType.String) { IsSearchable = true , IsFilterable = true},
                new SearchField("initial_severity", SearchFieldDataType.String) { IsSearchable = true ,IsFilterable = true, IsSortable = true },
                new SearchField("current_severity", SearchFieldDataType.String) { IsSearchable = true ,IsFilterable = true, IsSortable = true },
                new SearchField("country", SearchFieldDataType.String) { IsSearchable = true ,IsFilterable = true, IsSortable = true },
                new SearchField("project_id", SearchFieldDataType.String) { IsSearchable = true ,IsFilterable = true, IsSortable = true },

                new SearchField("contract_id", SearchFieldDataType.String) { IsSearchable = true },
                new SearchField("contract_status", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true},
                new SearchField("contract_item_ids", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsSearchable = true, IsFacetable = true },
                new SearchField("product_names", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsSearchable = true, IsFacetable = true },

                new SearchField("rule_type", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true},
                new SearchField("rule_region", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true},
                new SearchField("rule_action", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true},
                new SearchField("rule_reason", SearchFieldDataType.String) { IsSearchable = true },

                // Add a vector field for semantic search
                new SearchField("merged_text", SearchFieldDataType.String) { IsSearchable = true, AnalyzerName=LexicalAnalyzerName.StandardLucene },
                new SearchField("content_vector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = _searchOptions.VectorDimensions, // Dimension of the embedding vector
                        VectorSearchProfileName = _searchOptions.VectorSearchProfileName + "-" + serviceName, // Name of the vector search profile
                    },

                // ContractDocumentDto
            };

            //Enable semantic, optional
            var semanticConfiguration = new SemanticConfiguration(
                    semanticConfigName,
                    new SemanticPrioritizedFields
                    {
                        TitleField = new SemanticField("document_name"),

                        ContentFields =
                        {
                            new SemanticField("merged_text"),
                        },

                        KeywordsFields =
                        {
                            new SemanticField("store_id"),
                            new SemanticField("document_type"),
                            new SemanticField("billing_status"),
                            new SemanticField("contract_status"),
                            new SemanticField("contract_item_ids"),
                            new SemanticField("product_names")
                        }
                    });

            var vectorSearch = new VectorSearch
            {
                Profiles =
                {
                    new VectorSearchProfile(_searchOptions.VectorSearchProfileName + "-" + serviceName, _searchOptions.VectorAlgorithmConfigurationName)
                    {
                        VectorizerName = _searchOptions.VectorizerName, // The name of the embedding model
                    }
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration(_searchOptions.VectorAlgorithmConfigurationName)
                },
                Vectorizers =
                {
                    new AzureOpenAIVectorizer(_searchOptions.VectorizerName)
                    {
                        Parameters = new AzureOpenAIVectorizerParameters
                        {
                            ResourceUri = new Uri(_foundryOptions.ServiceUrl),
                            DeploymentName = _foundryOptions.EmbeddingDeploymentName,
                            ModelName = _foundryOptions.EmbeddingModelName,
                            ApiKey = _foundryOptions.ApiKey
                        }
                    }
                }
            };

            var index = new SearchIndex(index_name, fields)
            {
                Description = index_description,
                SemanticSearch = new SemanticSearch
                {
                    DefaultConfigurationName = semanticConfigName,
                    Configurations = { semanticConfiguration }
                },
                VectorSearch = vectorSearch
            };

            var result = await indexClient.CreateOrUpdateIndexAsync(index);
            _logger.LogInformation($"Index <{index_name}> created or updated successfully.");

            // create search client
            var searchClient = new SearchClient(new Uri(_searchOptions.ServiceUrl), index_name, new AzureKeyCredential(_searchOptions.ApiKey));


            //load data
            var specialRules = await _documentService.GetSpecialRuleDocumentsAsync();

            // Upload documents to the index
            // Upload in batches of 100, rate limit
            var ruleResults =
                await UploadInBatchesAsync(
                    searchClient,
                    specialRules);
           
        }

        public async Task AddOrdersToAISearchWorker(string serviceName, DateTime startDate = default, DateTime endDate = default, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) return;

            _indexingResults = new List<IndexingResult>();

            serviceName = serviceName.ToLower();
            _logger.LogInformation($"Add data to AI Search service <{serviceName}>...");
            var index_name = $"{serviceName}-index";

            // create search client
            var searchClient = new SearchClient(new Uri(_searchOptions.ServiceUrl), index_name, new AzureKeyCredential(_searchOptions.ApiKey));

            // Load orders month by month
            var orderResults = new List<IndexingResult>();
            if (startDate == default)
            {// set a default date
                startDate = new DateTime(2020, 1, 1); 
                endDate = new DateTime(2020, 2, 1);
            }

            _logger.LogInformation(
                "Processing order documents from {StartDate} to {EndDate}",
                startDate,
                endDate);

            var documents = await _documentService.GetOrderDocumentsAsync(startDate, endDate);
           
            if (documents.Any())
            {
                var monthResults = await UploadInBatchesAsync(
                    searchClient,
                    documents);

                orderResults.AddRange(monthResults);
            }


            _indexingResults = orderResults.ToList();
        }


        private async Task<List<IndexingResult>> UploadInBatchesAsync(SearchClient searchClient,
                                                                         IEnumerable<DocumentUnifiedSearchDto> documents,
                                                                         int batchSize = 100)
        {
            var results = new List<IndexingResult>();
            int total = 0;
            foreach (var batch in documents.Chunk(batchSize))
            {
                try
                {
                    var vectorDoc = await GenerateVectorsAsync(batch.ToList());
                    var response = await searchClient.MergeOrUploadDocumentsAsync(vectorDoc);
                    total += vectorDoc.Count;
                    results.AddRange(response.Value.Results);

                    int succeeded =
                        response.Value.Results.Count(x => x.Succeeded);

                    int failed =
                        response.Value.Results.Count(x => !x.Succeeded);

                    _logger.LogInformation(
                        "Uploaded batch of {BatchSize} documents. Type: {DocType}. " +
                        "Succeeded: {Succeeded}; Failed: {Failed}. Total: {Total}",
                        batch.Length,
                        batch[0].DocumentType,
                        succeeded,
                        failed,
                        total);

                    foreach (var failedResult in
                             response.Value.Results.Where(x => !x.Succeeded))
                    {
                        _logger.LogError(
                            "Failed to upload document {DocumentKey}: {Error}",
                            failedResult.Key,
                            failedResult.ErrorMessage);
                    }
                }
                catch (RequestFailedException ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to upload a batch containing {BatchSize} documents.",
                        batch.Length);

                    throw;
                }
            }

            return results;
        }

        private async Task<List<DocumentUnifiedSearchDto>> GenerateVectorsAsync(List<DocumentUnifiedSearchDto> docs, CancellationToken cancellationToken = default)
        {
            const int maxRetries = 5;
            int EmbeddingBatchSize = 100;
            int MaxEmbeddingConcurrency = 1;
            ArgumentNullException.ThrowIfNull(docs);

            var vectorDocuments = new ConcurrentBag<DocumentUnifiedSearchDto>();

            foreach (DocumentUnifiedSearchDto[] batch in docs.Chunk(EmbeddingBatchSize))
            {
                _logger.LogInformation(
                    "Generating vectors for batch of {BatchSize} documents.",
                    batch.Length);

                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxEmbeddingConcurrency
                };

                await Parallel.ForEachAsync(
                    batch,
                    options,
                    async (document, token) =>
                    {
                        string? textToEmbed = document.MergedText;

                        if (string.IsNullOrWhiteSpace(textToEmbed))
                        {
                            _logger.LogWarning(
                                "Skipping vector generation for document {DocumentId}: " +
                                "merged_text is empty.",
                                document.DocumentId);

                            return;
                        }

                        try
                        {
                            for (int attempt = 1; attempt <= maxRetries; attempt++)
                            {
                                try
                                {
                                    IReadOnlyList<float> vector =
                                        await GenerateEmbeddingAsync(textToEmbed, cancellationToken);

                                    if (vector.Count != _searchOptions.VectorDimensions)
                                    {
                                        throw new InvalidOperationException(
                                            $"Embedding dimension mismatch for " +
                                            $"{document.DocumentId}. " +
                                            $"Expected {_searchOptions.VectorDimensions}, " +
                                            $"received {vector.Count}.");
                                    }

                                    document.ContentVector = vector;
                                    break;
                                }
                                catch (System.ClientModel.ClientResultException ex)
                                        when (ex.Status == 429)
                                {
                                    if (attempt == maxRetries)
                                        throw;

                                    var delay = TimeSpan.FromSeconds(4 * attempt);

                                    _logger.LogWarning(
                                        "Embedding rate limited. Attempt {Attempt}/{MaxRetries}. " +
                                        "Retrying in {Seconds} seconds.",
                                        attempt,
                                        maxRetries,
                                        delay.TotalSeconds);

                                    await Task.Delay(delay, cancellationToken);
                                }
                            }

                            vectorDocuments.Add(document);

                        }
                        catch (OperationCanceledException)
                            when (token.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(
                                ex,
                                $"Failed to generate embedding for document {document.DocumentId}.");
                        }
                    });

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            return vectorDocuments.ToList();
        }

        private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            await _embeddingSemaphore.WaitAsync(cancellationToken);

            try
            {
                OpenAIEmbedding embedding = await _embeddingClient.GenerateEmbeddingAsync(text, null, cancellationToken);

                return embedding.ToFloats().ToArray();
            }
            finally
            {
                _embeddingSemaphore.Release();
            }
        }
    }
}
