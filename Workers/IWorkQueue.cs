using System.Threading;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Workers
{
    /// <summary>
    /// Minimal producer/consumer queue abstraction for background work.
    /// </summary>
    public interface IAzureWorkQueue
    {
        /// <summary>
        /// Enqueue a work item for background processing.
        /// </summary>
        Task EnqueueAsync(WorkItem item);

    }


    /// <summary>
    /// Minimal producer/consumer queue abstraction for background work.
    /// </summary>
    public interface IWorkQueue
    {
        /// <summary>
        /// Enqueue a work item for background processing.
        /// </summary>
        Task EnqueueAsync(WorkItem item);

        /// <summary>
        /// Dequeue the next work item, or wait until one is available.
        /// </summary>
        Task<WorkItem> DequeueAsync(CancellationToken cancellationToken);
    }
}
