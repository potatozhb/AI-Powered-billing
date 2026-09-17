using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Models
{
    public enum SpecialRuleType 
    { 
        Consumable, 
        General 
    }

    public enum SpecialRuleAction 
    { 
        BillableIfOutOfScope, 
        ReviewRequired, 
        BillableOverride, 
        FreeOverride, 
        NoAction 
    }

    public sealed class SpecialBillingRule
    {
        public string RuleId { get; set; } = string.Empty; 
        public string RuleName { get; set; } = string.Empty; 
        public SpecialRuleType RuleType { get; set; }
        public List<string> TriggerPhrases { get; set; } = new();

        /// <summary>
        /// Examples: ALL, USA, CANADA 
        /// </summary>
        public string Region { get; set; } = "ALL"; 
        public List<string> Conditions { get; set; } = new(); 
        public SpecialRuleAction Action { get; set; } 
        public string Reason { get; set; } = string.Empty;

        public string MergedText
        {
            get
            {
                var triggerPhrases = TriggerPhrases.Count > 0
                    ? string.Join(", ", TriggerPhrases)
                    : "None";

                var conditions = Conditions.Count > 0
                    ? string.Join(
                        Environment.NewLine,
                        Conditions.Select((condition, index) =>
                            $"{index + 1}. {condition}"))
                    : "None";

                return $"""
                SPECIAL BILLING RULE

                Rule ID: {RuleId}
                Rule Name: {RuleName}
                Rule Type: {RuleType.ToString().ToUpperInvariant()}
                Region: {Region}

                Trigger Phrases:
                {triggerPhrases}

                Conditions:
                {conditions}

                Action:
                {Action.ToString().ToLowerInvariant()}

                Reason:
                {Reason}

                Decision Instructions:
                A trigger phrase match alone does not mean this rule applies.

                The trigger phrase must be materially relevant to the current
                work order, including the issue reported, technician remarks,
                work performed, parts supplied or replaced, cause of failure,
                customer request, or other current service activity.

                The applicable region and all rule-specific conditions must
                also be satisfied.

                If the phrase is incidental, historical, negated, or unrelated
                to the current service, do not apply this rule.

                If the rule applies, use the action:
                {Action.ToString().ToLowerInvariant()}

                If applicability cannot be determined with sufficient confidence,
                return REVIEW_REQUIRED.

                Rule Reason:
                {Reason}
                """;
            }
        }
    }


}
