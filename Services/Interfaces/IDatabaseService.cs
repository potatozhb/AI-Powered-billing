using AIbillingRAGBuilder.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Services.Interfaces
{
    public interface IDatabaseService
    {
        Task<bool> Initialize(CancellationToken cancellationToken = default);
        Task<List<WorkOrderDto>> GetFullOrdersAsync(DateTime startTime, DateTime endTime);
        Task<List<WorkOrderDto>> Get100FullOrdersAsync(DateTime startTime, DateTime endTime);
        Task<WorkOrderDto> GetFullOrderBySqlAsync(string orderId, CancellationToken cancellationToken = default);

        Task<List<ContractDto>> GetFullContractsAsync();
        Task<List<ContractDto>> GetFullContractsAsync(List<string> storeIds);
        Task<List<WorkOrderRemarkDto>> GetRemarksForOrderAsync(string orderId, CancellationToken cancellationToken = default);
    }
}
