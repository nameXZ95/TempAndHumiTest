using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;

namespace TempAndHumiTest.Common
{
    public class DatabaseInitializer
    {
        private readonly string _connectionStr;

        public DatabaseInitializer(string connectionStr)
        {
            this._connectionStr = connectionStr;
        }

        /// <summary>
        /// 建表语句
        /// </summary>
        /// <param name="cancellationToken"></param>
        public async void Initialize(CancellationToken cancellationToken = default) 
        {
            try
            {
                await using var connection = new SqliteConnection(this._connectionStr);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                CREATE TABLE IF NOT EXISTS business_logs (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    created     TEXT    NOT NULL,
                    slave_id    TEXT    NOT NULL DEFAULT '',
                    temperature REAL    NOT NULL DEFAULT 0,
                    humidity    REAL    NOT NULL DEFAULT 0,
                    log_level   INTEGER NOT NULL DEFAULT 0,
                    state       INTEGER NOT NULL DEFAULT 0,
                    remark      TEXT    NOT NULL DEFAULT ''
                );

                CREATE INDEX IF NOT EXISTS idx_business_logs_created
                    ON business_logs (created DESC);

                CREATE INDEX IF NOT EXISTS idx_business_logs_slave_created
                    ON business_logs (slave_id, created DESC);
                """;
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception)
            {
                Application.Current.Shutdown(1);
                throw;
            }
           
            
        }
    }
}
