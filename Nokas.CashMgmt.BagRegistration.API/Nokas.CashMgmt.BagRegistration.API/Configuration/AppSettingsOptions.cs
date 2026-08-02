using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.API.Configuration
{
    public class AzureAdOptions
    {
        [Required]
        public string Authority { get; set; }

        [Required]
        public string Audience { get; set; }

        [Required]
        public string ValidIssuer { get; set; }
    }

    public class CosmosDbOptions
    {
        [Required]
        public string Account { get; set; }

        [Required]
        public string KeyValue { get; set; }

        [Required]
        public string DatabaseName { get; set; }

        [Required]
        public string BagRegContainerName { get; set; }

        public string ClientDirectoryContainerName { get; set; }
    }

    public class BlobStorageOptions
    {
        [Required]
        public string ConnectionString { get; set; }

        [Required]
        public string ContainerName { get; set; }
    }

    public class KeyVaultOptions
    {
        // Optional: Key Vault is skipped entirely when not configured (see Program.cs).
        public string Uri { get; set; }
    }

    public class ServiceBusOptions
    {
        [Required]
        public string ConnectionString { get; set; }

        [Required]
        public string TopicName { get; set; }
    }

    public class BagStatusFunctionOptions
    {
        [Required]
        public string BaseUrl { get; set; }

        [Required]
        public string FunctionKey { get; set; }
    }
}
