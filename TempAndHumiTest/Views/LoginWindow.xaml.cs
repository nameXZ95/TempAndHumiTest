using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using Serilog;
using TempAndHumiTest.ViewModels;

namespace TempAndHumiTest.Views
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {

        private readonly LoginViewModel _vm;
        public LoginWindow(LoginViewModel login)
        {
            InitializeComponent();
            _vm = login;
            DataContext = login;
            login.PropertyChanged += Login_PropertyChanged;
        }

        /// <summary>
        /// 当属性发生改变的时候就会调用这个方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Login_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当属性名是IsOpenLoginDialog并且属性的值是true的时候才会触发，因为登陆成功之后会把IsOpenLoginDialog改成true
            if (e.PropertyName == nameof(LoginViewModel.IsOpenLoginDialog) && _vm.IsOpenLoginDialog)
            {
                this.DialogResult = true;
            }
        }

        // 1. 实现窗口拖拽（按住头部任意位置可以移动窗口）
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// 因为PasswordBox的password属性是不能够双向绑定的，所以直接在隐藏代码里面修改，桥接一下。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async Task LoginClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this.PwdBox.Password) || string.IsNullOrEmpty(this.UserBox.Text))
            {
                MessageBox.Show("请输入正确的账户名和密码");
                return;
            }

            // 因为是直接绑定的LoginViewModel，所以可以直接调用里面的方法，注意要把里面的方法改成public的
            if (DataContext is LoginViewModel login)
            {
              await login.LoginByUsername(UserBox.Text, this.PwdBox.Password);
            }
            
        }

        /// <summary>
        /// 监听enter键
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async Task EnterKeyDownEvent(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (string.IsNullOrEmpty(this.PwdBox.Password) || string.IsNullOrEmpty(this.UserBox.Text))
                {
                    MessageBox.Show("请输入正确的账户名和密码");
                    return;
                }
                if (DataContext is LoginViewModel login)
                {
                    await login.LoginByUsername(UserBox.Text, this.PwdBox.Password);
                }
            }
        }
    }
}
