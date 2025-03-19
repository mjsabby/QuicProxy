namespace QuicProxy
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal static class Program
    {
        public static async Task Main()
        {
            string? processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
            {
                Environment.FailFast("Failed to get process path.");
                return;
            }

            var fullName = Directory.GetParent(processPath)?.FullName;
            if (string.IsNullOrEmpty(fullName))
            {
                Environment.FailFast("Failed to get parent directory.");
                return;
            }

            var config = JsonSerializer.Deserialize(await File.ReadAllTextAsync(Path.Combine(fullName, "appsettings.json")).ConfigureAwait(false), ConfigJsonContext.Default.Config)!;

            HostResolver hostResolver = new();
            Dictionary<string, X509Certificate2> certificates = [];

            foreach (var e in config.Servers)
            {
                string hostName = e.HostName;
                ProtocolType protocolType = e.ProtocolType;
                QuicSettings? quicSettings = e.QuicSettings;

                if (protocolType == ProtocolType.Quic && quicSettings == null)
                {
                    Environment.FailFast($"ALPN is required for QUIC protocol. Please specify it in the configuration for {hostName}.");
                    return;
                }

                hostResolver.AddMapping(hostName, e.IPAddress, e.Port, protocolType, e.CertValidationSettings, quicSettings);
                certificates.Add(e.HostName, new X509Certificate2(e.CertPath));
            }

            List<SslApplicationProtocol> alpnList = [];
            foreach (var e in config.AlpnList)
            {
                alpnList.Add(new SslApplicationProtocol(e));
            }

            await using QuicProxyServer server = new(alpnList, hostResolver, (host) => certificates[host], new ConsoleLogger());
            await server.StartAsync(IPAddress.Parse(config.IPAddress), config.Port).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("Press Enter to stop the server...");
            Console.ReadLine();

            await server.StopAsync().ConfigureAwait(false);
        }
    }
}