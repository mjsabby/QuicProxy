namespace QuicProxy
{
    using System.Text.Json.Serialization;

    internal sealed class QuicSettings
    {
        [JsonPropertyName("alpn")]
        public required string Alpn { get; set; }

        [JsonPropertyName("defaultCloseErrorCode")]
        public required uint DefaultCloseErrorCode { get; set; }

        [JsonPropertyName("defaultStreamErrorCode")]
        public required uint DefaultStreamErrorCode { get; set; }

        [JsonPropertyName("handshakeTimeoutInSeconds")]
        public required int HandshakeTimeoutInSeconds { get; set; }

        [JsonPropertyName("idleTimeoutInSeconds")]
        public required int IdleTimeoutInSeconds { get; set; }
    }
}