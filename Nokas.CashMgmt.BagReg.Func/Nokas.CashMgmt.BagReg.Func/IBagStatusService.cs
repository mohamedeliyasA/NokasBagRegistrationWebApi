using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nokas.CashMgmt.BagReg.Func
{
    public interface IBagStatusService
    {
        Task<BagStatusRecord?> GetStatusAsync(string bagNumber, CancellationToken ct = default);
        Task<BagCountingResultRecord?> GetCountingResultAsync(string bagNumber, CancellationToken ct = default);
        Task<bool> ValidateBagNumber(string bagNumber, CancellationToken ct);

    }

}
