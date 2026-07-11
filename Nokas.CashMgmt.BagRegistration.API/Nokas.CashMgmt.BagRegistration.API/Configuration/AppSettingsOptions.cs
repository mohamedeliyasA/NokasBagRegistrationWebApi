namespace Nokas.CashMgmt.BagRegistration.API.Configuration
{
    public class AzureAdOptions
    {
        public string Authority { get; set; }
        public string Audience { get; set; }
        public string ValidIssuer { get; set; }
    }

    public class CosmosDbOptions
    {
        public string Account { get; set; }
        public string KeyValue { get; set; }
        public string DatabaseName { get; set; }
        public string BagRegContainerName { get; set; }
        public string ClientDirectoryContainerName { get; set; }
    }

    public class BlobStorageOptions
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
    }

    public class KeyVaultOptions
    {
        public string Uri { get; set; }
    }

    public class ServiceBusOptions
    {
        public string ConnectionString { get; set; }
        public string TopicName { get; set; }
    }
}
