using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempAndHumiTest.CustomException
{
    public class MyException
    {
    }

    /// <summary>
    /// 这个和Java里面的枚举不一样，并不能添加参数，这里的枚举实际上就是常量
    /// </summary>
    //public enum ErrorBody
    //{
    //    UserNotFound(10086, $"用户名{username}所属用户不存在", username);

        
    //}

    // 基础业务异常
    public class BusinessException : Exception
    {
        public BusinessException(string message) : base(message) { }
        public BusinessException(string message, Exception inner) : base(message, inner) { }
    }

    // 具体业务异常
    public class UserNotFoundException : BusinessException
    {
        public UserNotFoundException(string username) : base($"用户 {username} 不存在") { }
    }

    public class DuplicateUsernameException : BusinessException
    {
        public DuplicateUsernameException(string username) : base($"用户名 {username} 已存在") { }
    }

    public class PasswordErrorException : BusinessException
    {
        public PasswordErrorException() : base("用户名或密码错误，请重新输入") { }
    }

    
}
