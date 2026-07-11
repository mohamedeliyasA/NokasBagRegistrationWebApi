using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.API.Configuration;

namespace Nokas.CashMgmt.BagRegistration.API.Services
{
    public class BlobArchiveService : IBlobArchiveService
    {
        private readonly BlobContainerClient _containerClient;

        public BlobArchiveService(IOptions<BlobStorageOptions> options)
        {
            var config = options.Value;
            _containerClient = new BlobContainerClient(config.ConnectionString, config.ContainerName);
            _containerClient.CreateIfNotExists();
        }

        public async Task ArchivePayloadAsync(string blobName, string content)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await blobClient.UploadAsync(stream, overwrite: true);
        }
    }
}
