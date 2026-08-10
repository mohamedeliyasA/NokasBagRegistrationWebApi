namespace Nokas.CashMgmt.Shared.StatusMapping;

public static class NattsafeStatusMapper
{
    public static string ToLabel(int? statusCode, DateTime? transportFetchedAt = null)
    {
        if (transportFetchedAt.HasValue && statusCode is 0 or 1 or 2)
        {
            return "picked-up";
        }

        return statusCode switch
        {
            0 or 1 => "registered",
            2 => "in-progress",
            3 => "picked-up",
            4 => "received by bank",
            5 => "counted",
            8 or 9 or -1 => "error",
            10 => "canceled",
            11 => "not approved",
            12 => "approved by bank",
            13 => "missing",
            _ => "unknown"
        };
    }
}
