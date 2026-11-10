using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using NModbus;
using NModbus.Serial;

namespace TempAndHumiTest.Services
{
    /// <summary>
    /// Modbus RTU 主站。只负责「把一串寄存器读回来」，并且把三件事收口在这里：
    /// <list type="number">
    /// <item>串口是独占、非线程安全的资源，所有 IO 用信号量串行化，避免两条请求在总线上交叉；</item>
    /// <item>出错后彻底关掉串口再重建（USB 转 485 出错后句柄常常是脏的，复用只会连环失败）；</item>
    /// <item>重试策略统一在这里，最后一次失败后不再做无意义的等待。</item>
    /// </list>
    /// </summary>
    public sealed class ModbusService : IDisposable
    {
        private readonly SensorOptions _options;

        /// <summary>串口同一时刻只能有一个请求：并发读会把总线和接收缓冲区搅乱。</summary>
        private readonly SemaphoreSlim _ioLock = new(1, 1);

        /// <summary>只保护 _port / _master 的引用切换，不覆盖整个 IO 过程。</summary>
        private readonly object _resourceLock = new();

        private SerialPort? _port;
        private IModbusSerialMaster? _master;
        private bool _disposed;

        public ModbusService(SensorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>串口当前是否已打开（仅用于状态展示，不要拿它做同步判断）。</summary>
        public bool IsOpen
        {
            get
            {
                lock (_resourceLock)
                {
                    return _master is not null;
                }
            }
        }

        /// <summary>
        /// 读取保持寄存器。含重试；失败时抛出 <see cref="InvalidOperationException"/>。
        /// </summary>
        public async Task<ushort[]> ReadHoldingRegistersAsync(
            byte slaveId,
            ushort startAddress,
            ushort numberOfPoints,
            CancellationToken cancellationToken)
        {
            if (numberOfPoints == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numberOfPoints), "至少需要读取 1 个寄存器。");
            }

            ObjectDisposedException.ThrowIf(_disposed, this);

            await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var maxAttempts = _options.RetryCount + 1;
                Exception? lastError = null;

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    // 每次尝试前都检查取消：停止监控时不用等完所有重试。
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var master = GetOrCreateMaster();

                        return await master
                            .ReadHoldingRegistersAsync(slaveId, startAddress, numberOfPoints)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (SlaveException ex)
                    {
                        // 从站给出了明确的异常码（地址非法、功能不支持……），重试毫无意义，直接上抛。
                        throw new InvalidOperationException(
                            $"从站 {slaveId} 返回 Modbus 异常：功能码 {ex.FunctionCode}，异常码 {ex.SlaveExceptionCode}。" +
                            "请核对寄存器地址是否在从站支持范围内。", ex);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;

                        // 任何一次通信异常之后都重建串口：残留的半包会让后续请求一直错位。
                        ResetPort();

                        if (attempt == maxAttempts)
                        {
                            break; // 最后一次失败不再等待，直接结束
                        }

                        // 线性退避，给从站 / 驱动一点恢复时间。
                        await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                throw new InvalidOperationException(
                    $"读取从站 {slaveId} 寄存器失败（已尝试 {maxAttempts} 次）;{lastError?.Message}",
                    lastError);
            }
            finally
            {
                _ioLock.Release();
            }
        }

        /// <summary>
        /// 创建串口和RtuMaster
        /// </summary>
        /// <returns></returns>
        private IModbusSerialMaster GetOrCreateMaster()
        {
            lock (_resourceLock)
            {
                if (_master is { } existing)
                {
                    return existing;
                }

                var port = new SerialPort(
                    _options.PortName,
                    _options.BaudRate,
                    _options.Parity,
                    _options.DataBits,
                    _options.StopBits)
                {
                    ReadTimeout = _options.IoTimeoutMs,
                    WriteTimeout = _options.IoTimeoutMs,

                    // 传感器是两线 RS485，不需要流控；显式关掉，避免个别驱动默认打开。
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false,
                };

                try
                {
                    port.Open();

                    var master = new ModbusFactory().CreateRtuMaster(port);

                    // 重试由本类统一控制；让 NModbus 再重试一次会把超时成倍叠加，最坏情况卡到十几秒。
                    master.Transport.Retries = 0;
                    master.Transport.ReadTimeout = _options.IoTimeoutMs;
                    master.Transport.WriteTimeout = _options.IoTimeoutMs;

                    _port = port;
                    _master = master;
                    return master;
                }
                catch
                {
                    ClosePortQuietly(port);
                    throw;
                }
            }
        }

       /// <summary>
       /// 关闭NModbus，先关串口，再关NModbus
       /// </summary>
        private void ResetPort()
        {
            lock (_resourceLock)
            {
                // 先赋值给变量
                var master = _master;
                var port = _port;

                // 置为空，这样的好处是防止多线程的时候引用被换掉，其他线程可能会 master= new()
                // 还有就是防止重复关闭，好处多多
                _master = null;
                _port = null;

                // 顺序很重要：先释放 NModbus 传输层，再关物理串口。
                // 只关不释放（或反过来）在部分 USB 转 485 驱动上会留下句柄，重连时报「拒绝访问」。
                if (master is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        // 关闭时的异常不影响重连，忽略。
                    }
                }

                ClosePortQuietly(port);
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        /// <param name="port"></param>
        private static void ClosePortQuietly(SerialPort? port)
        {
            if (port is null)
            {
                return;
            }

            try
            {
                if (port.IsOpen)
                {
                    port.Close();
                }
            }
            catch
            {
                // 忽略
            }

            try
            {
                port.Dispose();
            }
            catch
            {
                // 忽略
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // 如果此刻正好有一次读卡在串口上，最多等它自己超时退出。
            // 等不到就不硬关：从别的线程硬关正在读的 SerialPort 有可能死锁，
            // 而进程马上要结束，交给操作系统回收句柄更安全。
            if (_ioLock.Wait(TimeSpan.FromMilliseconds(_options.IoTimeoutMs)))
            {
                try
                {
                    ResetPort();
                }
                finally
                {
                    _ioLock.Release();
                }
            }

            // 故意不 Dispose _ioLock：可能还有等待者，Dispose 会抛 ObjectDisposedException。
        }
    }
}
