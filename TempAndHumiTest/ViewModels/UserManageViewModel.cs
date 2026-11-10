using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TempAndHumiTest.Models;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.ViewModels
{
    public partial class UserManageViewModel : BaseViewModel
    {
        
        [ObservableProperty] private ObservableCollection<LoginUserModel> _loginUserModels = new();

        public ILogService<UserManageViewModel> LogService { get; set; }

        public UserManageViewModel(ILogService<UserManageViewModel> logService)
        {
            LogService = logService;
        }
    }
}