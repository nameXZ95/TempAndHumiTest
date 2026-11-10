using System.Collections.Generic;
using System.IO.Ports;

namespace TempAndHumiTest.Services
{
    /// <summary>
    /// 采集参数集中管理：串口、从站地址、寄存器映射、轮询周期、报警阈值。
    /// 换传感器或换串口时只改这一个文件，不用在 ViewModel 里到处翻魔法数字。
    /// </summary>
    public sealed class SensorOptions
    {
        // ---------------- 串口 ----------------

        /// <summary>串口名，例如 "COM3"。</summary>
        public string PortName { get; init; } = "COM3";

        public int BaudRate { get; init; } = 9600;
        public Parity Parity { get; init; } = Parity.None;
        public int DataBits { get; init; } = 8;
        public StopBits StopBits { get; init; } = StopBits.One;

        /// <summary>
        /// 单次读写超时（毫秒）。9600bps 读 2 个寄存器不到 10ms，800ms 已经很宽松了。
        /// 这里调太大会让「停止监控」和界面刷新都变得迟钝——因为一次读卡住时循环也在卡住。
        /// </summary>
        public int IoTimeoutMs { get; init; } = 800;

        /// <summary>
        /// 失败后的重试次数（不含首次）。最坏卡顿 ≈ 重试次数 × 超时，别设太大。
        /// </summary>
        public int RetryCount { get; init; } = 1;

        // ---------------- 轮询 ----------------

        /// <summary>轮询周期（毫秒）：两次采集「开始时间」的间隔，不包含采集本身的耗时。</summary>
        public int PollIntervalMs { get; init; } = 1000;

        /// <summary>从站地址列表，顺序决定界面卡片的顺序。</summary>
        public IReadOnlyList<byte> SlaveIds { get; init; } = new byte[] { 1, 2 };

        // ---------------- 寄存器映射 ----------------

        /// <summary>
        /// 温度寄存器地址，<b>0 基</b>。NModbus 内部使用 0 基地址：
        /// 手册里写 40002 或 0x0001 的，这里填 0x0001；写 40001 / 0x0000 的填 0x0000。
        /// 读不到数据时优先怀疑这里差 1。
        /// </summary>
        public ushort TemperatureRegister { get; init; } = 0x0001;

        /// <summary>湿度寄存器地址，0 基。</summary>
        public ushort HumidityRegister { get; init; } = 0x0002;

        /// <summary>寄存器原始值 ÷ 该系数 = 实际物理量。常见温湿度传感器是 10（即 0.1℃ / 0.1%RH）。</summary>
        public double RegisterScale { get; init; } = 10.0;

        /// <summary>
        /// 温度是否按有符号 16 位解析。绝大多数温湿度传感器都是，
        /// 否则 -5.0℃（寄存器 0xFFCE）会被算成 6553.6℃。
        /// </summary>
        public bool TemperatureIsSigned { get; init; } = true;

        /// <summary>湿度是否按有符号 16 位解析。湿度不会为负，一般填 false。</summary>
        public bool HumidityIsSigned { get; init; } = false;

        // ---------------- 报警阈值 ----------------

        public double TemperatureLow { get; init; } = 10.0;
        public double TemperatureHigh { get; init; } = 35.0;
        public double HumidityLow { get; init; } = 20.0;
        public double HumidityHigh { get; init; } = 80.0;
    }
}
