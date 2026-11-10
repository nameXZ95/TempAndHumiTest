using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TempAndHumiTest.Common
{
    public sealed record PagedResult<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize=10)
        {
            public int TotalPages =>
                (int)Math.Ceiling((double)TotalCount / PageSize);
        }
}
