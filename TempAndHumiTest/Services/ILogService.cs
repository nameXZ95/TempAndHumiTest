using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TempAndHumiTest.Models;

namespace TempAndHumiTest.Services
{
    public interface ILogService<T>
    {

        void Info(string message, params object?[] propertyValues);
        void Alarm(string message, params object?[] propertyValues);
        void Error(string message, Exception? ex = null, params object?[] propertyValues);
        
    }
}
