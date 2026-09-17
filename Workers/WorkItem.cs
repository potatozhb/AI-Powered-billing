using System;

namespace AIbillingRAGBuilder.Workers
{
    public enum WorkPriority
    {
        Low = 0,
        High = 1
    }

    /// <summary>
    /// Simple work item representation placed on the background queue.
    /// </summary>
    public class WorkItem
    {
        public WorkItem()
        {
        }
        public WorkItem(WorkType wtype, string? payload = null, WorkPriority priority = WorkPriority.Low)
        {
            WorkType = wtype;
            Payload = payload;
            Priority = priority;
        }

        public Guid Id { get; } = Guid.NewGuid();

        public WorkType WorkType { get; set; }

        public WorkPriority Priority { get; set; }

        /// <summary>
        /// Optional payload (json, id, or descriptor) to describe the work.
        /// </summary>
        public string? Payload { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
