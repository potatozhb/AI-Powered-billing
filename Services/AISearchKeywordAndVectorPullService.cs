using AIbillingRAGBuilder.Dtos;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIbillingRAGBuilder.Services
{
    public class AISearchKeywordAndVectorPullService
    {   //pull model to build the indexer and skillset for the Azure Search service

        // Azure Search service configuration
        //private const string AZURE_SEARCH_SERVICE_URL = "https://crcautobillingsearchservice.search.windows.net";
        //// Azure Blob Storage configuration
        ////Azure AI Foundry configuration
        //private const string AI_FOUNDRY_AI_SERVICES_URL = "https://crc-autobilling-aisearch.cognitiveservices.azure.com/";
        
        //// Azure AI Foundry embedding model configuration
        //private const string FOUNDRY_EMBEDDING_DEPLOYMENT_NAME = "text-embedding-3-small-code-vector";
        //private const string FOUNDRY_EMBEDDING_MODEL_NAME = "text-embedding-3-small";

        //private const int VECTOR_DIMENSIONS = 1536; // Dimension of the embedding vector
        //private const string VECTOR_SEARCH_PROFILE_NAME = "vector-profile"; // Name of the vector search profile
        //private const string VECTOR_SEARCH_ALGORITHM_CONFIG_NAME = "myHnsw";
        //private const string VECTOR_SEARCH_VECTORIZER_NAME = "myFoundry"; // option includes: OpenAI, Foundry, Custom, None

        private readonly ILogger<OrderRagIndexFunction> _logger;
        private readonly AzureSearchOptions _searchOptions;
        private readonly FoundryOptions _foundryOptions;

        public AISearchKeywordAndVectorPullService(ILogger<OrderRagIndexFunction> logger,
            IOptions<AzureSearchOptions> searchOptions,
            IOptions<FoundryOptions> foundryOptions)
        {
            _logger = logger;
            _searchOptions = searchOptions.Value;
            _foundryOptions = foundryOptions.Value;
        }

        public async Task BuildAISearchService(string serviceName)
        {
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
                new SearchField("product_id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true },
                new SearchField("name", SearchFieldDataType.String) { IsSearchable = true, IsFilterable = true, IsSortable = true, AnalyzerName=LexicalAnalyzerName.StandardLucene },
                new SearchField("description", SearchFieldDataType.String) { IsSearchable = true , AnalyzerName=LexicalAnalyzerName.EnLucene },
                new SearchField("category", SearchFieldDataType.String) { IsSearchable = true , IsFilterable = true, IsSortable = true, IsFacetable = true, AnalyzerName=LexicalAnalyzerName.EnLucene },
                new SearchField("price", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true, IsFacetable = true },
                new SearchField("availability", SearchFieldDataType.String) { IsSearchable = true ,IsFilterable = true, IsSortable = true, IsFacetable = true,  AnalyzerName=LexicalAnalyzerName.StandardLucene },
                new SearchField("ingredients", SearchFieldDataType.String) { IsSearchable = true ,AnalyzerName=LexicalAnalyzerName.EnLucene },
                new SearchField("rating", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true},
                new SearchField("release_date", SearchFieldDataType.DateTimeOffset) {  IsFilterable = true, IsSortable = true },
                new SearchField("tags", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsSearchable = true, IsFacetable = true },
                new SearchField("image_url", SearchFieldDataType.String),

                // Add a vector field for semantic search
                new SearchField("merged_text", SearchFieldDataType.String) { IsSearchable = true, AnalyzerName=LexicalAnalyzerName.StandardLucene },
                new SearchField("content_vector", SearchFieldDataType.Collection(SearchFieldDataType.Single)) 
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = _searchOptions.VectorDimensions, // Dimension of the embedding vector
                        VectorSearchProfileName = _searchOptions.VectorSearchProfileName + "-" + serviceName, // Name of the vector search profile
                    }
            };

            //Enable semantic, optional
            var semanticConfiguration = new SemanticConfiguration(
                                                    semanticConfigName,
                                                    new SemanticPrioritizedFields
                                                    {
                                                        TitleField = new SemanticField("name"),

                                                        ContentFields =
                                                        {
                                                            new SemanticField("description"),
                                                            new SemanticField("ingredients"),
                                                            new SemanticField("tags")
                                                        },

                                                        KeywordsFields =
                                                        {
                                                            new SemanticField("category")
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
                            ModelName = _foundryOptions.EmbeddingModelName
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

            // create data source
            var indexerClient = new SearchIndexerClient(new Uri(_searchOptions.ServiceUrl), credential);
            var container = new SearchIndexerDataContainer("food-product-catlog");
            var dataSourceConnection = new SearchIndexerDataSourceConnection(data_source_name,
                SearchIndexerDataSourceType.AzureBlob, _searchOptions.BLOBStorageConnectionString, container)
            {
                Description = $"Data source for service <{serviceName}> searching keywords in the database"
            };
            var dataSource = await indexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSourceConnection);
            _logger.LogInformation($"Data source <{data_source_name}> created or updated successfully.");


            // create skill sets
            // skill 1 : merge skill
            var inputs = new List<InputFieldMappingEntry>
            {
                new InputFieldMappingEntry("text") { Source = "/document/description" },
                new InputFieldMappingEntry("itemsToInsert") { Source = "/document/tags" }
            };
            var outputs = new List<OutputFieldMappingEntry>
            {
                new OutputFieldMappingEntry("mergedText") { TargetName = "merged_text" }
            };

            var mergeSkill = new MergeSkill(inputs,outputs)
            {
                Name = "mergeText",
                Description = "Merge description and tags into merged_text field",
                Context = "/document"
            };

            // skill 2 : embedding skill
            var embeddingInputs = new List<InputFieldMappingEntry>
            {
                new InputFieldMappingEntry("text") { Source = "/document/merged_text" }
            };
            var embeddingOutputs = new List<OutputFieldMappingEntry>
            {
                new OutputFieldMappingEntry("embedding") { TargetName = "content_vector" }
            };

            var embeddingSkill = new AzureOpenAIEmbeddingSkill(embeddingInputs, embeddingOutputs)
            {
                Description = "Generate embedding vector for merged_text field",
                Context = "/document",
                ResourceUri = new Uri(_foundryOptions.ServiceUrl),
                DeploymentName = _foundryOptions.EmbeddingDeploymentName,
                ModelName = _foundryOptions.EmbeddingModelName,
                Dimensions = _searchOptions.VectorDimensions
            };

            var skills = new List<SearchIndexerSkill> { mergeSkill, embeddingSkill };
            var skillset = new SearchIndexerSkillset($"{serviceName}-skillset", skills)
            {
                Description = $"Skillset for service <{serviceName}> to generate embedding vectors",
                CognitiveServicesAccount = new CognitiveServicesAccountKey(_foundryOptions.ApiKey)
            };

            await indexerClient.CreateOrUpdateSkillsetAsync(skillset);
            _logger.LogInformation($"Skillset <{serviceName}-skillset> created or updated successfully.");

            // create search indexer
            var indexerParameters = new IndexingParameters
            {
                IndexingParametersConfiguration = new IndexingParametersConfiguration
                {
                    ParsingMode = BlobIndexerParsingMode.Json,
                    DataToExtract = BlobIndexerDataToExtract.ContentAndMetadata,
                }
            };

            var indexer = new SearchIndexer(indexer_name, data_source_name, index.Name)
            {
                SkillsetName = skillset.Name,
                Description = indexer_description,
                Parameters = indexerParameters
            };

            //                                               skill output name                   search index field name
            indexer.OutputFieldMappings.Add(new FieldMapping("/document/merged_text") { TargetFieldName = "merged_text" });
            indexer.OutputFieldMappings.Add(new FieldMapping("/document/content_vector") { TargetFieldName = "content_vector" });

            // create and run the indexer
            var indexerResult = await indexerClient.CreateOrUpdateIndexerAsync(indexer);
            _logger.LogInformation($"Indexer <{indexer_name}> created or updated successfully.");
        }
    }
}
