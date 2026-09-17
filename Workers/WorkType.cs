namespace AIbillingRAGBuilder.Workers
{
    /// <summary>
    /// Represents types of background work the worker can perform.
    /// Add new values as new work scenarios are required.
    /// </summary>
    public enum WorkType
    {
        Unknown = 0,
        LoadData,
        DeleteIndex,
        BuildSearchEngine,
        AddHistoryData,
        AddData,
        BillingDecision,
    }
}
