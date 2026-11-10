using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TempAndHumiTest.Common;
using TempAndHumiTest.Models;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.ViewModels
{
    public partial class SettingsViewModel:ObservableObject
    {
       
        [ObservableProperty]
        private bool isOpenDialog;

        private AppSettings _appSettings;

        public IReadOnlyList<string> LanguageList { get; }

        public IReadOnlyList<string> PortNameList { get; }

        public IReadOnlyList<int> BaudRateList { get; }

        public IRelayCommand SaveCommand { get; }

        public AppSettings AppSettings
        {
            get { return _appSettings; }
            set { _appSettings = value; }
        }

        public SettingsViewModel(AppSettings appSettings) 
        {
            _appSettings = appSettings;
            LanguageList = ["中文简体", "English" ,"中文繁体"];
            PortNameList = SerialPort.GetPortNames();
            BaudRateList = [2400, 4800, 9600, 19200, 38400, 57600, 115200];
            _appSettings.TempAndHumiLimtModel = new TempAndHumiLimtModel();
            _appSettings.AppSettingModel = new AppSettingModel();
            _appSettings.PortModel = new SerialPortModel();
            SaveCommand = new RelayCommand(OnSaveSettings);
        }

        private void OnSaveSettings()
        {
            var dataSaveTime = _appSettings.AppSettingModel.DataSaveTime;
            var limitModel = _appSettings.TempAndHumiLimtModel;
            WeakReferenceMessenger.Default.Send(new MainBackgroundChangedMessage(dataSaveTime));
            WeakReferenceMessenger.Default.Send(new MainBackgroundChangedMessage(limitModel));
            IsOpenDialog = true;
        }
    }


}
