using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace TempAndHumiTest.Models
{
    public class AppSettings
    {
        public SerialPortModel PortModel {  get; set; }

        public TempAndHumiLimtModel TempAndHumiLimtModel { get; set; }

        public AppSettingModel AppSettingModel { get; set; }

    }

    public class SerialPortModel
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; }

        public int DataBits { get; init; } = 8;
    }

    public class TempAndHumiLimtModel
    {
        public double TempMax {  get; set; } = 37.8;
        public double TempMin { get; set; } = 15.3;

        public double HumiMax { get; set; } = 64.9;

        public double HumiMin { get; set; } = 19.0;
    }

    public class AppSettingModel
    {
        public int CollectTime { get; set; } = 30;
        public int DataSaveTime { get; set; } = 5;
        public string Language { get; set; } = "中文";

        public Brush Background { get; set; } = (SolidColorBrush)new BrushConverter().ConvertFrom("#1A1E27")!; // 默认背景
    }
}
