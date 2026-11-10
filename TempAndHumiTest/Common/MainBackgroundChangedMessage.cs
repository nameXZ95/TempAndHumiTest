using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TempAndHumiTest.Models;

namespace TempAndHumiTest.Common
{
    // 定义一个传递背景刷子的消息
    public class MainBackgroundChangedMessage
    {
        //public Brush NewBackground { get; }
        public int SaveDataTime;

        public TempAndHumiLimtModel LimtModel;

        public UserModel User;
        public MainBackgroundChangedMessage(int time)
        {
            SaveDataTime = time;
        }

        public MainBackgroundChangedMessage(TempAndHumiLimtModel tempAndHumiLimtModel)
        {
            LimtModel = tempAndHumiLimtModel;
        }

        public MainBackgroundChangedMessage(UserModel userModel)
        {
            User = userModel;
        }
    }
}
