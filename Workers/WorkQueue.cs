using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Workers
{
    /// <summary>
    /// Concurrent queue implementation used by the hosted background worker.
    /// </summary>
    public class WorkQueue : IWorkQueue
    {
        private readonly ConcurrentQueue<WorkItem> _items = new ConcurrentQueue<WorkItem>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public async Task EnqueueAsync(WorkItem item)
        {
            if (item == null)
                return;

            _items.Enqueue(item);
            _signal.Release();
        }

        public async Task<WorkItem> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);

            if (_items.TryDequeue(out var item))
                return item!;

            // This should not happen because semaphore tracks items, but return a fallback
            return new WorkItem(WorkType.Unknown, null);
        }
    }
}
