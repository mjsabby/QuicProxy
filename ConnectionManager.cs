namespace QuicProxy
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Quic;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class ConnectionManager
    {
        public static async Task HandleClientConnectionAsync(HostResolver hostResolver, QuicConnection clientConnection, ILogger logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(clientConnection, nameof(clientConnection));
            string host = clientConnection.TargetHostName;

            logger.LogInfo($"Handling connection from {host}");

            var hostMapping = hostResolver.ResolveHostname(host);
            if (hostMapping == null)
            {
                logger.LogError($"No mapping found for hostname: {host}");
                return;
            }

            logger.LogInfo($"Resolved {host} to {hostMapping.InternalServerIp}:{hostMapping.InternalServerPort}");

            if (hostMapping.InternalServerProtocolType == ProtocolType.Quic)
            {
                try
                {
                    var serverConnection = await ConnectToInternalQuicServerAsync(hostMapping, logger, cancellationToken).ConfigureAwait(false);

                    try
                    {
                        await ProxyQuicConnectionStreamsAsync(clientConnection, serverConnection, logger, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        await serverConnection.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch (QuicException ex)
                {
                    logger.LogError($"Error handling client connection: {ex.Message}");
                }
                catch (IOException ex)
                {
                    logger.LogError($"Error handling client connection: {ex.Message}");
                }
                catch (Exception ex)
                {
                    logger.LogError($"Unexpected error handling client connection: {ex.Message}");
                }
            }
            else
            {
                using TcpClient tcpClient = await ConnectToInternalTcpServerAsync(hostMapping, logger, cancellationToken).ConfigureAwait(false);
                using var networkStream = tcpClient.GetStream();
                await ProxyQuicToTcpAsync(clientConnection, networkStream, logger, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<TcpClient> ConnectToInternalTcpServerAsync(HostMapping hostMapping, ILogger logger, CancellationToken cancellationToken)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(hostMapping.InternalServerIp, hostMapping.InternalServerPort, cancellationToken).ConfigureAwait(false);
            logger.LogInfo($"Connected to internal TCP server: {hostMapping.InternalServerIp}:{hostMapping.InternalServerPort}");
            return tcpClient;
        }

        private static async Task<QuicConnection> ConnectToInternalQuicServerAsync(HostMapping hostMapping, ILogger logger, CancellationToken cancellationToken)
        {
            QuicSettings quicSettings = hostMapping.QuicSettings!;
            var options = new QuicClientConnectionOptions
            {
                DefaultCloseErrorCode = quicSettings.DefaultCloseErrorCode,
                DefaultStreamErrorCode = quicSettings.DefaultStreamErrorCode,
                HandshakeTimeout = TimeSpan.FromSeconds(quicSettings.HandshakeTimeoutInSeconds),
                IdleTimeout = TimeSpan.FromMinutes(quicSettings.IdleTimeoutInSeconds),
                RemoteEndPoint = new IPEndPoint(IPAddress.Parse(hostMapping.InternalServerIp), hostMapping.InternalServerPort),
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true, // ignore internal server cert validation, since in same VNET
                    ApplicationProtocols = [new SslApplicationProtocol(hostMapping.QuicSettings!.Alpn)],
                    TargetHost = hostMapping.HostName
                }
            };

            var connection = await QuicConnection.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
            logger.LogInfo($"Connected to internal server: {hostMapping.InternalServerIp}:{hostMapping.InternalServerPort}");

            return connection;
        }

        private static async Task ProxyQuicToTcpAsync(QuicConnection clientConnection, NetworkStream networkStream, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                var clientStream = await clientConnection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInfo($"Accepted stream from client: Type={clientStream.Type}, ID={clientStream.Id}");

                var clientToServer = StreamProxy.ProxyStreamAsync(clientStream, networkStream, "Client -> Server", logger, cancellationToken);
                var serverToClient = StreamProxy.ProxyStreamAsync(networkStream, clientStream, "Server -> Client", logger, cancellationToken);

                await Task.WhenAll(clientToServer, serverToClient).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError($"Error proxying QUIC to TCP: {ex.Message}");
            }
        }

        private static async Task ProxyQuicConnectionStreamsAsync(QuicConnection clientConnection, QuicConnection serverConnection, ILogger logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(clientConnection, nameof(clientConnection));
            ArgumentNullException.ThrowIfNull(serverConnection, nameof(serverConnection));

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var clientStream = await clientConnection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);
                        logger.LogInfo($"Accepted stream from client: Type={clientStream.Type}, ID={clientStream.Id}");
                        _ = Task.Run(new Func<Task?>(() => HandleQuicStreamAsync(serverConnection, clientStream, logger, cancellationToken)), cancellationToken).ConfigureAwait(false);
                    }
                    catch (QuicException ex)
                    {
                        logger.LogError($"QUIC error accepting stream: {ex}");
                        break;
                    }
                    catch (IOException ex)
                    {
                        logger.LogError($"I/O error accepting stream: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Unexpected error accepting stream: {ex.Message}");
                    }
                }
            }
            catch (QuicException ex)
            {
                logger.LogError($"QUIC error during stream proxying: {ex.Message}");
            }
            catch (IOException ex)
            {
                logger.LogError($"I/O error during stream proxying: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error during stream proxying: {ex.Message}");
            }
        }

        private static async Task HandleQuicStreamAsync(QuicConnection serverConnection, QuicStream clientStream, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                var serverStream = await serverConnection.OpenOutboundStreamAsync(clientStream.Type, cancellationToken).ConfigureAwait(false);
                await StreamProxy.ProxyQuicStreamsAsync(clientStream, serverStream, logger, cancellationToken).ConfigureAwait(false);
            }
            catch (QuicException ex)
            {
                logger.LogError($"QUIC error handling stream: {ex.Message}");
            }
            catch (IOException ex)
            {
                logger.LogError($"I/O error handling stream: {ex.Message}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Unexpected error handling stream: {ex.Message}");
            }
        }
    }
}