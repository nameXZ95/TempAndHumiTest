using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempAndHumiTest.Common;
using TempAndHumiTest.Models;

namespace TempAndHumiTest.ViewModels
{
    public partial class LogViewModel:BaseViewModel
    {

        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private bool _disposed;
        private readonly Dispatcher? _dispatcher;

        public IReadOnlyList<string> ItemNums { get; }
        public IReadOnlyList<string> AlarmLevels { get; }

        [ObservableProperty]
        private string startTime = string.Empty;

        [ObservableProperty]
        private string endTime = string.Empty;

        [ObservableProperty]
        private int totalCount;

        [ObservableProperty]
        private int currentPage = 1;

        [ObservableProperty]
        private int pageSize = 10;

        [ObservableProperty]
        private int totalPages;
        [ObservableProperty]
        private bool isCollection = true;
        [ObservableProperty]
        private bool isAlarm = false;
        [ObservableProperty]
        private bool isOperation = false;
        [ObservableProperty]
        private string level;
        [ObservableProperty]
        private string itemNum;

        private readonly IBusinessLogService _businessLogService;

        public ICommand QueryLogCommand { get; }

        public ICommand LastPageCommand { get; }

        public ICommand NextPageCommand { get; }

        public ICommand ResetQueryCommand { get; }

        public ICommand GoToPageCommand { get; }

        [ObservableProperty]
        private ObservableCollection<LogModel> logModels = new ObservableCollection<LogModel>();

        public LogViewModel(IBusinessLogService businessLogService)
        {
            this._businessLogService = businessLogService;
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            ItemNums = new[] { "01", "02" };
            AlarmLevels = ["严重", "警告", "正常"];
            //GetLogModelsAsync();
            QueryLogCommand = new RelayCommand(() =>
            {
                GetLogModelsAsync();
            });

            LastPageCommand = new RelayCommand(() => 
            {
                if (this.CurrentPage <= 1)
                {
                    return;
                }
                CurrentPage = this.CurrentPage - 1;
                GetLogModelsAsync();
            });

            NextPageCommand = new RelayCommand(() =>
            {
                if (this.CurrentPage >= this.totalPages)
                {
                    return;
                }

                CurrentPage = this.CurrentPage + 1;
                GetLogModelsAsync();
            });

            ResetQueryCommand = new RelayCommand(() =>
            {
                IsCollection = true;
                IsAlarm = false;
                IsOperation = false;
                Level = string.Empty;
                ItemNum = string.Empty;
                
                StartTime = string.Empty;
                EndTime = string.Empty;
                CurrentPage = 1;
                // 重置之后进行查询
                GetLogModelsAsync();
            });

            GoToPageCommand = new RelayCommand(ChangePage);
        }

        /// <summary>
        /// 跳转
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        private void ChangePage()
        {
            GetLogModelsAsync();
        }

        private async void GetLogModelsAsync()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            LogModels.Clear();
            await QueryTask(token);
        }

        /// <summary>
        /// 停止任务
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            var cts = _cts;
            var task = _pollTask;

            if (cts is null)
            {
                return;
            }

            
            cts.Cancel();

            if (task is not null)
            {
                try
                {
                    // 这里故意不 ConfigureAwait(false)：StopAsync 从 UI 命令进来，
                    // 后面的 UI 赋值需要回到 UI 线程。
                    await task;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
            }

            cts.Dispose();
            _cts = null;
            _pollTask = null;
        }
      
        private async Task QueryTask(CancellationToken token)
        {

            // 如果取消就抛异常，然后终止
            token.ThrowIfCancellationRequested();
            LogQuery query = new() 
            { 
                PageNumber = CurrentPage,
                PageSize = PageSize,
                StartTime = StartTime ?? string.Empty,
                EndTime = EndTime ?? string.Empty,
            };
            Debug.WriteLine($"选中的等级是:{Level}");
            PagedResult<LogModel> result = await _businessLogService.GetAllLogAsync(query, token);  
         
            // 修复 CS8602：_dispatcher 可能为 null，需判空
            if (_dispatcher is not null)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    var appendList = result.Items;
                    for (int i = 0; i < appendList.Count; i++)
                    {
                        var model = appendList[i];
                        model.RowNumber = i + 1;
                        LogModels.Add(model);
                    }
                    PageSize = query.PageSize;
                    TotalCount = result.TotalCount;
                    CurrentPage = result.PageNumber;
                    TotalPages = TotalCount <= 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;
                    while (LogModels.Count > 100) LogModels.RemoveAt(0);
                });
            }
        }

        /// <summary>
        /// Releases resources used by the instance and cancels any pending operations.
        /// </summary>
        /// <remarks>Call when the instance is no longer needed to ensure proper cleanup.</remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts?.Cancel();
        }
    }
}
