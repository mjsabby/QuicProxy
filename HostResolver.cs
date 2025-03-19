namespace QuicProxy
{
    using System.Collections.Generic;

    internal sealed class HostResolver
    {
        private readonly Dictionary<string, HostMapping> _hostMappings = [];

        public void AddMapping(string hostname, string internalServerIp, int internalServerPort, ProtocolType protocolType, CertValidationSettings certValidationSettings, QuicSettings? quicSettings)
        {
            var mapping = new HostMapping
            {
                HostName = hostname,
                InternalServerIp = internalServerIp,
                InternalServerPort = internalServerPort,
                InternalServerProtocolType = protocolType,
                CertValidationSettings = certValidationSettings,
                QuicSettings = quicSettings
            };

            _hostMappings[hostname] = mapping;
        }

        public HostMapping? ResolveHostname(string hostname)
        {
            return _hostMappings.TryGetValue(hostname, out var mapping) ? mapping : null;
        }
    }
}