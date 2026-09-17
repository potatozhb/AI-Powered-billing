using AIbillingRAGBuilder.Dtos;
using AIbillingRAGBuilder.Services.Interfaces;
using Grpc.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using System.Data;
using System.Diagnostics.Contracts;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;

namespace AIbillingRAGBuilder.Services
{
    public class DatabaseService : IDatabaseService, IDisposable
    {
        //private const string connectionString = "";
        private readonly ILogger<DatabaseService> _logger;
        private Dictionary<int, string> _users;
        private Dictionary<int, string> _majorAccounts;
        private Dictionary<int, string> _technicianTypes;
        private Dictionary<int, string> _customers;
        private Dictionary<int, string> _stores;
        private List<ContractDto> _contracts;
        private SemaphoreSlim initializeSemaphore = new SemaphoreSlim(1, 1);
        private SemaphoreSlim orderRemaphore = new SemaphoreSlim(1, 1);
        private SemaphoreSlim contractRemaphore = new SemaphoreSlim(1, 1);

        private readonly SqlOptions _options;

        //private string StartTime = "2020-01-01";
        //private string EndTime = "2020-02-01";

        public DatabaseService(ILogger<DatabaseService> logger, IOptions<SqlOptions> options)
        {
            _options = options.Value;
            _logger =logger;

            _logger.LogInformation(_options.ConnectionString);

            _ = Initialize();
        }

        public async Task<bool> Initialize(CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("************ Start Initialize Database... **********************");
            _logger.LogInformation(_options.ConnectionString);

            var lockAcquired = false;
            try
            {
                await initializeSemaphore.WaitAsync(cancellationToken); 
                lockAcquired = true;

                _users = new Dictionary<int, string>();
                _technicianTypes = new Dictionary<int, string>();
                _majorAccounts = new Dictionary<int, string>();
                _customers = new Dictionary<int, string>();
                _stores = new Dictionary<int, string>();


                await GetAllMajorAccountsAsync(cancellationToken);
                Task getAllTechnicians = GetAllTechniciansAsync(cancellationToken);
                Task getAllUsers = GetAllUsersAsync(cancellationToken);

                Task getCustomers = GetAllCustomersAsync(cancellationToken);
                Task getStores = GetAllStoresAsync(cancellationToken);

                await Task.WhenAll(getAllTechnicians, getAllUsers, getCustomers, getStores);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when initializing.");
                return false;
            }
            finally
            {
                if (lockAcquired)
                {
                    initializeSemaphore.Release();
                }
            }

        }

        public async Task<WorkOrderDto?> GetFullOrderBySqlAsync(string orderId, CancellationToken cancellationToken = default)
        {
            if (int.TryParse(orderId, out var parsedOrderId))
            {
                #region raw sql to get the data
                try
                {
                    await using var connection = new SqlConnection(_options.ConnectionString);
                    await connection.OpenAsync();
                    var sql = "select * from eWorkOrder where workOrderId = @orderId";
                    await using var command = new SqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@orderId", parsedOrderId);

                    var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        try
                        {
                            var order = await ReadOrderDataAsync(reader);

                            #region Get the lines and remarks for this order
                            await GetAllTechniciansAsync(cancellationToken);
                            var lines = await GetLineForOrderAsync(orderId, cancellationToken);
                            var remarks = await GetRemarksForOrderAsync(orderId, cancellationToken);
                            foreach (var line in lines)
                            {
                                line.Technician = _users.ContainsKey(line.TechnicianId) ? _users[line.TechnicianId] : string.Empty;
                                line.TechnicianType = _technicianTypes.ContainsKey(line.TechnicianId) ?
                                    (Enum.TryParse<UserType>(_technicianTypes[line.TechnicianId], out var cur) ? cur : UserType.Software) : UserType.Software;
                            }
                            #endregion

                            order.WorkOrderLines = lines;
                            order.Remarks = remarks;

                            order.Contracts = await GetContractByStoreAsync(order.StoreId.ToString(), cancellationToken);
                            return order;
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error when reading from the database.");
                    return null;
                }
                #endregion

                return null;
            }
            else
            {
                _logger.LogWarning($"Invalid orderId format: {orderId}");
                return null;
            }
        }

        public async Task<List<WorkOrderDto>> Get100FullOrdersAsync(DateTime startTime, DateTime endTime)
        {
            var orders = await GetFullOrdersAsync(startTime, endTime);
            return orders.Where(x =>  x.CreateDateTime.HasValue).ToList().Take(1).ToList();
        }

        /// <summary>
        /// Add contract and inline informations
        /// </summary>
        /// <returns></returns>
        public async Task<List<WorkOrderDto>> GetFullOrdersAsync(DateTime startTime, DateTime endTime)
        {
            var contracts = await GetFullContractsAsync();
            var lockAcquired = false;
            try
            {
                await orderRemaphore.WaitAsync();
                lockAcquired = true;

                var contractMapToStore = contracts.GroupBy(
                    x => x.StoreId).ToDictionary(
                    k => k.Key, v => v.ToList());

                var workOrderRemarks = await GetAllRemarksAsync(startTime, endTime);
                foreach (var remark in workOrderRemarks)
                {
                    remark.Technician = _users.ContainsKey(remark.TechnicianId) ? _users[remark.TechnicianId] : string.Empty;
                }

                var remarksByOrderId = workOrderRemarks
                            .GroupBy(x => x.WorkOrderID)
                            .ToDictionary(
                                g => g.Key,
                                g => g.ToList());

                var workLines = await GetAllLinesAsync(startTime, endTime);
                foreach (var line in workLines)
                {
                    line.Technician = _users.ContainsKey(line.TechnicianId) ? _users[line.TechnicianId] : string.Empty;
                    line.TechnicianType = _technicianTypes.ContainsKey(line.TechnicianId) ?
                        (Enum.TryParse<UserType>(_technicianTypes[line.TechnicianId], out var cur) ? cur : UserType.Software) : UserType.Software;
                }

                var lineDic = workLines
                            .GroupBy(x => x.WorkOrderId)
                            .ToDictionary(
                                g => g.Key,
                                g => g.ToList());

                var workOrders = await GetOrdersAsync(startTime, endTime);
                foreach (var order in workOrders)
                {
                    if (lineDic.ContainsKey(order.WorkOrderId))
                        order.WorkOrderLines = lineDic[order.WorkOrderId];
                    if (remarksByOrderId.ContainsKey(order.WorkOrderId))
                        order.Remarks = remarksByOrderId[order.WorkOrderId];
                    if (contractMapToStore != null && contractMapToStore.ContainsKey(order.StoreId))
                        order.Contracts = contractMapToStore[order.StoreId];
                }

                //_workOrderMap = workOrders.ToDictionary(x => x.WorkOrderId, o => o);

                return workOrders;
            }
            finally
            {
                if (lockAcquired)
                {
                    orderRemaphore.Release();
                }
            }
        }

        public async Task<List<ContractDto>> GetFullContractsAsync(List<string> storeIds)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync();
                var parameterNames = storeIds
                       .Select((_, index) => $"@storeId{index}")
                       .ToList();

                var sql = $@"SELECT *
                            FROM eContract
                            WHERE Status = 'Active'
                            AND StoreId IN ({string.Join(",", parameterNames)})";

                await using var command =
                    new SqlCommand(sql, connection);

                for (int i = 0; i < storeIds.Count; i++)
                {
                    command.Parameters.AddWithValue(
                        $"@storeId{i}",
                        storeIds[i]);
                }

                var reader = await command.ExecuteReaderAsync();
                var contracts = new List<ContractDto>();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var temp = new ContractDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("ContractID")),
                            StoreId = reader.GetInt32(reader.GetOrdinal("StoreID")),
                            Status = Enum.Parse<ContractStatus>(reader.GetString(reader.GetOrdinal("Status")))
                        };

                        contracts.Add(temp);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                _logger.LogInformation($"Contract count: {contracts.Count}");

                var cids = contracts.Select(x => x.Id).ToList();
                var items = await GetContractItemsAsync(cids);
                var itemsDic = items.GroupBy(x => x.ContractId).ToDictionary(
                                                                        g => g.Key,
                                                                        g => g.ToList()
                                                                    );
                var revisions = await GetContractRevisionsAsync();
                foreach(var contract in contracts)
                {
                    contract.Items = itemsDic.ContainsKey(contract.Id) ? itemsDic[contract.Id] : new List<ContractItemDto>();
                    contract.ContractTerm = revisions.ContainsKey(contract.Id) ? revisions[contract.Id] : null;
                } 

                return contracts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<ContractDto>();
            }
        }

        public async Task<List<ContractDto>> GetFullContractsAsync()
        {
            var lockAcquired = false;
            try
            {
                await contractRemaphore.WaitAsync();
                lockAcquired = true;
                if (_contracts?.Count > 0) return _contracts;

                var contracts = await GetContractsAsync();
                var contractItems = await GetContractItemsAsync();
                var revisions = await GetContractRevisionsAsync();
                var coverages = await GetItemCoveragesAsync();

                var map = contractItems.GroupBy(
                        x => x.ContractId).ToDictionary(
                        k => k.Key, v => v.ToList());

                foreach (var contract in contracts)
                {
                    if (map.ContainsKey(contract.Id)) contract.Items = map[contract.Id];
                    if (contract.Items == null) continue;
                    if (revisions.ContainsKey(contract.Id))
                    {
                        contract.ContractTerm = revisions[contract.Id];
                    }

                    foreach (var item in contract.Items)
                    {
                        if (item.SoftwareCoverageId != null && coverages.ContainsKey((int)item.SoftwareCoverageId))
                        {
                            item.SoftwareCoverages?.Clear();
                            item.SoftwareCoverages?.AddRange(coverages[(int)item.SoftwareCoverageId]);
                        }
                        if (item.HardwareCoverageId != null && coverages.ContainsKey((int)item.HardwareCoverageId))
                        {
                            item.HardwareCoverages?.Clear();
                            item.HardwareCoverages?.AddRange(coverages[(int)item.HardwareCoverageId]);
                        }
                    }
                }

                _contracts = contracts;
                return contracts;
            }
            finally
            {
                if (lockAcquired)
                {
                    contractRemaphore.Release();
                }
            }
        }

        private async Task<List<ContractDto>> GetContractByStoreAsync(string storeId, CancellationToken cancellationToken = default)
        {
            try
            {
                var items = await GetContractItemsAsync(cancellationToken);
                var itemsDic = items.GroupBy(x => x.ContractId).ToDictionary(
                                                                        g => g.Key,
                                                                        g => g.ToList()
                                                                    );
                var terms = await GetContractRevisionsAsync(cancellationToken);
                var coverages = await GetItemCoveragesAsync();

                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync();
                var sql = $"select * from eContract where StoreID = {storeId} and status = 'Active'";
                await using var command = new SqlCommand(sql, connection);
                var reader = await command.ExecuteReaderAsync();
                var res = new List<ContractDto>();

                while (await reader.ReadAsync() && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var temp = new ContractDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("ContractID")),
                            StoreId = reader.GetInt32(reader.GetOrdinal("StoreID")),
                            Status = Enum.Parse<ContractStatus>(reader.GetString(reader.GetOrdinal("Status")))
                        };

                        temp.Items = itemsDic.ContainsKey(temp.Id) ? itemsDic[temp.Id] : new List<ContractItemDto>();
                        temp.ContractTerm = terms[temp.Id];
                        foreach (var item in temp.Items)
                        {
                            if (item.SoftwareCoverageId != null && coverages.ContainsKey((int)item.SoftwareCoverageId))
                            {
                                item.SoftwareCoverages?.Clear();
                                item.SoftwareCoverages?.AddRange(coverages[(int)item.SoftwareCoverageId]);
                            }
                            if (item.HardwareCoverageId != null && coverages.ContainsKey((int)item.HardwareCoverageId))
                            {
                                item.HardwareCoverages?.Clear();
                                item.HardwareCoverages?.AddRange(coverages[(int)item.HardwareCoverageId]);
                            }
                        }
                        var str = temp.Content;
                        res.Add(temp);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                _logger.LogInformation($"Contract count: {res.Count}");

                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<ContractDto>();
            }
        }

        public async Task<List<WorkOrderlineDto>> GetLineForOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select * from eWorkOrderline where WorkOrderId = {orderId}";
                await using var command = new SqlCommand(sql, connection);

                var res = new List<WorkOrderlineDto>();
                var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    try
                    {
                        var userId = reader.GetInt32(reader.GetOrdinal("TechnicianID"));
                        var temp = new WorkOrderlineDto
                        {
                            WorkOrderId = int.Parse(orderId),
                            LineNumber = reader.GetInt32(reader.GetOrdinal("LineNumber")),
                            TechnicianId = userId,
                            //Technician = _users.ContainsKey(userId) ? _users[userId] : string.Empty,
                            //TechnicianType = _technicians.ContainsKey(userId) ? Enum.Parse<UserType>(_technicians[userId]) : UserType.Software,
                            ContractId = reader.GetInt32(reader.GetOrdinal("ContractId")),
                            ContractItemId = reader.IsDBNull(reader.GetOrdinal("ContractItemID")) ? 0 : reader.GetInt32(reader.GetOrdinal("ContractItemID")),
                            Status = Enum.Parse<InlineStatus>(reader.GetString(reader.GetOrdinal("Status"))),
                            CreatedTime = reader.IsDBNull(reader.GetOrdinal("CreatedTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("CreatedTimeLocal")),
                            DispatchTime = reader.IsDBNull(reader.GetOrdinal("DispatchTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("DispatchTimeLocal")),
                            ArrivalTime = reader.IsDBNull(reader.GetOrdinal("ArrivalTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("ArrivalTimeLocal")),
                            CompleteTime = reader.IsDBNull(reader.GetOrdinal("CompleteTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("CompleteTimeLocal")),
                            //Remarks = remarksByOrderId.ContainsKey(orderId) ? remarksByOrderId[orderId] : null
                        };

                        res.Add(temp);
                    }
                    catch (Exception ex)
                    {

                    }
                }
                _logger.LogInformation($"Order line count: {res.Count}");

                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<WorkOrderlineDto>();
            }
        }

        public async Task<List<WorkOrderRemarkDto>> GetRemarksForOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return new List<WorkOrderRemarkDto>();
            }

            try
            {
                var users = await GetAllUsersAsync(cancellationToken);

                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select * from eWorkOrderRemark where WorkOrderID = @OrderId";
                await using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@OrderId", orderId);

                var reader = await command.ExecuteReaderAsync();

                var orderRemarkList = new List<WorkOrderRemarkDto>();
                while (await reader.ReadAsync())
                {
                    var userId = reader.GetInt32(reader.GetOrdinal("UserID"));
                    var temp = new WorkOrderRemarkDto
                    {
                        WorkOrderRemarkID = reader.GetInt32(reader.GetOrdinal("WorkOrderRemarkID")),
                        WorkOrderID = reader.GetInt32(reader.GetOrdinal("WorkOrderID")),
                        TechnicianId = userId,
                        Technician = users.ContainsKey(userId) ? users[userId] : string.Empty,
                        Time = reader.IsDBNull(reader.GetOrdinal("Time")) ? null : reader.GetDateTime(reader.GetOrdinal("Time")),
                        RemarkType = reader.IsDBNull(reader.GetOrdinal("RemarkType")) ?
                                    CustomerRemarkType.Internal : Enum.Parse<CustomerRemarkType>(reader.GetString(reader.GetOrdinal("RemarkType"))),
                        Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? null : reader.GetString(reader.GetOrdinal("Content")),

                    };
                    orderRemarkList.Add(temp);

                }
                _logger.LogInformation($"Order remark count: {orderRemarkList.Count}");

                return orderRemarkList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<WorkOrderRemarkDto>();
            }
        }

        private async Task<List<ContractDto>> GetContractsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync();
                var sql = "select * from eContract where status = 'Active'";
                await using var command = new SqlCommand(sql, connection);
                var reader = await command.ExecuteReaderAsync();
                var contracts = new List<ContractDto>();

                while (await reader.ReadAsync() && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var temp = new ContractDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("ContractID")),
                            StoreId = reader.GetInt32(reader.GetOrdinal("StoreID")),
                            Status = Enum.Parse<ContractStatus>(reader.GetString(reader.GetOrdinal("Status")))
                        };

                        contracts.Add(temp);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                _logger.LogInformation($"Contract count: {contracts.Count}");

                return contracts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<ContractDto>();
            }
        }

        private async Task<Dictionary<int, List<Coverage>>> GetItemCoveragesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync();
                var sql = "select * from eCoverageDetail";
                await using var command = new SqlCommand(sql, connection);
                var reader = await command.ExecuteReaderAsync();
                var coverages = new Dictionary<int, List<Coverage>>();

                while (await reader.ReadAsync() && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var id = reader.GetInt32(reader.GetOrdinal("CoverageID"));
                        if(!coverages.ContainsKey(id)) coverages[id] = new List<Coverage>();
                        var temp = new Coverage
                        {
                            Id = id,
                            Day = Enum.Parse<CoverageDay>(reader.GetString(reader.GetOrdinal("Day"))),
                            StartTime = TimeOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("StartTime"))),
                            EndTime = TimeOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("EndTime")))
                        };

                        coverages[id].Add(temp);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                _logger.LogInformation($"Contract coverage count: {coverages.Count}");

                return coverages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new Dictionary<int, List<Coverage>>();
            }
        }

        private async Task<List<ContractItemDto>> GetContractItemsAsync(List<int> contractIds, CancellationToken cancellationToken = default)
        {
            var parameterNames = contractIds
                                .Select((_, i) => $"@contractId{i}")
                                .ToList();

            var sql = """
                            WITH LatestRevision AS
                            (
                                SELECT *,
                                       ROW_NUMBER() OVER
                                       (
                                           PARTITION BY ContractID, ContractItemID
                                           ORDER BY RevisionNumber DESC
                                       ) AS rn
                                FROM eContractItem
                                WHERE ContractID IN ({string.Join(", ", parameterNames)})
                            )
                            SELECT *
                            FROM LatestRevision
                            WHERE rn = 1;
                            """;

            var revisions = await GetContractRevisionsAsync(contractIds, cancellationToken);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);

            if (contractIds != null)
            {
                for (var i = 0; i < contractIds.Count; i++)
                {
                    command.Parameters.Add(
                        $"@contractId{i}",
                        SqlDbType.Int).Value = contractIds[i];
                }
            }

            return await GetContractItemsHelperAsync(command, revisions, cancellationToken);
        }
        private async Task<List<ContractItemDto>> GetContractItemsAsync(CancellationToken cancellationToken = default)
        {
            var sql = """
                            WITH LatestRevision AS
                            (
                                SELECT *,
                                       ROW_NUMBER() OVER
                                       (
                                           PARTITION BY ContractID, ContractItemID
                                           ORDER BY RevisionNumber DESC
                                       ) AS rn
                                FROM eContractItem
                            )
                            SELECT *
                            FROM LatestRevision
                            WHERE rn = 1;
                            """;

            var revisions = await GetContractRevisionsAsync(cancellationToken);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);

            return await GetContractItemsHelperAsync(command, revisions, cancellationToken);
        }

        private async Task<List<ContractItemDto>> GetContractItemsHelperAsync(SqlCommand command, Dictionary<int, ContractRevisionDto> revision, CancellationToken cancellationToken = default)
        {
            try
            {
                var reader = await command.ExecuteReaderAsync();
                var contractItems = new List<ContractItemDto>();
                while (await reader.ReadAsync() && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var cid = reader.GetInt32(reader.GetOrdinal("ContractID"));
                        var temp = new ContractItemDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("ContractItemID")),
                            ContractId = cid,
                            RevisionNumber = reader.GetInt32(reader.GetOrdinal("RevisionNumber")),
                            ItemType = reader.GetString(reader.GetOrdinal("HardwareOrSoftware")) == "Software" ? ContractType.Software : ContractType.Hardware,
                            ProductTypeId = reader.GetInt32(reader.GetOrdinal("ProductTypeID")),
                            ProductName = reader.IsDBNull(reader.GetOrdinal("Description")) ? "" : reader.GetString(reader.GetOrdinal("Description")),
                            WarrantyStartDate = reader.IsDBNull(reader.GetOrdinal("WarrantyStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("WarrantyStartDate")),
                            WarrantyEndDate = reader.IsDBNull(reader.GetOrdinal("WarrantyEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("WarrantyEndDate")).Date.AddDays(1).AddTicks(-1),
                            ContractStartDate = reader.IsDBNull(reader.GetOrdinal("ContractStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ContractStartDate")),
                            ContractEndDate = reader.IsDBNull(reader.GetOrdinal("ContractEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ContractEndDate")).Date.AddDays(1).AddTicks(-1),
                            //Coverages 
                            SoftwareCoverageId = revision.ContainsKey(cid) ? revision[cid].SoftwareCoverageId : null,
                            HardwareCoverageId = revision.ContainsKey(cid) ? revision[cid].HardwareCoverageId : null,
                        };

                        contractItems.Add(temp);
                    }
                    catch (Exception ex)
                    {
                    }
                }
                _logger.LogInformation($"Contract item count: {contractItems.Count}");

                return contractItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<ContractItemDto>();
            }
        }

        private async Task<Dictionary<int, ContractRevisionDto>> GetContractRevisionsAsync(List<int> contractIds, CancellationToken cancellationToken = default)
        {
            var parameterNames = contractIds
                                .Select((_, i) => $"@contractId{i}")
                                .ToList();

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync();

            var sql = """
                            WITH LatestRevision AS
                            (
                                SELECT *,
                                       ROW_NUMBER() OVER
                                       (
                                           PARTITION BY ContractID
                                           ORDER BY RevisionNumber DESC
                                       ) AS rn
                                FROM eContractRevision
                                WHERE ContractID IN ({string.Join(", ", parameterNames)})
                            )
                            SELECT *
                            FROM LatestRevision
                            WHERE rn = 1;
                            """;
            await using var command = new SqlCommand(sql, connection);

            if (contractIds != null)
            {
                for (var i = 0; i < contractIds.Count; i++)
                {
                    command.Parameters.Add(
                        $"@contractId{i}",
                        SqlDbType.Int).Value = contractIds[i];
                }
            }

            return await GetContractRevisionsHelperAsync(command);
        }


        private async Task<Dictionary<int, ContractRevisionDto>> GetContractRevisionsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync();

            var sql = """
                            WITH LatestRevision AS
                            (
                                SELECT *,
                                       ROW_NUMBER() OVER
                                       (
                                           PARTITION BY ContractID
                                           ORDER BY RevisionNumber DESC
                                       ) AS rn
                                FROM eContractRevision
                            )
                            SELECT *
                            FROM LatestRevision
                            WHERE rn = 1;
                            """;
            await using var command = new SqlCommand(sql, connection);
            return await GetContractRevisionsHelperAsync(command);
        }


        private async Task<Dictionary<int, ContractRevisionDto>> GetContractRevisionsHelperAsync(SqlCommand command, CancellationToken cancellationToken = default)
        {
            try
            {
                var reader = await command.ExecuteReaderAsync();

                var dic = new Dictionary<int, ContractRevisionDto>();
                while (await reader.ReadAsync() && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var temp = new ContractRevisionDto
                        {
                            ContractId = reader.GetInt32(reader.GetOrdinal("ContractID")),
                            SWWarrantyStartDate = reader.IsDBNull(reader.GetOrdinal("SWWarrantyStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("SWWarrantyStartDate")),
                            SWWarrantyEndDate = reader.IsDBNull(reader.GetOrdinal("SWWarrantyEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("SWWarrantyEndDate")).AddDays(1),
                            SWContractStartDate = reader.IsDBNull(reader.GetOrdinal("SWContractStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("SWContractStartDate")),
                            SWContractEndDate = reader.IsDBNull(reader.GetOrdinal("SWContractEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("SWContractEndDate")).AddDays(1),
                            HWWarrantyStartDate = reader.IsDBNull(reader.GetOrdinal("HWWarrantyStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("HWWarrantyStartDate")),
                            HWWarrantyEndDate = reader.IsDBNull(reader.GetOrdinal("HWWarrantyEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("HWWarrantyEndDate")).AddDays(1),
                            HWContractStartDate = reader.IsDBNull(reader.GetOrdinal("HWContractStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("HWContractStartDate")),
                            HWContractEndDate = reader.IsDBNull(reader.GetOrdinal("HWContractEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("HWContractEndDate")).AddDays(1),
                            SoftwareCoverageId = reader.IsDBNull(reader.GetOrdinal("SoftwareCoverageId")) ? 0 : reader.GetInt32(reader.GetOrdinal("SoftwareCoverageId")),
                            HardwareCoverageId = reader.IsDBNull(reader.GetOrdinal("HardwareCoverageId")) ? 0 : reader.GetInt32(reader.GetOrdinal("HardwareCoverageId")),
                            //Coverages 
                        };

                        dic[temp.ContractId] = temp;
                    }
                    catch (Exception ex)
                    {
                    }
                }
                _logger.LogInformation($"Contract Revision count: {dic.Count}");

                return dic;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new Dictionary<int, ContractRevisionDto>();
            }
        }

        private async Task<WorkOrderDto> ReadOrderDataAsync(SqlDataReader reader)
        {
            try
            {
                var orderId = reader.GetInt32(reader.GetOrdinal("WorkOrderID"));
                var storeId = reader.GetInt32(reader.GetOrdinal("StoreID"));
                var customerId = _stores.ContainsKey(storeId) ? int.Parse(_stores[storeId]) : 0;
                var accountId = _customers.ContainsKey(customerId) ? int.Parse(_customers[customerId]) : 0;
                var majorAccountName = _majorAccounts.ContainsKey(accountId) ? _majorAccounts[accountId] : string.Empty;

                var temp = new WorkOrderDto
                {
                    WorkOrderId = orderId,
                    StoreId = storeId,
                    StoreName = reader.IsDBNull(reader.GetOrdinal("StoreName")) ? null : reader.GetString(reader.GetOrdinal("StoreName")),
                    CountryString = reader.IsDBNull(reader.GetOrdinal("Country")) ? null : reader.GetString(reader.GetOrdinal("Country")),
                    CreateDateTime = reader.IsDBNull(reader.GetOrdinal("CreatedTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("CreatedTimeLocal")),
                    Status = reader.IsDBNull(reader.GetOrdinal("Status")) ? OrderStatus.Canceled : Enum.Parse<OrderStatus>(reader.GetString(reader.GetOrdinal("Status"))),
                    BillingStatus = reader.GetDecimal(reader.GetOrdinal("Total")) > 0 ? BillingStatus.Billable : BillingStatus.Free,
                    InitialSeverity = reader.GetInt32(reader.GetOrdinal("InitialSeverity")),
                    CurrentSeverity = reader.GetInt32(reader.GetOrdinal("Severity")),
                    ProjectId = reader.GetInt32(reader.GetOrdinal("JobID")),
                    FirstRemarkId = reader.IsDBNull(reader.GetOrdinal("FirstRemarkID")) ? null : reader.GetInt32(reader.GetOrdinal("FirstRemarkID")),
                    LastRemarkId = reader.IsDBNull(reader.GetOrdinal("LastRemarkID")) ? null : reader.GetInt32(reader.GetOrdinal("LastRemarkID")),
                    IsMajorAccount = _customers.ContainsKey(customerId) ? true : false,
                    MajorAccountName = majorAccountName
                    //WorkOrderLines = lineDic.ContainsKey(orderId) ? lineDic[orderId] : null
                };
                return temp;
            }
            catch (Exception ex)
            {
                return new WorkOrderDto();
            }

        }

        private async Task<List<WorkOrderDto>> GetOrdersAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync();
                var sql = @"
                            SELECT *
                            FROM eWorkOrder
                            WHERE Status = 'Billed'
                              AND CreatedTime >= @StartTime
                              AND CreatedTime < @EndTime";
                await using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 120;
                command.Parameters.Add(
                    "@StartTime",
                    System.Data.SqlDbType.DateTime2).Value = startTime;

                command.Parameters.Add(
                    "@EndTime",
                    System.Data.SqlDbType.DateTime2).Value = endTime;

                var reader = await command.ExecuteReaderAsync();
                var workOrders = new List<WorkOrderDto>();
                while (await reader.ReadAsync() && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {                        
                        workOrders.Add(await ReadOrderDataAsync(reader));
                    }
                    catch (Exception ex)
                    {
                    }
                }
                _logger.LogInformation($"Order count: {workOrders.Count}");

                return workOrders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<WorkOrderDto>();
            }
        }

        private async Task<List<WorkOrderlineDto>> GetAllLinesAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select * from eWorkOrderline where CreatedTime >= '{startTime}' and CreatedTime < '{endTime}'";
                await using var command = new SqlCommand(sql, connection);
                
                var reader = await command.ExecuteReaderAsync();
                var workLines = new List<WorkOrderlineDto>();
                while (await reader.ReadAsync())
                {
                    try
                    {
                        var userId = reader.GetInt32(reader.GetOrdinal("TechnicianID"));
                        var orderId = reader.GetInt32(reader.GetOrdinal("WorkOrderId"));
                        var temp = new WorkOrderlineDto
                        {
                            WorkOrderId = orderId,
                            LineNumber = reader.GetInt32(reader.GetOrdinal("LineNumber")),
                            TechnicianId = userId,
                            //Technician = _users.ContainsKey(userId) ? _users[userId] : string.Empty,
                            //TechnicianType = _technicians.ContainsKey(userId) ? Enum.Parse<UserType>(_technicians[userId]) : UserType.Software,
                            ContractId = reader.GetInt32(reader.GetOrdinal("ContractId")),
                            ContractItemId = reader.IsDBNull(reader.GetOrdinal("ContractItemID")) ? 0 : reader.GetInt32(reader.GetOrdinal("ContractItemID")),
                            Status = Enum.Parse<InlineStatus>(reader.GetString(reader.GetOrdinal("Status"))),
                            CreatedTime = reader.IsDBNull(reader.GetOrdinal("CreatedTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("CreatedTimeLocal")),
                            DispatchTime = reader.IsDBNull(reader.GetOrdinal("DispatchTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("DispatchTimeLocal")),
                            ArrivalTime = reader.IsDBNull(reader.GetOrdinal("ArrivalTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("ArrivalTimeLocal")),
                            CompleteTime = reader.IsDBNull(reader.GetOrdinal("CompleteTimeLocal")) ? null : reader.GetDateTime(reader.GetOrdinal("CompleteTimeLocal")),
                            //Remarks = remarksByOrderId.ContainsKey(orderId) ? remarksByOrderId[orderId] : null
                        };

                        workLines.Add(temp);
                    }
                    catch(Exception ex)
                    {
                    
                    }
                }
                _logger.LogInformation($"Order line count: {workLines.Count}");

                return workLines;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<WorkOrderlineDto>();
            }
        }

        private async Task<List<WorkOrderRemarkDto>> GetAllRemarksAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select * from eWorkOrderRemark where Time >= '{startTime}' and Time < '{endTime}'";
                await using var command = new SqlCommand(sql, connection);

                var reader = await command.ExecuteReaderAsync();
                var workOrderRemarks = new List<WorkOrderRemarkDto>();
                while (await reader.ReadAsync())
                {
                    try
                    {
                        var userId = reader.GetInt32(reader.GetOrdinal("UserID"));
                        var temp = new WorkOrderRemarkDto
                        {
                            WorkOrderRemarkID = reader.GetInt32(reader.GetOrdinal("WorkOrderRemarkID")),
                            WorkOrderID = reader.GetInt32(reader.GetOrdinal("WorkOrderID")),
                            TechnicianId = userId,
                            Time = reader.IsDBNull(reader.GetOrdinal("Time")) ? null : reader.GetDateTime(reader.GetOrdinal("Time")),
                            RemarkType = reader.IsDBNull(reader.GetOrdinal("RemarkType")) ?
                                        CustomerRemarkType.Internal : Enum.Parse<CustomerRemarkType>(reader.GetString(reader.GetOrdinal("RemarkType"))),
                            Content = reader.IsDBNull(reader.GetOrdinal("Content")) ? null : reader.GetString(reader.GetOrdinal("Content")),

                        };
                        workOrderRemarks.Add(temp);
                        //break;
                    }
                    catch(Exception ex)
                    {

                    }
                }
                _logger.LogInformation($"Order remark count: {workOrderRemarks.Count}");

                return workOrderRemarks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new List<WorkOrderRemarkDto>();
            }
        }

        private async Task<Dictionary<int, string>> GetAllUsersAsync(CancellationToken cancellationToken = default)
        {
            try
            {

                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select userid, username from eUser";
                await using var command = new SqlCommand(sql, connection);

                var reader = await command.ExecuteReaderAsync();
                _users = new Dictionary<int, string>();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(reader.GetOrdinal("UserID"));
                    var userName = reader.IsDBNull(reader.GetOrdinal("UserName")) ? string.Empty : reader.GetString(reader.GetOrdinal("UserName"));
                    _users[id] = userName;
                }
                _logger.LogInformation($"User count: {_users.Count}");

                return _users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new Dictionary<int, string>();
            }
        }

        private async Task<Dictionary<int, string>> GetAllTechniciansAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select userid, technicianType from eTechnician";
                await using var command = new SqlCommand(sql, connection);

                var reader = await command.ExecuteReaderAsync();
                _technicianTypes = new Dictionary<int, string>();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(reader.GetOrdinal("UserID"));
                    var techType = reader.IsDBNull(reader.GetOrdinal("TechnicianType")) ? string.Empty : reader.GetString(reader.GetOrdinal("TechnicianType"));
                    _technicianTypes[id] = techType;
                }
                _logger.LogInformation($"Technician count: {_technicianTypes.Count}");

                return _technicianTypes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new Dictionary<int, string>();
            }
        }

        private async Task<Dictionary<int, string>> GetAllMajorAccountsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation(_options.ConnectionString);

                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select * from eMajorAccount";
                await using var command = new SqlCommand(sql, connection);

                var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(reader.GetOrdinal("MajorAccountID"));
                    var accountName = reader.IsDBNull(reader.GetOrdinal("MajorAccountName")) ? string.Empty : reader.GetString(reader.GetOrdinal("MajorAccountName"));
                    _majorAccounts[id] = accountName;
                }
                _logger.LogInformation($"Najor Account count: {_majorAccounts.Count}");

                return _majorAccounts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new Dictionary<int, string>();
            }
        }

        private async Task<Dictionary<int, string>> GetAllCustomersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select customerid, majorAccountId from eCustomer";
                await using var command = new SqlCommand(sql, connection);

                var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(reader.GetOrdinal("customerid"));
                    var majorId = reader.IsDBNull(reader.GetOrdinal("majorAccountId")) ? 0 : reader.GetInt32(reader.GetOrdinal("majorAccountId"));
                    _customers[id] = majorId.ToString();
                }
                _logger.LogInformation($"Customer count: {_customers.Count}");

                return _customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new Dictionary<int, string>();
            }
        }


        private async Task<Dictionary<int, string>> GetAllStoresAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var connection = new SqlConnection(_options.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                var sql = $"select storeId, customerid from eStore";
                await using var command = new SqlCommand(sql, connection);

                var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(reader.GetOrdinal("storeId"));
                    var customerId = reader.IsDBNull(reader.GetOrdinal("customerid")) ? 0 : reader.GetInt32(reader.GetOrdinal("customerid"));
                    _stores[id] = customerId.ToString();
                }
                _logger.LogInformation($"Store count: {_stores.Count}");

                return _stores;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when reading from the database.");
                return new Dictionary<int, string>();
            }
        }

        public void Dispose()
        {
            try
            {
                initializeSemaphore?.Dispose();
                orderRemaphore?.Dispose();
                contractRemaphore?.Dispose();
            }
            catch (Exception ex)
            {
                // dispose should not throw
                _logger?.LogDebug(ex, "Exception during DatabaseService.Dispose");
            }
        }
    }
}


