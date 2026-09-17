using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Dtos
{
    public class ClosedOrderEvent
    {
        public string EventType { get; set; } = "WorkOrderClosed";
        public string Code { get; set; }
        public string TableName { get; set; }
        public string WorkOrderId { get; set; }
        public string Status { get; set; }
        public int UserId { get; set; }
        public string ClientDescriptor { get; set; }
        public string RemoteIpAddress { get; set; }
        public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
