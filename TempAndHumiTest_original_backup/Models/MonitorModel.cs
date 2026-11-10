using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TempAndHumiTest.Models
{
    public partial class MonitorModel : INotifyPropertyChanged
    {
        public Brush warmColor;

        public Brush WarmColor
        {
            get { return warmColor; }
            set
            {
                warmColor = value;
                OnPropertyChanged();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string warmIcon;
        public string WarmIcon
        {
            get { return warmIcon; }
            set
            {
                warmIcon = value;
                OnPropertyChanged();
            }
        }

        public string warmMessage;
        public string WarmMessage
        {
            get { return warmMessage; }
            set
            {
                warmMessage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MonitorModel()
        {
            WarmColor = Brushes.SkyBlue;
            WarmIcon = "\ue627";
            WarmMessage = "一切正常2";
        }

        private double temperature = 20.5;

        public double Temperature
        {
            get { return temperature; }
            set { temperature = value; OnPropertyChanged(); }
        }

        private double humidity = 45.0f;

        public double Humidity
        {
            get { return humidity; }
            set { humidity = value; OnPropertyChanged(); }
        }

        
    }
}
