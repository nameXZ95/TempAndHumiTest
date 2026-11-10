using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TempAndHumiTest.Common;

namespace TempAndHumiTest.Models
{
    public class LogModel
    {
        // 序号
        public int RowNumber { get; set; }
        public long Id { get; set; }
        public DateTime Created { get; set; }
        public string SlaveId { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public LogLevel LogLevel { get; set; }
        public int State { get; set; }
        public string Remark { get; set; } = string.Empty;

    
    }

    public class LogQuery
    {
        public long Id { get; set; }
        public string SlaveId { get; set; } = string.Empty;
        public LogLevel LogLevel { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
