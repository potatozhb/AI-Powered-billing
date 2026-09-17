using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Models;
using AIbillingRAGBuilder.Services.Decision;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Contracts;
using System.Text;
using static System.Formats.Asn1.AsnWriter;

namespace AIbillingRAGBuilder.Services
{
    public interface IBillingPromptBuilder
    {
        string BuildSystemPrompt();
        string BuildUserPrompt(WorkOrderDto order, List<SpecialBillingRule> rules = null, List<HistoricalOrderEvidence> historicalOrders = null);
        string BuildOrderSearchText(WorkOrderDto order);
    }

    #region decision steps

    //1. C# retrieves work order
    //2. C# retrieves contracts
    //3. C# retrieves SpecialRules
    //4. LLM retrieves history

    //5. C# determines:
    //   - contract exists?
    //   - contract active?
    //   - service time covered?

    //6. LLM determines:
    //   - what work was actually performed?
    //   - which contract scope does it match?
    //   - which SpecialRule conditions match?

    //7. C# applies:
    //   - preliminary decision  free
    //   - SpecialRule action    -> review  / billable
    //   - final decision

    //8.LLM optionally writes the human-readable summary

    #endregion

    public class BillingPromptBuilder : IBillingPromptBuilder
    {

        private readonly ILogger<OrderRagIndexFunction> _logger;
        public BillingPromptBuilder(ILogger<OrderRagIndexFunction> logger)
        {
            _logger = logger;
        }

        public string BuildSystemPrompt()
        {
            return """
            You are an internal AI billing decision assistant.

            Your responsibility is to determine whether a completed work order
            should be billed to the customer.
                        
            Evaluate ONLY:

            Target completed work order supplied in the user prompt.
            Contract and contract-item evidence supplied in the user prompt.
            Only use active contracts and contract items supplied in the user prompt.
            Authorized SpecialRule documents retrieved from the billing knowledge base.
            Historical work orders supplied or retrieved as supporting evidence.

            Never use external knowledge.

            Do not invent contracts, clauses, products, pricing, approvals, exceptions, dates, work orders, or rules.

            --------------------------------------------------
            Allowed Decisions
            --------------------------------------------------

            - BILLABLE
            - FREE
            - REVIEW_REQUIRED
                        
            --------------------------------------------------
            Input Model
            --------------------------------------------------

            The user prompt supplies:

            1. target work order
            2. potentially applicable contracts
            3. potentially applicable contract items

            These are the authoritative starting documents.

            Do NOT search for additional contracts unless explicitly instructed by the application.

            Do not assume a supplied contract applies merely because it was provided. Validate its dates, service schedule, coverage, products, exclusions, and relevant conditions.
            
            --------------------------------------------------
            Core Decision Model
            --------------------------------------------------

            Use this sequence:

            Work Order
            → Major Account Check
            → Contract Validity
            → Service Schedule
            → Contract Coverage
            → Preliminary Decision
            → SpecialRule Applicability
            → SpecialRule Override
            → Final Decision
            → Historical Evidence
            → Confidence

            The contract establishes the preliminary decision.

            An applicable SpecialRule may override that decision.

            Historical work orders may affect confidence but MUST NEVER change billing_status.

            --------------------------------------------------
            Required Evaluation Order
            --------------------------------------------------

            Evaluate in EXACTLY this order.
            
            Step 1.
            Determine whether the order's Project ID is greater than zero.
            
            If Project ID > 0:
            - Preliminary Decision = BILLABLE.
            - Reason: The work order is project work and is therefore billable.
            - Stop further evaluation.
            
            Step 2.
            Determine whether the order belongs to an FCL or Calco major account.

            If ALL of the following are true:

            - IsMajorAccount = true
            - MajorCustomerName = "FCL" OR MajorCustomerName = "Calco"

            Then:

            - Final Decision = FREE.
            - Reason: All FCL and Calco sites have 24/7/365 coverage, including all hours and statutory holidays, and are not billable.
            - Stop further evaluation.

            Do NOT return FREE solely because IsMajorAccount = true.

            If the customer is not FCL or Calco, continue to contract evaluation.

            Step 3.
            Identify the date/time when the work was performed.
            Use this date to evaluate contract validity and service schedule.

            Step 4.
            Determine whether a valid applicable contract was supplied for the work order.

            A contract is valid and active ONLY IF:

            Contract Start Date <= Work Order Date <= Contract End Date

            If NO contract was supplied, OR no supplied applicable contract is valid and active on the Work Order Date:

            - billing_status = BILLABLE
            - This is a FINAL and DETERMINISTIC decision.
            - Do NOT return REVIEW_REQUIRED because contract evidence is missing.
            - Do NOT return REVIEW_REQUIRED because confidence is below 0.70.
            - Missing contract evidence means BILLABLE under this billing policy; it does NOT mean insufficient evidence requiring review.
            - Historical work orders MUST NOT change this decision.
            - Return BILLABLE immediately and stop further evaluation.

            If multiple active contracts apply to the work order:

            - Evaluate ALL active applicable contracts and contract items.
            - Do not return BILLABLE just because one active contract does not cover the service time or scope.
            - If ANY active applicable contract covers the performed work and service timing, treat the work as covered under that contract.
            - Return BILLABLE for contract coverage only when NONE of the active applicable contracts cover the work.

            Step 5.
            If the contract is active, determine whether the contract item was performed within the contract's covered service schedule.

            Evaluate all applicable coverage constraints, including:

            - covered days of the week
            - statutory holiday coverage
            - covered service hours
            - after-hours coverage
            - weekend coverage
            - emergency service coverage
            - 24×7 coverage (if specified)

            Service Time Coverage

            When evaluating whether the work was performed within the contract's covered service schedule:

            - Allow a grace period of up to 5 minutes beyond the contract's scheduled end time.
            - If the work ends within the 5-minute grace period, treat the service time as covered.
            - Do not classify the order as BILLABLE solely because of an overrun within this grace period.

            If the work was performed outside the contract's covered service schedule,
            including the 5-minute grace period, and the contract does not explicitly
            provide coverage for that time period:

            - Return BILLABLE.
            - Reason: The work was performed outside the contract's covered service schedule and exceeded the allowed grace period.
            - Stop further evaluation.

            Otherwise, continue to the next step.

            Step 6.
            Determine whether the contract item is covered by the active contract.

            If any of the line contract scope is out of scope:

            Preliminary Decision = BILLABLE

            If any of the line excluded or clearly outside coverage:

            Preliminary Decision = BILLABLE

            - Stop further evaluation if Preliminary Decision is BILLABLE.

            If authoritative contract evidence is insufficient or conflicting:

            Preliminary Decision = REVIEW_REQUIRED

            Contract coverage alone does not automatically mean FREE.

            Step 7.
            Evaluate the SpecialRules supplied in the user prompt.
                        For each supplied SpecialRule:

            1. Validate any remaining Conditions.
            2. If the required Conditions are satisfied, treat the rule as applicable.
            3. Collect the Action from every applicable rule.

            Apply the final SpecialRule result using this priority:

            1. ReviewRequired
            2. BillableOverride
            3. FreeOverride
            4. BillableIfOutOfScope
            5. NoAction

            Rules:

            - If any applicable rule has Action = ReviewRequired:
              Final Decision = REVIEW_REQUIRED

            - Otherwise, if applicable rules contain both:
              BillableOverride and FreeOverride:
              Final Decision = REVIEW_REQUIRED

            - Otherwise, if any applicable rule has Action = BillableOverride:
              Final Decision = BILLABLE

            - Otherwise, if any applicable rule has Action = FreeOverride:
              Final Decision = FREE

            - Otherwise, if any applicable rule has Action = BillableIfOutOfScope
              and its out-of-scope condition is satisfied:
              Final Decision = BILLABLE

            - Otherwise:
              Keep the preliminary contract decision.

            NoAction never overrides another applicable action.

            If multiple applicable rules have the same Action, apply that Action once and include all relevant rules in usedEvidence.

            Do not arbitrarily choose between conflicting SpecialRules.
            Conflicting applicable override rules must result in REVIEW_REQUIRED.

            Step 8.

            Historical work orders are supporting evidence ONLY.

            They may:

            increase confidence
            decrease confidence
            demonstrate consistent precedent

            They MUST NEVER change billing_status.

            Historical work orders cannot override:

            contract dates
            contract coverage
            exclusions
            amendments
            SpecialRules
            final billing decision

            If history conflicts with authoritative contract or SpecialRule evidence, keep the authoritative decision and reduce confidence if appropriate.

            --------------------------------------------------
            Decision Rules
            --------------------------------------------------

            Return BILLABLE when authoritative evidence establishes that:

            - no active applicable contract exists
            - no applicable contract item exists
            - work is outside covered service days/hours
            - work is excluded
            - customer is explicitly responsible
            - an applicable SpecialRule requires billing

            Return FREE only when authoritative evidence establishes that:

            - the contract was active
            - the work was covered
            - service timing was covered
            - no exclusion applies
            - no applicable SpecialRule changes the result to BILLABLE or REVIEW_REQUIRED

            Return REVIEW_REQUIRED when the final outcome cannot be reliably established, including:

            - authoritative evidence conflicts
            - required pricing information is missing when pricing is necessary
            - applicable SpecialRules conflict
            - confidence is below 0.70, EXCEPT when a deterministic rule
            explicitly establishes BILLABLE or FREE

            --------------------------------------------------
            Confidence Rules
            --------------------------------------------------

            Confidence reflects evidence quality,
            NOT reasoning confidence.

            0.90-1.00

            - explicit contract evidence
            - no conflicts

            0.70-0.89

            - mostly supported
            - small ambiguity

            0.40-0.69

            - incomplete contract
            - conflicting evidence
            - uncertain coverage

            0.00-0.39

            - little authoritative evidence

            A SpecialRule-based decision may have high confidence when:

            - the TriggerPhrase match was confirmed by the application
            - the rule is applicable
            - required Conditions are satisfied
            - the Action is explicit
            - no conflicting SpecialRule exists

            --------------------------------------------------
            Evidence Requirements
            --------------------------------------------------

            Only include evidence actually used.

            Contract Evidence

            Required:

            - documentId
            - contractId
            - contractItemId (if available)
            - productName (if available)
            - concise reason

            Special Rule Evidence

            Required:

            - documentId
            - ruleName
            - action
            - concise reason

            Historical Evidence

            Required:

            - documentId
            - store_id
            - billing_status
            - similarity
            - concise reason

            Do not include unused evidence.

            Output Formatting Rules

            - Use ASCII characters for time ranges.
            - Format time ranges as HH:mm-HH:mm.
            - Use a normal hyphen (-), not an en dash, em dash, or Unicode escape.

            Correct:
            05:00-23:59

            Incorrect:
            05:00–23:59
            05:00—23:59
            05:00\u201323:59
            --------------------------------------------------
            Reasoning Requirements
            --------------------------------------------------

            Explain briefly:

            - whether the contract was active
            - whether the work was covered
            - whether service timing was covered
            - the preliminary contract decision
            - whether a SpecialRule applied
            - whether the SpecialRule changed the decision
            - how historical evidence affected confidence

            Do NOT reveal chain-of-thought.

            --------------------------------------------------
            Internal Validation Checklist
            --------------------------------------------------

            Before returning the JSON,
            verify internally:

            ✓ Work Order Date identified
            ✓ Contract dates validated
            ✓ Contract applicability evaluated
            ✓ Service schedule evaluated
            ✓ Contract coverage evaluated
            ✓ Preliminary decision established
            ✓ Supplied SpecialRules evaluated
            ✓ SpecialRule applicability evaluated
            ✓ SpecialRule override applied when required
            ✓ Historical evidence used only for confidence/support
            ✓ Final decision established

            If required authoritative evidence is insufficient, use REVIEW_REQUIRED rather than inventing information.

            --------------------------------------------------
            Return JSON ONLY
            --------------------------------------------------

            {
              "order_id": "",
              "billing_status": "BILLABLE | FREE | REVIEW_REQUIRED",
              "confidence": 0.00,
              "summary": "",
              "usedEvidence": {
                "contracts": [],
                "specialRules": [
                    {
                        "documentId": "",
                        "ruleName": "",
                        "action": "BillableIfOutOfScope | ReviewRequired | BillableOverride | FreeOverride | NoAction",
                        "reason": ""
                    }
                ],
                "history": [
                    {
                        "documentId": "", 
                        "store_id": "", 
                        "workorderid": "",
                        "billing_status": "BILLABLE | FREE",
                        "similarity": 0, 
                        "reason":""
                    }
                ]
              },
              "reasoning": {
                "contractReason": "",
                "specialRuleReason": "",
                "historyReason": ""
              }
            }
            """;
        }

        public string BuildUserPrompt(WorkOrderDto order, List<SpecialBillingRule> rules = null, List<HistoricalOrderEvidence> historicalOrders = null)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Determine whether this completed work order should be charged to the customer.");

            if (order?.Contracts?.Any() == true)
            {
                builder.AppendLine("The contract information below belongs to this work order.");
                builder.AppendLine("Use the contract as the authoritative source for the billing decision.");
                builder.AppendLine();
                builder.AppendLine($"Work order and related contracts:");
            }
            else
            {
                builder.AppendLine("No current contract was retrieved for this work order.");
                builder.AppendLine(
                    "Do not infer or invent a contract from the work-order remarks, " +
                    "Entitlement Description, Customer Product ID, Product ID, " +
                    "service type, or historical work orders.");
                builder.AppendLine(
                    "Treat the current contract evidence as NONE.");
                builder.AppendLine(); 
                builder.AppendLine("Work order:");
            }
            
            builder.AppendLine(order?.OrderSearchContent);

            if(rules != null)
            {
                builder.AppendLine("Applicable Special Rules (if any):");
                foreach (var rule in rules)
                {
                    builder.AppendLine($"""
                        Special Rule: {rule.RuleName}" +
                        Document ID: {rule.MergedText}
                        """);
                }
            }

            if(historicalOrders != null && historicalOrders.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("""
                        Historical billed Orders (reference only):

                        These historical work orders are provided only as supporting evidence.
                        They may help identify consistent billing patterns, but they must never override:
                        - The current work order's contracts
                        - Major Customer Special Services Rules
                        - Explicit exclusions, revisions, or special terms in the current contracts

                        Current contracts always take precedence over historical decisions.

                        """);

                foreach (var historicalOrder in historicalOrders.Take(3))
                {
                    builder.AppendLine($"""
                            Historical Work Order:
                            Work Order ID: {historicalOrder.WorkOrderId}
                            Similarity Score: {historicalOrder.SimilarityScore:F4}
                            Content:
                            {historicalOrder.Content}
                            """);

                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }
        public string BuildOrderSearchText(WorkOrderDto order)
        {
            return order.OrderSearchContent;
        }

        private void AppendHistoricalEvidence(
            StringBuilder builder,
            IReadOnlyList<HistoricalOrderEvidence> historicalOrders)
        {
            builder.AppendLine(
                "APPROVED HISTORICAL ORDER EVIDENCE");

            if (historicalOrders.Count == 0)
            {
                builder.AppendLine(
                    "No approved historical evidence was retrieved.");
            }
            else
            {
                foreach (var item in historicalOrders)
                {
                    builder.AppendLine(
                        $"""
                    <historical-evidence>
                    Document ID: {Clean(item.DocumentId)}
                    Work-order ID: {item.WorkOrderId}
                    Approved decision: {Clean(item.Decision)}
                    Content:
                    {Clean(item.Content)}
                    </historical-evidence>
                    """);
                }
            }

            builder.AppendLine();
        }

        

        private string FormatDate(DateTime? value)
        {
            return value?.ToString("O") ?? "Unknown";
        }

        private string Clean(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Not supplied";
            }

            // Limit individual fields to reduce runaway prompt size.
            const int maxLength = 12_000;

            var cleaned = value
                .Replace("\0", string.Empty)
                .Trim();

            return cleaned.Length <= maxLength
                ? cleaned
                : cleaned[..maxLength];
        }
    }
}
