namespace QuicProxy
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal sealed class Config
    {
        [JsonPropertyName("alpnList")]
        public required List<string> AlpnList { get; set; }

        [JsonPropertyName("ip")]
        public required string IPAddress { get; set; }

        [JsonPropertyName("port")]
        public required int Port { get; set; }

        [JsonPropertyName("servers")]
        public required List<ServerConfig> Servers { get; set; }
    }

    internal sealed class ServerConfig
    {
        [JsonPropertyName("hostName")]
        public required string HostName { get; set; }

        [JsonPropertyName("ip")]
        public required string IPAddress { get; set; }

        [JsonPropertyName("port")]
        public required int Port { get; set; }

        [JsonPropertyName("certPath")]
        public required string CertPath { get; set; }

        [JsonPropertyName("protocolType")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public required ProtocolType ProtocolType { get; set; }

        [JsonPropertyName("certValidationSettings")]
        public required CertValidationSettings CertValidationSettings { get; set; }

        [JsonPropertyName("quicSettings")]
        public QuicSettings? QuicSettings { get; set; }
    }

    [JsonSerializable(typeof(Config))]
    internal sealed partial class ConfigJsonContext : JsonSerializerContext
    {
    }
}