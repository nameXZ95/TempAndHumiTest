using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using TempAndHumiTest.Models;
using TempAndHumiTest.ViewModels;
using TempAndHumiTest.Views;

namespace TempAndHumiTest.Components
{
    /// <summary>
    /// Settings.xaml 的交互逻辑
    /// </summary>
    public partial class Settings : Window
    {
        private readonly SettingsViewModel _vm;
        public Settings(SettingsViewModel settingsViewModel)
        {
            InitializeComponent();
            _vm = settingsViewModel;
            this.DataContext = settingsViewModel;
            settingsViewModel.PropertyChanged += OnVmPropertyChanged;
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 只有通过 ShowDialog 显示时才能设置 DialogResult
            if (e.PropertyName == nameof(SettingsViewModel.IsOpenDialog) && _vm.IsOpenDialog)
            {
                DialogResult = true;
            }

        }

        protected override void OnClosed(EventArgs e)
        {
            // 取消订阅
            _vm.PropertyChanged -= OnVmPropertyChanged;
            base.OnClosed(e);
        }

        // 1. 实现窗口拖拽（按住头部任意位置可以移动窗口）
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

    }
    // 控制弹窗的开关就是控制window的DialogResult，如果DialogResult是false就是关闭
    // 使用window的ShowDialog就会开窗口


}
