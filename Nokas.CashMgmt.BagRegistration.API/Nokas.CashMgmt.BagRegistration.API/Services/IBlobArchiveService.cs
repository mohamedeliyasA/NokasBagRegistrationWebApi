namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public interface IBlobArchiveService
    {
        Task EnsureContainerExistsAsync(CancellationToken cancellationToken = default);

        Task ArchivePayloadAsync(string blobName, string content, CancellationToken cancellationToken = default);
    }

}
