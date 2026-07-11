namespace Nokas.CashMgmt.BagRegistration.API.Models
{
    public class PayloadBlobItem
    {
        public string BlobName { get; }
        public string Content { get; }

        public PayloadBlobItem(string blobName, string content)
        {
            BlobName = blobName;
            Content = content;
        }
    }
}
