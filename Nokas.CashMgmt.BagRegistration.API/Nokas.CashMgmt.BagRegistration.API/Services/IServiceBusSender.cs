namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public interface IServiceBusSender
    {
        Task SendMessageAsync(string messageBody, string subject, string? messageId = null, CancellationToken cancellationToken = default);
    }
}
