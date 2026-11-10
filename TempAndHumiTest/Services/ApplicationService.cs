using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempAndHumiTest.Services
{
    public class ApplicationService : IApplicationService
    {

      
        /// <summary>
        /// 页面退出的逻辑
        /// </summary>
        public void Shutdown()
        {
            System.Windows.Application.Current?.Shutdown();
        }
    }
}
