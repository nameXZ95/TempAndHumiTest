using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TempAndHumiTest.ViewModels;

namespace TempAndHumiTest.Views
{
    /// <summary>
    /// LogView.xaml 的交互逻辑
    /// </summary>
    public partial class LogView : UserControl
    {
        public LogView()
        {
            InitializeComponent();
        }


        /// <summary>
        /// 如果输入其他的字符就不会录入
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void PageNumberBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许输入数字字符
            e.Handled = !e.Text.All(char.IsDigit);
        }

        /// <summary>
        /// 拦截
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PageNumberBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            // 拦截粘贴：粘贴内容必须全部是数字
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (!text.All(char.IsDigit))
                    e.CancelCommand();
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void PageNumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitPageInput();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // 按 Esc 取消编辑，恢复当前页
                var box = (TextBox)sender;
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                e.Handled = true;
            }
        }

        private void PageNumberBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitPageInput();
        }

        private void CommitPageInput()
        {
            
            var box = PageNumberBox;

            // 空值：恢复为当前页
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                return;
            }

            // 先校验数字
            if (!int.TryParse(box.Text, out int page))
            {
                // 非法字符（理论上被 PreviewTextInput 拦截了，这里兜底）
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                return;
            }

            var vm = (LogViewModel)DataContext;

            // 范围修正：超出边界时自动夹到合法范围，或提示后回退
            if (page < 1)
            {
                page = 1;
            }
            else if (page > vm.TotalPages)
            {
                page = vm.TotalPages;
                // 也可以在这里给用户一个 MessageBox / Toast 提示"已跳转到最后一页"
            }

            // 把修正后的值写回绑定源
            box.Text = page.ToString();
            var expr = box.GetBindingExpression(TextBox.TextProperty);
            expr?.UpdateSource();

            // 若源值变化，触发加载该页数据
            if (vm.CurrentPage != page)
            {
                vm.CurrentPage = page;
                // 调用异步加载：
                // await vm.LoadPageAsync(page);
            }
        }

      
    }
}
