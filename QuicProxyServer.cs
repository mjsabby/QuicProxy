namespace QuicProxy
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Quic;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class QuicProxyServer(List<SslApplicationProtocol> alpnList, HostResolver hostResolver, Func<string, X509Certificate2> certResolver, ILogger logger) : IAsyncDisposable
    {
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        private readonly HostResolver _hostResolver = hostResolver ?? throw new ArgumentNullException(nameof(hostResolver));

        private readonly Func<string, X509Certificate2> _certResolver = certResolver ?? throw new ArgumentNullException(nameof(certResolver));

        private readonly CancellationTokenSource _cts = new();

        private QuicListener? _listener;

        private Task? _listenerTask;

        public async Task StartAsync(IPAddress ipAddress, int port)
        {
            if (_listener != null)
            {
                throw new InvalidOperationException("Server is already running.");
            }

            var options = new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(ipAddress, port),
                ApplicationProtocols = alpnList,
                ConnectionOptionsCallback = GetConnectionOptions
            };

            _listener = await QuicListener.ListenAsync(options, _cts.Token).ConfigureAwait(false);
            _logger.LogInfo($"QUIC proxy server listening on {ipAddress}:{port}");
            _listenerTask = AcceptConnectionsAsync(_listener, _hostResolver, _logger, _cts.Token);
        }

        public async Task StopAsync()
        {
            if (_listener == null || _listenerTask == null)
            {
                return;
            }

            await _cts.CancelAsync().ConfigureAwait(false);

            try
            {
                await _listenerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping server: {ex.Message}");
            }
            finally
            {
                await _listener.DisposeAsync().ConfigureAwait(false);
                _listener = null;
                _listenerTask = null;
            }

            _logger.LogInfo("QUIC proxy server stopped");
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _cts.Dispose();
        }

        private ValueTask<QuicServerConnectionOptions> GetConnectionOptions(QuicConnection connection, SslClientHelloInfo clientHelloInfo, CancellationToken cancellationToken)
        {
            string serverName = clientHelloInfo.ServerName;
            HostMapping hostMapping = _hostResolver.ResolveHostname(serverName)!;
            QuicSettings quicSettings = hostMapping.QuicSettings!;

            var options = new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = quicSettings.DefaultStreamErrorCode,
                DefaultCloseErrorCode = quicSettings.DefaultCloseErrorCode,
                HandshakeTimeout = TimeSpan.FromSeconds(quicSettings.HandshakeTimeoutInSeconds),
                IdleTimeout = TimeSpan.FromMinutes(quicSettings.IdleTimeoutInSeconds),
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [new SslApplicationProtocol(quicSettings.Alpn)],
                    ServerCertificate = _certResolver(serverName),
                    ClientCertificateRequired = true,
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => ClientCertValidationCallback(hostMapping, certificate, chain, sslPolicyErrors, _logger)
                }
            };

            return new ValueTask<QuicServerConnectionOptions>(options);
        }

        private static bool ClientCertValidationCallback(HostMapping hostMapping, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors, ILogger logger)
        {
            if (certificate == null || chain == null)
            {
                logger.LogError("Certificate or chain is null");
                return false;
            }

            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                logger.LogError($"SSL policy errors: {sslPolicyErrors}");
                return false;
            }

            X509Certificate2 rootCACertificate = chain.ChainElements[^1].Certificate;
            string rootCAThumbprint = rootCACertificate.Thumbprint;

            CertValidationSettings certValidationSettings = hostMapping.CertValidationSettings;

            if (rootCAThumbprint.Equals(certValidationSettings.RootCAThumbprint, StringComparison.Ordinal))
            {
                logger.LogInfo($"Client that possessed a cert that was chained to Root CA: {rootCAThumbprint} validated.");
                return true;
            }

            // check for entra tenant id and the group id in some IANA OID extension I find reasonable

            return false;
        }

        private static async Task AcceptConnectionsAsync(QuicListener listener, HostResolver hostResolver, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    QuicConnection connection = await listener.AcceptConnectionAsync(cancellationToken).ConfigureAwait(false);
                    logger.LogInfo($"Accepted connection from {connection.RemoteEndPoint} for {connection.TargetHostName}");

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ConnectionManager.HandleClientConnectionAsync(hostResolver, connection, logger, cancellationToken).ConfigureAwait(false);
                        }
                        finally
                        {
                            await connection.DisposeAsync().ConfigureAwait(false);
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.LogError($"Error accepting connections: {ex.Message}");
            }
        }
    }
}