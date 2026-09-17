using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Dtos
{
    public sealed class EventOptions
    {
        public const string SectionName = "EventHubs";

        public string ConnectionString { get; set; } = string.Empty;

        public string ClosedOrderEventHubName { get; set; } = "ClosedOrders";
    }

    public sealed class SqlOptions
    {
        public const string SectionName = "Sql";

        public string ConnectionString { get; set; } = string.Empty;
    }

    public sealed class AzureSearchOptions
    {
        public const string SectionName = "AzureSearch";

        public string ServiceUrl { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public int VectorDimensions { get; set; } = 1536;

        public string VectorSearchProfileName { get; set; } = "vector-profile";

        public string VectorAlgorithmConfigurationName { get; set; } = "myHnsw";

        public string VectorizerName { get; set; } = "myFoundry";

        public string BLOBStorageConnectionString { get; set;  } = string.Empty;
    }

    public sealed class FoundryOptions
    {
        public const string SectionName = "Foundry";

        public string ServiceUrl { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public string EmbeddingDeploymentName { get; set; } = string.Empty;

        public string EmbeddingModelName { get; set; } = string.Empty;
    }
}
