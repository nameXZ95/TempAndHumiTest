using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NModbus.Device;
using TempAndHumiTest.Models;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.ViewModels
{
    public partial class MonitorViewModel : BaseViewModel
    {
        public MonitorModel Monitor { get; set; } = new MonitorModel();

        private CancellationTokenSource _cts = new CancellationTokenSource();

        private ModbusService modbusService;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
        private readonly Dispatcher _dispatcher;
        private Task? _loopTask;

        [ObservableProperty]
        private bool isStartMonitor = false;

        public ICommand OpenCommand { get; set; }

        public MonitorViewModel()
        {
            modbusService = new ModbusService("COM3");
            OpenCommand = new RelayCommand(() =>
            {
                if (this.IsStartMonitor)
                {
                    StartMonitor();
                }
                if (!this.IsStartMonitor)
                {
                    _ = StopMonitor();
                }
            });
        }

        /// <summary>停止监控，并等待循环安全退出</summary>
        public async Task StopMonitor()
        {
            if (_cts == null) return;

            _cts.Cancel();
            try
            {
                if (_loopTask != null) await _loopTask;
            }
            catch (OperationCanceledException) { /* 正常取消，忽略 */ }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _loopTask = null;
            }
        }

        private void StartMonitor()
        {
           
               _loopTask = Task.Run(
                    () =>
                    {
                        this.LoopAsync();
                        return Task.CompletedTask;
                    },
                    _cts.Token
                );
            
        }

        private async void LoopAsync(int intervalMs = 1000)
        {
            var token = _cts.Token;
            byte slaveId = 0x01;
            ushort startAddress = 1;
            ushort numberOfPoints = 2;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await modbusService
                           .ReadHoldingRegistersAsync(
                               slaveId,
                               startAddress,
                               numberOfPoints,
                               new CancellationToken()
                           )
                           .ConfigureAwait(false);

                    Debug.WriteLine($"寄存器值： {string.Join(",", result)}");
                    this.Monitor.Temperature = result[0] / 10.0;
                    this.Monitor.Humidity = result[1] / 10.0;

                }
                catch (OperationCanceledException)
                {
                    break; // 停止时主动取消，直接退出
                }
                catch (Exception ex)
                {
                    // 超时、CRC 校验失败、从站异常等都走这里
                    // 不要让异常把循环打断，否则点一次超时监控就停了
                    // ErrorOccurred?.Invoke(ex);
                }

                // 读取间隔：给传感器留出处理时间，也避免总线过于拥挤
                try
                {
                    await Task.Delay(intervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

            }
        }

        private void checkAndAnalysis(ushort[] result)
        {
            Debug.WriteLine($"寄存器值： {string.Join(",", result)}");
            this.Monitor.Temperature = result[0] / 10.0;
            this.Monitor.Humidity = result[1] / 10.0;
        }

        //private void CheckRes(ushort[] dataBytes)
        //{
        //    var length = dataBytes.Length - 2;
        //    ushort crc = 0xFFFF;
        //    for (int i = 0; i < length; i++) // 只对前 6 字节计算
        //    {
        //        crc ^= dataBytes[i];
        //        for (int j = 0; j < 8; j++)
        //        {
        //            if ((crc & 0x0001) != 0)
        //            {
        //                crc >>= 1;
        //                crc ^= 0xA001;
        //            }
        //            else
        //            {
        //                crc >>= 1;
        //            }
        //        }
        //    }

        //    return crc;
        //}
    }
}
