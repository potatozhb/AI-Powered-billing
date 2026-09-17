
using System.Text.Json.Serialization;

namespace AIbillingRAGBuilder.Dtos
{
    public class DocumentUnifiedSearchDto
    {
        // Shared key for all document types
        [JsonPropertyName("document_id")]
        public string DocumentId { get; set; } = string.Empty;
        [JsonPropertyName("document_name")]
        public string DocumentName { get; set; } = string.Empty;

        [JsonPropertyName("document_type")]
        public string DocumentType { get; set; } = string.Empty;

        /// <summary>
        /// Start from 1
        /// </summary>
        [JsonPropertyName("chunk_number")]
        public string ChunkNumber { get; set;  } = "1";

        // Shared metadata
        [JsonPropertyName("store_id")]
        public string StoreId { get; set; } = string.Empty;
        [JsonPropertyName("store_name")]
        public string? StoreName { get; set; } = string.Empty;

        // Searchable text
        [JsonPropertyName("merged_text")]
        public string MergedText { get; set; } = string.Empty;
        // Embedding vector
        [JsonPropertyName("content_vector")]
        public IReadOnlyList<float>? ContentVector { get; set; }


        // Work-order-specific fields
        [JsonPropertyName("work_order_id")]
        public string? WorkOrderId { get; set; } = string.Empty;
        [JsonPropertyName("billing_status")]
        public string? BillingStatus { get; set; } = string.Empty;
        [JsonPropertyName("initial_severity")]
        public string? InitialSeverity { get; set; } = string.Empty;
        [JsonPropertyName("current_severity")]
        public string? CurrentSeverity { get; set; } = string.Empty;
        [JsonPropertyName("country")]
        public string? CountryString { get; set; } = null;
        [JsonPropertyName("project_id")]
        public string? ProjectId { get; set; }

        // Contract-specific fields
        [JsonPropertyName("contract_id")]
        public string? ContractId { get; set; } = string.Empty;

        [JsonPropertyName("contract_status")]
        public string? ContractStatus { get; set; } = string.Empty;

        [JsonPropertyName("contract_item_ids")]
        public List<string>? ContractItemIds { get; set; } = new List<string>();

        [JsonPropertyName("product_names")]
        public List<string>? ProductNames { get; set; } = new List<string>();


        // ============================================================
        // Special-rule-specific fields
        // ============================================================

        /// <summary>
        /// CONSUMABLE
        /// GENERAL
        /// </summary>
        [JsonPropertyName("rule_type")]
        public string? RuleType { get; set; }

        /// <summary>
        /// ALL
        /// CANADA
        /// USA
        /// </summary>
        [JsonPropertyName("rule_region")]
        public string? RuleRegion { get; set; }

        /// <summary>
        /// REVIEW_REQUIRED
        /// BILLABLE_IF_OUT_OF_SCOPE
        /// BILLABLE_OVERRIDE
        /// FREE_OVERRIDE
        /// </summary>
        [JsonPropertyName("rule_action")]
        public string? RuleAction { get; set; }

        /// <summary>
        /// Human/LLM-readable explanation of why this rule exists.
        /// </summary>
        [JsonPropertyName("rule_reason")]
        public string? RuleReason { get; set; }
    }
}
