using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TempAndHumiTest.Services
{
    public class DialogService:IDialogService
    {
        public DialogResult ShowConfirm(string message, string title = "提示")
        {
            var result = MessageBox.Show(
            message, title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

            return result == MessageBoxResult.Yes
                ? DialogResult.Yes
                : DialogResult.No;
        }

        public void ShowError(string message, string title = "错误")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public void ShowInfo(string message, string title = "提示")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
