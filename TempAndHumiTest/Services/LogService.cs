using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TempAndHumiTest.Models;

namespace TempAndHumiTest.Services
{
    class LogService<T> : ILogService<T>
    {
        private readonly ILogger<T> _logger;

        public LogService(ILogger<T> logger) 
        {
            this._logger = logger;
        }
        
        public void Alarm(string message, params object?[] propertyValues)
        {
            
            _logger.LogWarning(message, propertyValues);
        }


        public void Error(string message, Exception? ex = null, params object?[] propertyValues)
        {
           
            _logger?.LogError(ex, message, propertyValues);
        }

        public void Info(string message, params object?[] propertyValues)
        {
            
            _logger.LogInformation(message, propertyValues);
        }

        private void Log(LogLevel level)
        {
            var log = new LogModel();
        }

    }
}
