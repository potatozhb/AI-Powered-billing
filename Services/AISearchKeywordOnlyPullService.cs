using AIbillingRAGBuilder.Dtos;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AIbillingRAGBuilder.Services
{
    /*  library azure-search-documents, azure-identity
    1. data is  a list of json files, store in Azure blob storage, each json file contains a list of keywords, each keyword is a string.
    2. The service will read the json files from Azure blob storage, and for each keyword, it will search in the database for matching records.\
    3. User Azure agent with the AI search service as the knowledge to finish the search, and return the results to the user.
     */

    public class AISearchKeywordOnlyPullService
    {   //pull model to build the indexer and skillset for the Azure Search service
        
        // Azure Search service configuration
        //private const string AZURE_SEARCH_SERVICE_URL = "https://crcautobillingsearchservice.search.windows.net";
        //// Azure Blob Storage configuration
       

        private readonly ILogger<OrderRagIndexFunction> _logger;
        private readonly AzureSearchOptions _searchOptions;
        private readonly FoundryOptions _foundryOptions;

        public AISearchKeywordOnlyPullService(ILogger<OrderRagIndexFunction> logger,
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
                new SearchField("tags", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFacetable = true },
                new SearchField("image_url", SearchFieldDataType.String)
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
                                                            new SemanticField("ingredients")
                                                        },

                                                        KeywordsFields =
                                                        {
                                                            new SemanticField("category"),
                                                            new SemanticField("tags")
                                                        }
                                                    });

            var index = new SearchIndex(index_name, fields)
            {
                Description = index_description,
                SemanticSearch = new SemanticSearch
                {
                    DefaultConfigurationName = semanticConfigName,
                    Configurations = { semanticConfiguration }
                }
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
                Description = indexer_description,
                Parameters = indexerParameters
            };

            // create and run the indexer
            var indexerResult = await indexerClient.CreateOrUpdateIndexerAsync(indexer);
            _logger.LogInformation($"Indexer <{indexer_name}> created or updated successfully.");
        }
    }
}
