using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Serilog;
using TempAndHumiTest.Models;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.Common
{
    public interface IBusinessLogService
    {
        /// <summary>
        ///  插入数据
        /// </summary>
        Task TryAddLogAsync(LogModel businessLog, CancellationToken token = default);

        Task<PagedResult<LogModel>> GetAllLogAsync(LogQuery businessLog, CancellationToken token = default, int count = 1000);
    }

    public class BusinessLogService : IBusinessLogService
    {
        private readonly IBusinessLogRepository _businessLogRepository;

        private ILogService<BusinessLogService> _logService;
        public BusinessLogService(IBusinessLogRepository logRepository, ILogService<BusinessLogService> businessLogService)
        {
            this._businessLogRepository = logRepository;
            this._logService = businessLogService;

        }

        public async Task<PagedResult<LogModel>> GetAllLogAsync(LogQuery businessLog, CancellationToken token = default, int count = 1000)
        {
            try
            {
                _logService.Info("查询业务日志");
                return await _businessLogRepository.QueryAsync(businessLog, token, count);
            }
            catch (Exception ex)
            {
                _logService.Error("查询业务日志失败,失败原因：{Message}", ex, ex.Message);
                throw;
            }
        }

        public async Task TryAddLogAsync(LogModel businessLog, CancellationToken token = default)
        {
            try
            {
                await _businessLogRepository.InsertAsync(businessLog, token);
            }
            catch (Exception ex)
            {
                _logService.Error("添加业务日志失败,失败原因：{Message}", ex, ex.Message);
                throw;
            }

        }
    }


    public interface IBusinessLogRepository
    {
        Task InsertAsync(LogModel logModel, CancellationToken token = default);

        Task<PagedResult<LogModel>> QueryAsync(LogQuery logModel, CancellationToken token = default, int count = 1000);

        Task<PagedResult<LogModel>> QueryAfterId(long id, CancellationToken token = default, int count = 200);
    }

    public class BusinessLogRepository : IBusinessLogRepository
    {
        // 连接信息
        private readonly string _connectionString;
        private ILogService<BusinessLogService> _logService;

        public BusinessLogRepository(string connectionString, ILogService<BusinessLogService> logService)
        {
            _connectionString = connectionString;
            _logService = logService;
        }
        /// <summary>
        /// 添加日志
        /// </summary>
        /// <param name="logModel">日志模型</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        public async Task InsertAsync(LogModel logModel, CancellationToken token = default)
        {
            await using var connection = new SqliteConnection(this._connectionString);
            await connection.OpenAsync(token);
            // await using var command 这种写法的意思是会释放，作用域内完成就会释放
            await using var command = connection.CreateCommand();
            string sql = "insert into business_logs (created,slave_id,temperature,humidity,log_level,state,remark) " +
                "values (@created,@slaveId,@temperature,@humidity,@level,@state,@remark)";
            command.CommandText = sql;
            command.Parameters.AddWithValue("@created", logModel.Created);
            command.Parameters.AddWithValue("@slaveId", logModel.SlaveId);
            command.Parameters.AddWithValue("@temperature", logModel.Temperature);
            command.Parameters.AddWithValue("@humidity", logModel.Humidity);
            command.Parameters.AddWithValue("@level", logModel.LogLevel);
            command.Parameters.AddWithValue("@state", logModel.State);
            command.Parameters.AddWithValue("@remark", logModel.Remark);
            this.LogSql(command, "InsertAsync");
            await command.ExecuteNonQueryAsync(token);
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="logModel"></param>
        /// <param name="token"></param>
        /// <param name="count">总数</param>
        /// <returns></returns>
        public async Task<PagedResult<LogModel>> QueryAsync(LogQuery logModel, CancellationToken token = default, int count = 1000)
        {
            List<LogModel> list = new();
            var pageNumber = logModel.PageNumber;
            var pageSize = logModel.PageSize;
            int offset = (pageNumber - 1) * pageSize;

            await using var connection = new SqliteConnection(this._connectionString);
            await connection.OpenAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            var sql = new StringBuilder("select * from business_logs where 1=1");
            var countSql = new StringBuilder("select count(id) from business_logs where 1=1");
            if (!string.IsNullOrWhiteSpace(logModel.StartTime))
            {
                sql.Append(" AND created >= @from");
                countSql.Append(" AND created >= @from");
                command.Parameters.AddWithValue("@from", logModel.StartTime);

            }
            if (!string.IsNullOrWhiteSpace(logModel.EndTime))
            {
                sql.Append(" AND created < @to");
                countSql.Append(" AND created < @to");
                command.Parameters.AddWithValue("@to", logModel.EndTime);

            }
            if (!string.IsNullOrWhiteSpace(logModel.SlaveId))
            {
                sql.Append(" And slave_id = @slaveId");
                countSql.Append(" And slave_id = @slaveId");
                command.Parameters.AddWithValue("@slaveId", logModel.SlaveId);
            }
            if (logModel.LogLevel != LogLevel.Debug)
            {
                sql.Append(" And log_level = @level");
                countSql.Append(" And log_level = @level");
                command.Parameters.AddWithValue("@level", logModel.LogLevel);
            }
            
            sql.Append(" order by id desc");
            sql.Append(" LIMIT @pageSize OFFSET @offset");
            command.Parameters.AddWithValue("@pageSize", pageSize);
            command.Parameters.AddWithValue("@offset", offset);

            command.CommandText = sql.ToString();
            this.LogSql(command, "QueryAsync");
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                list.Add(Map(reader));
            }
            var totalCount = await this.QueryCount(countSql.ToString(), logModel, token);
            PagedResult<LogModel> result = new PagedResult<LogModel>(list, totalCount, pageNumber, pageSize);
            return result;
        }

        private async Task<int> QueryCount(
            string sql,
            LogQuery logModel,
            CancellationToken token)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(token);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            // 绑定与 COUNT 查询 WHERE 条件对应的参数
            if (!string.IsNullOrWhiteSpace(logModel.StartTime))
            {
                command.Parameters.AddWithValue("@from", logModel.StartTime);
            }

            if (!string.IsNullOrWhiteSpace(logModel.EndTime))
            {
                command.Parameters.AddWithValue("@to", logModel.EndTime);
            }

            if (!string.IsNullOrWhiteSpace(logModel.SlaveId))
            {
                command.Parameters.AddWithValue("@slaveId", logModel.SlaveId);
            }

            if (logModel.LogLevel != LogLevel.Debug)
            {
                command.Parameters.AddWithValue("@level", logModel.LogLevel);
            }
            this.LogSql(command, "QueryCount");
            //
            var result = await command.ExecuteScalarAsync(token);
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// 追加查询，通过查询id后面的默认200条数据进行追加
        /// </summary>
        /// <param name="id">id</param>
        /// <param name="token">取消令牌</param>
        /// <param name="count">总数</param>
        /// <returns></returns>
        public async Task<PagedResult<LogModel>> QueryAfterId(long id, CancellationToken token = default, int count = 200)
        {
            List<LogModel> list = new();
            await using var connection = new SqliteConnection(this._connectionString);
            await connection.OpenAsync(token);
            await using SqliteCommand command = connection.CreateCommand();
            var sql = new StringBuilder("select * from business_logs where id > @id ORDER BY id DESC LIMIT @count");
            command.CommandText = sql.ToString();
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@count", count);
            this.LogSql(command, "QueryAfterId");
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                list.Add(Map(reader));
            }
            PagedResult<LogModel> result = new PagedResult<LogModel>(list, 0, 0, 0);
            return result;
        }

        private static LogModel Map(SqliteDataReader r) => new()
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Created = DateTime.Parse(
                      r.GetString(r.GetOrdinal("created"))),
            SlaveId = r.GetString(r.GetOrdinal("slave_id")),
            Temperature = r.GetDouble(r.GetOrdinal("temperature")),
            Humidity = r.GetDouble(r.GetOrdinal("humidity")),
            LogLevel = (LogLevel)r.GetInt32(r.GetOrdinal("log_level")),
            State = r.GetInt32(r.GetOrdinal("state")),
            Remark = r.GetString(r.GetOrdinal("remark"))
        };

        /// <summary>
        /// 打印sql，带参数
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="tag"></param>
        public void LogSql(SqliteCommand cmd, string tag = "")
        {
            var sql = new StringBuilder(cmd.CommandText);
            if (cmd.Parameters.Count > 0)
            {
                sql.AppendLine();
                sql.Append("-- 参数: ");
                foreach (SqliteParameter p in cmd.Parameters)
                {
                    sql.Append($"{p.ParameterName}={FormatValue(p.Value)}, ");
                }
                sql.Length -= 2; // 去掉末尾 ", "
            }
            _logService.Info("[{Tag}] {Sql}", tag, sql.ToString());
        }

        private static string FormatValue(object? v) => v switch
        {
            null or DBNull => "NULL",
            string s => $"'{s.Replace("'", "''")}'", // 字符串加引号并转义
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'",
            _ => v.ToString() ?? "NULL"
        };
    }
}
