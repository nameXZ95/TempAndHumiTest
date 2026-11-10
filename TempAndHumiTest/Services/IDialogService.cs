using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempAndHumiTest.Services
{
    public interface IDialogService
    {
        DialogResult ShowConfirm(string message, string title = "提示");
        void ShowInfo(string message, string title = "提示");
        void ShowError(string message, string title = "错误");
    }
}
