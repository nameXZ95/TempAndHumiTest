using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Context;
using TempAndHumiTest.Common;
using TempAndHumiTest.Models;

namespace TempAndHumiTest.Services
{
    /// <summary>温湿度采集口：ViewModel 只依赖它，方便以后换协议或写单元测试。</summary>
    public interface ISensorReader : IDisposable
    {
        SensorOptions Options { get; }

        /// <summary>读取一个从站的温湿度。失败时抛异常，由调用方决定怎么展示。</summary>
        Task<TemperatureHumidityReading> ReadAsync(byte slaveId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 把「原始寄存器」翻译成「温度 / 湿度」，并保证一次请求就把两个值读回来。
    /// </summary>
    public sealed class SensorReader : ISensorReader
    {
        private readonly ModbusService _modbus;

        private readonly ILogService<SensorReader> _logService;

        const string name = nameof(SensorReader);

        public SensorReader(ModbusService modbus, SensorOptions options, ILogService<SensorReader> logService)
        {
            _modbus = modbus ?? throw new ArgumentNullException(nameof(modbus));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _logService = logService;
        }

        public SensorOptions Options { get; }

        public async Task<TemperatureHumidityReading> ReadAsync(byte slaveId, CancellationToken cancellationToken)
        {
            var first = Math.Min(Options.TemperatureRegister, Options.HumidityRegister);
            var last = Math.Max(Options.TemperatureRegister, Options.HumidityRegister);
            var count = (ushort)(last - first + 1);

            // 温湿度寄存器是相邻的，一次请求读回两个值：
            // 只花一次往返时间，总线占用也最小（原代码这里是对的，保留）。
            using var activity =
                 new Activity("Modbus.ReadRegisters").Start();
            var operationId = activity.SpanId.ToString();
            using (LogContext.PushProperty(
                "OperationId", operationId))
            {
                _logService.Info("开启读取温湿度数据{slaveId} 号从站", slaveId.ToString());
                var registers = await _modbus
                    .ReadHoldingRegistersAsync(slaveId, first, count, cancellationToken)
                    .ConfigureAwait(false);

                var temperature = Decode(registers[Options.TemperatureRegister - first], Options.TemperatureIsSigned);
                var humidity = Decode(registers[Options.HumidityRegister - first], Options.HumidityIsSigned);
                _logService.Info("读取温湿度成功");
                // 湿度不可能是负数。个别传感器把湿度也塞进有符号位，这里兜一下底。
                if (humidity < 0)
                {
                    humidity = 0;
                }
                
                return new TemperatureHumidityReading(temperature, humidity, DateTimeOffset.Now);
            }
                
        }

        /// <summary>
        /// 关键点：负温度必须按「有符号 16 位」解释。
        /// 否则 -5.0℃（寄存器 0xFFCE = 65486）会被算成 6548.6℃，仪表直接爆表。
        /// </summary>
        private double Decode(ushort raw, bool isSigned)
        {
            // 两边都显式提升到 int，否则 short 和 ushort 没有公共类型，编译不过。
            int value = isSigned ? (short)raw : raw;
            return value / Options.RegisterScale;
        }

        public void Dispose() => _modbus.Dispose();
    }
}
