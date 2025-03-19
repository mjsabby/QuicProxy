namespace QuicProxy
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal sealed class CertValidationSettings
    {
        [JsonPropertyName("rootCAThumbprint")]
        public required string RootCAThumbprint { get; set; }

        [JsonPropertyName("entraTenantId")]
        public required string EntraTenantId { get; set; }

        [JsonPropertyName("entraGroupIds")]
        public required HashSet<string> EntraGroupIds { get; set; }
    }
}