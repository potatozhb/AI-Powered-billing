using AIbillingRAGBuilder.Models;

public static class SpecialBillingRules
{
    public static IReadOnlyList<SpecialBillingRule> All { get; } =
        new List<SpecialBillingRule>
        {
            new()
            {
                RuleId = "SR-SOFTWARE-OUTOFSCOPE-001",
                RuleName = "Software Installation and Configuration Changes",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "NEW MENU",
                    "CREATE MENU",
                    "ADD MENU",
                    "NEW INSTALLATION",
                    "INSTALL SOFTWARE",
                    "NEW LAPTOP",
                    "MOVE TERMINAL",
                    "RELOCATE TERMINAL",
                    "PRINTER ROUTING",
                    "CHANGE PRINTER ROUTING",
                    "CONFIGURATION CHANGE",
                    "RECONFIGURE",
                    "NEW SETUP",
                    "Rollback",
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The matched request is materially involved in the current service",
                    "The work involves a new installation, new setup, relocation, or configuration change",
                    "The work is not break-fix troubleshooting or correction of an existing software bug"
                },
                Action = SpecialRuleAction.BillableOverride,
                Reason =
                    "Software contracts cover break-fix issues and software bugs. New installations, new setups, terminal relocations, menu creation, printer-routing changes, and extensive configuration changes are outside normal software contract coverage."
            },

            new()
            {
                RuleId = "SR-CONSUMABLE-001",
                RuleName = "Consumable Parts",
                RuleType = SpecialRuleType.Consumable,
                TriggerPhrases = new()
                {
                    "BATTERY",
                    "FUSER",
                    "GIFT CARD",
                    "GLASS",
                    "INK",
                    "KEY",
                    "LABEL",
                    "LOCK",
                    "MAG CARD",
                    "TONER",
                    "TRAY"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The matched item is materially involved in the current service",
                    "The item was supplied, replaced, repaired, or serviced",
                    "The item is confirmed to be outside the applicable contract coverage"
                },
                Action = SpecialRuleAction.BillableIfOutOfScope,
                Reason =
                    "Consumable items may be outside normal contract coverage and may change an otherwise free service to billable."
            },

            new()
            {
                RuleId = "SR-CONSUMABLE-002",
                RuleName = "USA Meat/Deli Scale Print Head",
                RuleType = SpecialRuleType.Consumable,
                TriggerPhrases = new()
                {
                    "PRINT HEAD"
                },
                Region = "USA",
                Conditions = new()
                {
                    "The print head is for a meat or deli scale",
                    "The print head is materially involved in the current service",
                    "The print head is confirmed to be outside the applicable contract coverage"
                },
                Action = SpecialRuleAction.BillableIfOutOfScope,
                Reason =
                    "Print heads are treated as possible consumables only for USA meat/deli scale service."
            },

            new()
            {
                RuleId = "SR-GENERAL-001",
                RuleName = "Canadian Payment Device Service",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "CREDIT CARD",
                    "DEBIT",
                    "PIN PAD"
                },
                Region = "CANADA",
                Conditions = new()
                {
                    "The payment device or payment processing issue is materially involved in the current service"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Canadian payment-related service requires billing review."
            },

            new()
            {
                RuleId = "SR-GENERAL-002",
                RuleName = "Explicit Billing Indicators",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "BILL",
                    "BILLABLE",
                    "BILLED",
                    "FREIGHT"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The phrase refers to the current work order rather than historical or reference information",
                    "The billing language is materially related to the current service or charge"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Explicit billing language may indicate separately billable work and requires evaluation before overriding contract coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-003",
                RuleName = "Physical or Accidental Damage",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "BURN OUT",
                    "BURNT OUT",
                    "CRACK",
                    "CRACKED",
                    "DROP",
                    "DROPPED",
                    "FIRE",
                    "FLOOD",
                    "LIQUID",
                    "SPILL",
                    "SPILLED"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The phrase describes the current equipment condition or cause of failure"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Physical, accidental, liquid, fire, or environmental damage may be outside normal contract coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-004",
                RuleName = "Theft and Tampering",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "ROB",
                    "STOLE",
                    "STOLEN",
                    "THEFT",
                    "TAMPER",
                    "TAMPERED",
                    "TAMPERING"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The theft or tampering condition is materially related to the current service request"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Theft or tampering-related damage may not be covered by normal service contracts."
            },

            new()
            {
                RuleId = "SR-GENERAL-005",
                RuleName = "Security and Compliance Service",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "ANTI-VIRUS",
                    "VIRUS",
                    "PCI"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The security, malware, or compliance issue is materially involved in the current service"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Security, malware, and PCI-related services may fall outside standard hardware or software support coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-006",
                RuleName = "Installation and Setup",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "INSTALL",
                    "INSTALLED",
                    "INSTALLING",
                    "SET UP",
                    "SETTING UP",
                    "REINSTALL",
                    "REINSALLED",
                    "REINSTALLING"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "Installation, setup, or reinstallation is part of the actual work performed or requested",
                    "The phrase does not merely describe routine troubleshooting"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Installation and setup work may represent project or change work outside normal break/fix service."
            },

            new()
            {
                RuleId = "SR-GENERAL-007",
                RuleName = "Configuration and Programming",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "CONFIGURE",
                    "CONFIGURED",
                    "CONFIGURING",
                    "PROGRAM",
                    "PROGRAMMED",
                    "PROGRAMMING",
                    "SCRIPT",
                    "SCRIPTED",
                    "SCRIPTING"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "Configuration, programming, or scripting is actual requested or performed work",
                    "The phrase is not merely part of diagnostic troubleshooting"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Configuration, programming, and scripting may represent additional work outside normal contract service."
            },

            new()
            {
                RuleId = "SR-GENERAL-008",
                RuleName = "Move and Relocation",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "MOVE",
                    "MOVED",
                    "MOVING",
                    "RELOCATE",
                    "RELOCATION",
                    "RELOCATING",
                    "TRANSFER",
                    "TRANSFERRED",
                    "TRANSFERRING"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "Equipment, systems, or services are actually being moved, relocated, or transferred"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Relocation and transfer work may be separately billable rather than normal break/fix support."
            },

            new()
            {
                RuleId = "SR-GENERAL-009",
                RuleName = "Change Requests and Additional Work",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "ADD",
                    "ADDED",
                    "CHANGE REQUEST"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The customer requested new, additional, or changed functionality or equipment",
                    "The phrase represents additional work rather than repair of an existing covered item"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Customer-requested additions and changes may fall outside existing contract coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-010",
                RuleName = "Remove or Replace",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "REMOVAL",
                    "REMOVE",
                    "REMOVED",
                    "REPLACE",
                    "REPLACED",
                    "REPLACING",
                    "SUPPLIED",
                    "SUPPLY"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The activity represents additional, non-standard, or customer-requested work",
                    "Normal replacement of a failed contract-covered component does not activate this rule"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Removal, replacement, and supplied-item terminology is common in normal repair work and requires context before being treated as potentially billable."
            },

            new()
            {
                RuleId = "SR-GENERAL-011",
                RuleName = "Upgrade Service",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "UPGRADE",
                    "UPGRADED",
                    "UPGRADING"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The current work includes upgrading equipment, software, configuration, or functionality"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Upgrade work may represent enhancement rather than repair and may be outside normal contract coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-012",
                RuleName = "Restore and Data Operations",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "RESTORE",
                    "RESTORED",
                    "RESTORING",
                    "IMPORT",
                    "EXPORT",
                    "CORRUPT",
                    "CORRUPTION"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The restore, data operation, or corruption issue is materially involved in the current service"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Data restoration, import/export, and corruption-related work may require separate billing evaluation."
            },

            new()
            {
                RuleId = "SR-GENERAL-013",
                RuleName = "Network and Connectivity",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "IP",
                    "ISP",
                    "NETWORK",
                    "NETWORKING",
                    "PING"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The network, ISP, or IP issue is a material cause of the service request",
                    "Diagnostic use of ping or IP information alone does not activate the rule"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Network and connectivity issues may involve infrastructure outside normal supported equipment coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-014",
                RuleName = "External Power Failure",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "POWER FAILURE",
                    "POWER OUTAGE"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The external power event caused or materially contributed to the current service request"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "External power failures or outages may represent conditions outside normal contract coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-015",
                RuleName = "Training and Advisory Service",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "TRAIN",
                    "TRAINED",
                    "TRAINING",
                    "QUESTIONS"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The primary work is user training, instruction, consultation, or operational assistance"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Training and advisory services may not be included in normal break/fix service coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-016",
                RuleName = "Mobile and Peripheral Devices",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "LAPTOP",
                    "MOBILE",
                    "PDA",
                    "TABLET"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The referenced device is materially involved in the current service"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Service involving laptops, mobile devices, PDAs, or tablets may require confirmation that the device is within contract scope."
            },

            new()
            {
                RuleId = "SR-GENERAL-017",
                RuleName = "Menu and Department Changes",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "DEPARTMENT",
                    "MENU"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The current work involves creating, modifying, moving, or configuring a department or menu"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "Menu and department changes may represent customer-requested configuration work outside standard break/fix coverage."
            },

            new()
            {
                RuleId = "SR-GENERAL-018",
                RuleName = "Miscellaneous Operational Triggers",
                RuleType = SpecialRuleType.General,
                TriggerPhrases = new()
                {
                    "BUTTON",
                    "LABEL",
                    "RECEIVING",
                    "SPLIT"
                },
                Region = "ALL",
                Conditions = new()
                {
                    "The phrase is materially related to potentially out-of-scope work",
                    "The phrase alone is not sufficient to trigger a billing decision"
                },
                Action = SpecialRuleAction.ReviewRequired,
                Reason =
                    "These are weak billing indicators and require surrounding context before they can affect the billing decision."
            }
        };
}