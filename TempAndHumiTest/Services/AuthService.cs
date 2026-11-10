using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TempAndHumiTest.Common;
using TempAndHumiTest.CustomException;
using TempAndHumiTest.Data;
using TempAndHumiTest.Models;

namespace TempAndHumiTest.Services
{
    public interface IAuthService
    {
        Task UserLogin(string username, string password, CancellationToken token);

        Task UserAdd(string username, OperateRoleEnum role, string password, CancellationToken token);
    }

    public class AuthService : IAuthService
    {
       
        private readonly IAuthRepository authRepository;
        private ILogService<AuthService> _logService;

        public AuthService(IAuthRepository authRepository, ILogService<AuthService> logService)
        {
            this.authRepository = authRepository;
            _logService = logService;
        }

        public async Task UserAdd(string username, OperateRoleEnum role = OperateRoleEnum.Operator, string password = "Aa@123", CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(username) )
            {
                return;
            }
            await authRepository.AddUser(username, role, password, token);
           

            
        }

        public async Task UserLogin(string username, string password, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return;
            }
            UserModel? user = await authRepository.GetUser(username, token);
            if (user is null)
            {
                _logService.Error($"当前用户{username}不存在");
                throw new UserNotFoundException(username);
            }
            string salt = user.Salt;
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password, salt);
            
            if (user.Password != passwordHash)
            {
                _logService.Error($"用户名或密码错误,请重新输入");
                throw new PasswordErrorException();
            }

            _logService.Info("用户{username}登陆成功", username);

            // todo 这里需要插入日志，用户登陆的日志

            // 登陆成功之后发送消息给mainviewmodel，把登陆成功之后的用户信息传过去，在页面上进行修改
            WeakReferenceMessenger.Default.Send(new MainBackgroundChangedMessage(user));
           
        }
    }

    public interface IAuthRepository
    {
        Task AddUser(string username, OperateRoleEnum role, string password, CancellationToken token);
        Task<UserModel?> GetUser(string username, CancellationToken token);
    }

    public class AuthRepository : IAuthRepository
    {
        // 连接信息
        //private readonly string _connectionString = "Server=localhost;Database=temperature_humidity;Integrated Security=True;TrustServerCertificate=True;";
        private ILogService<AuthRepository> _logService;
        private readonly AppDbContext _context;

        public AuthRepository(ILogService<AuthRepository> logService, AppDbContext context)
        {
           _context = context;
            this._logService = logService;
        }

        public async Task<UserModel?> GetUser(string username, CancellationToken token)
        {
            //await using var connection = new SqlConnection(_connectionString);
            //await connection.OpenAsync(token);
            //var user = new UserModel();
            //await using var command = connection.CreateCommand();
            //string sql = "select * from user_manage where account = @username";
            //command.CommandText = sql;
            //command.Parameters.AddWithValue("username", username);
            //SqlDataReader result =  await command.ExecuteReaderAsync(token);
            //if (result.HasRows)
            //{

            //}
            //return user;
            return await _context.userModels.FirstOrDefaultAsync<UserModel>(u => u.Account == username, token);
        }

        public async Task AddUser(string username, OperateRoleEnum role, string password, CancellationToken token)
        {
            try
            {
                UserModel user = new UserModel();
                var salt = BCrypt.Net.BCrypt.GenerateSalt();
                user.Salt = salt;
                user.Account = username;
                user.Role = role;
                user.Password = BCrypt.Net.BCrypt.HashPassword(password, salt);

                await _context.AddAsync(user, token);
                _logService.Info("新增用户{username}成功", username);
            }
            catch (Exception ex)
            {
                _logService.Error("新增用户失败,异常信息：{message}", ex, ex.Message + ex.StackTrace);
                throw;
            }
           
        }
    }
}
