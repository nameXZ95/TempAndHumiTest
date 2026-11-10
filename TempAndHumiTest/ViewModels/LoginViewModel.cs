using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.ViewModels
{
    public partial class LoginViewModel:ObservableObject
    {
        [ObservableProperty]
        private bool isOpenLoginDialog;
        public IAuthService LoginService { get; set; }
        public ILogService<LoginViewModel> LogService { get; set; }

        public string Account { get; set; } = string.Empty;

        [ObservableProperty] private string password = string.Empty;

        public CancellationTokenSource cts { get; set; }


        public LoginViewModel(IAuthService authService, ILogService<LoginViewModel> logService)
        {
            LoginService = authService;
            LogService = logService;

           
        }

        /// <summary>
        /// 登陆逻辑的实现，如果登陆成功的话就会直接跳转到主页
        /// 这里如果有异常的话会在AuthService里面抛出来，所以如果在LoginService.UserLogin方法执行完成之后，那么就可以直接跳转到主页了;
        /// 这里修改了IsOpenLoginDialog = true，在隐藏代码里面会同步给DialogResult
        /// 详情见 <see cref="TempAndHumiTest.Views.LoginWindow.Login_PropertyChanged(object?, System.ComponentModel.PropertyChangedEventArgs)"/>
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public async Task LoginByUsername(string username, string password)
        {
            cts = new CancellationTokenSource();
            var token = cts.Token;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请输入账号或密码");
                return;
            }

            // 登陆的逻辑
            await LoginService.UserLogin(username, password, token);

            // 这里修改了IsOpenLoginDialog = true，在隐藏代码里面会同步给DialogResult
            IsOpenLoginDialog = true;
        }
    }
}
