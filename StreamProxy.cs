namespace QuicProxy
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Net.Quic;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class StreamProxy
    {
        private const int BufferSize = 8192;

        public static async Task ProxyQuicStreamsAsync(QuicStream clientStream, QuicStream serverStream, ILogger logger, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(clientStream, nameof(clientStream));
            ArgumentNullException.ThrowIfNull(serverStream, nameof(serverStream));

            var clientToServerTask = ProxyQuicStreamAsync(clientStream, serverStream, "Client -> Server", logger, cancellationToken);
            var serverToClientTask = ProxyQuicStreamAsync(serverStream, clientStream, "Server <- Client", logger, cancellationToken);

            await Task.WhenAll(clientToServerTask, serverToClientTask).ConfigureAwait(false);
        }

        public static async Task ProxyStreamAsync(Stream source, Stream destination, string direction, ILogger logger, CancellationToken cancellationToken)
        {
            ArrayPool<byte> arrayPool = ArrayPool<byte>.Shared;
            byte[]? buffer = null;
            try
            {
                buffer = arrayPool.Rent(BufferSize);
                int bytesRead;

                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    logger.LogInfo($"{direction}: Proxying {bytesRead} bytes");
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                logger.LogError($"{direction}: Stream closed");
            }
            catch (IOException ex)
            {
                logger.LogError($"{direction}: Error: {ex.Message}");
            }
            finally
            {
                if (buffer != null)
                {
                    arrayPool.Return(buffer);
                }
            }
        }

        private static async Task ProxyQuicStreamAsync(Stream source, QuicStream destination, string direction, ILogger logger, CancellationToken cancellationToken)
        {
            ArrayPool<byte> arrayPool = ArrayPool<byte>.Shared;
            byte[]? buffer = null;
            try
            {
                buffer = arrayPool.Rent(BufferSize);
                int bytesRead;

                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    logger.LogInfo($"{direction}: Proxying {bytesRead} bytes");
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                destination.CompleteWrites();
                logger.LogError($"{direction}: Stream closed");
            }
            catch (QuicException ex)
            {
                logger.LogError($"{direction}: QUIC error: {ex.Message}");
                try
                {
                    destination.CompleteWrites();
                }
                catch (QuicException)
                {
                }
            }
            catch (IOException ex)
            {
                logger.LogError($"{direction}: Error: {ex.Message}");
                try
                {
                    destination.CompleteWrites();
                }
                catch (QuicException)
                {
                }
            }
            finally
            {
                if (buffer != null)
                {
                    arrayPool.Return(buffer);
                }
            }
        }
    }
}