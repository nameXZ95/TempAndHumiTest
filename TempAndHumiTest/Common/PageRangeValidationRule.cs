using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace TempAndHumiTest.Common
{
    public class PageRangeValidationRule : ValidationRule
    {
       
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            //value 是 Binding 的源值（这里是 CurrentPage，理论上已经是 int）
            // 但为了兜底字符串输入，这里也做一次转换判断
            if (value == null)
                return new ValidationResult(false, "页码不能为空");

            if (!int.TryParse(value.ToString(), out int page))
                return new ValidationResult(false, "请输入有效的数字");

            if (page < 1)
                return new ValidationResult(false, "页码不能小于 1");

            // 注意：这里的最大值需要动态获取，见下方"动态最大值"一节
            // 简单方案：通过一个附加属性或依赖属性传入 PageCount
            // 或者直接在 ViewModel 中通过 INotifyDataErrorInfo 校验
            return ValidationResult.ValidResult;
        }
    }
}
