using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TempAndHumiTest.Common;

namespace TempAndHumiTest.Models
{
    public class UserModel
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        [Column("account")]
        public string Account { get; set; } = string.Empty;
        [Column("role")]
        public OperateRoleEnum Role { get; set; } = OperateRoleEnum.Operator;
        [Column("password")]
        public string Password {  get; set; } = "Aa@123";
        [Column("salt")]
        public string Salt { get; set; } = string.Empty;

        public int State { get; set; }


    }

    public class LoginUserModel
    {
        public string Account { set; get; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        public OperateRoleEnum Role { set; get; }
        public string Department { get; set; } = string.Empty;

        public int State { set; get; }

        public DateTime LastLoginTime;
    }
}
