using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TempAndHumiTest.Common
{
    public static class PasswordBoxHelper
    {
        // 定义一个附加属性 BoundPassword
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

        public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
        public static void SetBoundPassword(DependencyObject d, string value) => d.SetCurrentValue(BoundPasswordProperty, value);

        // 只在「View → VM」这条路上置 true，避免回写时把控件里的光标/内容重置
        private static readonly DependencyProperty UpdatingProperty =
            DependencyProperty.RegisterAttached("Updating", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

        // 当 ViewModel 的值改变时，更新 PasswordBox
        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                // 防止死循环
                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

                if (!(bool)passwordBox.GetValue(IsUpdatingProperty))
                {
                    passwordBox.Password = (string)e.NewValue;
                }

                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        // 当 PasswordBox 的值改变时，更新 ViewModel
        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.SetValue(IsUpdatingProperty, true);
                SetBoundPassword(passwordBox, passwordBox.Password);
                passwordBox.SetValue(IsUpdatingProperty, false);
            }
        }

        // 内部标记，防止双向绑定死循环
        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(PasswordBoxHelper));
    }
}
