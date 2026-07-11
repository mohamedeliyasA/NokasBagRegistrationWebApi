namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public interface IBlobArchiveService
    {
        Task ArchivePayloadAsync(string blobName, string content);
    }

}
