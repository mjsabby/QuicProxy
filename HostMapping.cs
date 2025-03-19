namespace QuicProxy
{
    internal sealed class HostMapping
    {
        public required string HostName { get; set; } = string.Empty;

        public required string InternalServerIp { get; set; } = string.Empty;

        public required int InternalServerPort { get; set; }

        public required CertValidationSettings CertValidationSettings { get; set; }

        public QuicSettings? QuicSettings { get; set; }

        public required ProtocolType InternalServerProtocolType { get; set; }
    }
}