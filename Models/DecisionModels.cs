
using System.Text.Json.Serialization;

namespace AIbillingRAGBuilder.Services.Decision
{
    public sealed class AIDecisionOptions
    {
        public string ChatDeploymentName { get; set; } = string.Empty;

        public string SemanticConfigurationName { get; set; } =
            string.Empty;

        public string VectorFieldName { get; set; } = "content_vector";

        public int ContractResultCount { get; set; } = 3;

        public int HistoricalResultCount { get; set; } = 5;
    }
    public sealed class BillingDecisionResponse
    {
        [JsonPropertyName("decision_id")]
        public string DecisionId { get; set; }

        [JsonPropertyName("order_id")]
        public string OrderId { get; set; }

        [JsonPropertyName("billing_status")]
        public BillingDecision Billing_Status { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("createAt")]
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("usedEvidence")]
        public UsedEvidence UsedEvidence { get; set; } = new();

        [JsonPropertyName("reasoning")]
        public BillingReasoning Reasoning { get; set; } = new();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BillingDecision
    {
        [JsonStringEnumMemberName("BILLABLE")]
        Billable,

        [JsonStringEnumMemberName("FREE")]
        Free,

        [JsonStringEnumMemberName("REVIEW_REQUIRED")]
        ReviewRequired
    }

    public sealed class UsedEvidence
    {
        [JsonPropertyName("contracts")]
        public List<ContractEvidence> Contracts { get; set; } = [];

        [JsonPropertyName("specialRules")]
        public List<SpecialRuleEvidence> SpecialRules { get; set; } = [];

        [JsonPropertyName("history")]
        public List<HistoryEvidence> History { get; set; } = [];
    }

    public sealed class ContractEvidence
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("documentId")]
        public string DocumentId { get; set; } = "0";

        [JsonPropertyName("decision_id")]
        public string DecisionId { get; set; }

        [JsonPropertyName("contractId")]
        public string ContractId { get; set; } = string.Empty;

        [JsonPropertyName("contractItemId")]
        public string? ContractItemId { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class SpecialRuleEvidence
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("documentId")]
        public string DocumentId { get; set; } = "0";

        [JsonPropertyName("decision_id")]
        public string DecisionId { get; set; }

        [JsonPropertyName("ruleName")]
        public string? RuleName { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class HistoryEvidence
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("documentId")]
        public string DocumentId { get; set; } = "0";

        [JsonPropertyName("decision_id")]
        public string DecisionId { get; set; }

        [JsonPropertyName("workOrderId")]
        public string WorkOrderId { get; set; } = string.Empty;

        [JsonPropertyName("billing_status")]
        public BillingDecision Billing_Status { get; set; }

        [JsonPropertyName("similarity")]
        public double? Similarity { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class BillingReasoning
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("decision_id")]
        public string DecisionId { get; set; }

        [JsonPropertyName("contractReason")]
        public string ContractReason { get; set; } = string.Empty;

        [JsonPropertyName("specialRuleReason")]
        public string SpecialRuleReason { get; set; } = string.Empty;

        [JsonPropertyName("historyReason")]
        public string HistoryReason { get; set; } = string.Empty;
    }
}
