using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NModbus;
using NModbus.Serial;

namespace TempAndHumiTest.Services
{
    public class ModbusService
    {
        private SerialPort _serialPort;
        private IModbusSerialMaster? _master;
        // 保护 _port / _master 的引用切换，而不是保护整个 IO 过程
        private readonly object _resourceLock = new();

        private readonly string _portName;
        private readonly int _baudRate;
        private readonly Parity _parity;
        private readonly int _dataBits;
        private readonly StopBits _stopBits;
        private readonly int _readTimeoutMs;

        /// <summary>每次成功读到数据时触发（注意：在后台线程触发）</summary>
        public event Action<double, double>? DataReceived;
        /// <summary>读取出现异常时触发（超时、CRC 错误等）</summary>
        public event Action<Exception>? ErrorOccurred;

  
        public ModbusService(string portName,int baudRate = 9600,
            Parity parity = Parity.None,
            int dataBits = 8,
            StopBits stopBits = StopBits.One,
            int readTimeoutMs = 1500)
        {
            _portName = portName;
            _baudRate = baudRate;
            _parity = parity;
            _dataBits = dataBits;
            _stopBits = stopBits;
            _readTimeoutMs = readTimeoutMs;

        }

        /// <summary>
        /// 获取或者创建rtu
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        private Task<IModbusSerialMaster> GetOrCreateMasterAsync(CancellationToken token)
        {
            if (_master is not null)
            {
                return Task.FromResult(_master);
            }
            lock (_resourceLock)
            {
                var port = new SerialPort(_portName)
                {
                    BaudRate = _baudRate,
                    Parity = _parity,
                    DataBits = _dataBits,
                    StopBits = _stopBits,
                    ReadTimeout = _readTimeoutMs,
                    WriteTimeout = _readTimeoutMs
                };
                try
                {
                    
                    port.Open();
                }
                catch
                {
                    port.Dispose();
                    throw;
                }
                var factory = new ModbusFactory();
                var master = factory.CreateRtuMaster(port);

                _serialPort = port;
                _master = master;

                return Task.FromResult<IModbusSerialMaster>(master);
            }

        }

        /// <summary>
        /// 读取寄存器
        /// </summary>
        /// <param name="slaveId"></param>
        /// <param name="startAddress"></param>
        /// <param name="numberOfPoints"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId,
        ushort startAddress,
        ushort numberOfPoints, CancellationToken token) 
        {
            if (_master is null)
            {
               _master = await GetOrCreateMasterAsync(token).ConfigureAwait(false);
            }
            // 重试次数
            const int maxAttempts = 3;
            Exception? lastError = null;
            for (int i = 0; i < maxAttempts; i++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                   var master = await GetOrCreateMasterAsync(token).ConfigureAwait(false);

                    return await master.ReadHoldingRegistersAsync(slaveAddress: slaveId, startAddress: startAddress, numberOfPoints: numberOfPoints)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    
                    throw;
                }
                catch (SlaveException ex)
                {
                    // 协议级异常：从站明确说“地址非法/功能不支持”，重试无意义
                    throw new InvalidOperationException(
                        $"从站 {slaveId} 返回 Modbus 异常，" +
                        $"功能码 {ex.FunctionCode}，异常码 {ex.SlaveExceptionCode}",
                        ex);
                }
                catch (Exception ex) when (i < maxAttempts)
                {
                    lastError = ex;
                    // 串口通信失败：先彻底释放串口，退避后重建
                    ResetPort();
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * i),
                            token)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    ResetPort();
                }

            }

            throw new InvalidOperationException(
                     $"读取失败，已尝试 {maxAttempts} 次", lastError);
        }

        private void ResetPort()
        {
            lock (_resourceLock)
            {
                _master = null;

                var port = _serialPort;
                _serialPort = null;

                if (port is null) return;

                // 顺序很重要：先 Close，再 Dispose
                // 只 Dispose 不 Close 在某些 USB 转串口驱动下会留下句柄
                try { port.Close(); } catch { /* 忽略关闭异常 */ }
                try { port.Dispose(); } catch { /* 忽略释放异常 */ }
            }
        }

    }
}
