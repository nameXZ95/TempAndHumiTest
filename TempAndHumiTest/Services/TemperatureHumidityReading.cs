using System;

namespace TempAndHumiTest.Services
{
    /// <summary>一次成功采集的结果。</summary>
    /// <param name="Temperature">温度（℃），已按比例换算。</param>
    /// <param name="Humidity">湿度（%RH），已按比例换算。</param>
    /// <param name="Timestamp">读取成功的时刻。</param>
    public readonly record struct TemperatureHumidityReading(
        double Temperature,
        double Humidity,
        DateTimeOffset Timestamp);
}
