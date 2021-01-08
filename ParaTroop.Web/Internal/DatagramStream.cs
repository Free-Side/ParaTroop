using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ParaTroop.Web.Internal {
    public class DatagramStream : Stream {
        private static readonly IPEndPoint AnyEndpoint =
            new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);

        private readonly UdpClient client;

        // Unconsumed data from previous read
        private Byte[] currentBuffer;
        private Int32 start;

        public override Boolean CanRead => true;
        public override Boolean CanSeek => false;
        public override Boolean CanWrite => false;
        public override Int64 Length => -1;

        public override Int64 Position {
            get => -1;
            set => throw new NotSupportedException();
        }

        public DatagramStream(UdpClient client) {
            this.client = client;
        }

        public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count) {
            if (this.currentBuffer == null) {
                // There's no security on this thing, the client side ports should not be open to the public internet.
                var ipEndpoint = AnyEndpoint;
                this.currentBuffer = this.client.Receive(ref ipEndpoint);
                this.start = 0;
            }

            return ReadFromCurrentBuffer(buffer, offset, count);
        }

        private int ReadFromCurrentBuffer(Byte[] buffer, Int32 offset, Int32 count) {
            if (this.currentBuffer == null) {
                throw new InvalidOperationException(
                    "Unable to read data from uninitialized buffer."
                );
            }

            var bufferBytes = this.currentBuffer.Length - this.start;
            var readBytes = Math.Min(count, bufferBytes);
            Array.Copy(this.currentBuffer, start, buffer, offset, readBytes);
            if (readBytes < bufferBytes) {
                this.start += readBytes;
            } else {
                this.currentBuffer = null;
            }

            return readBytes;
        }

        public override async Task<Int32> ReadAsync(
            Byte[] buffer,
            Int32 offset,
            Int32 count,
            CancellationToken cancellationToken) {

            if (this.currentBuffer == null) {
                await GetBuffer(cancellationToken);
            }

            return ReadFromCurrentBuffer(buffer, offset, count);
        }

        private async Task GetBuffer(CancellationToken cancellationToken) {
            // There's no security on this thing, the client side ports should not be open to the public internet.
            var received = await TaskExtensions.WhenAny(
                this.client.ReceiveAsync(),
                Task.Delay(TimeSpan.MaxValue, cancellationToken).Then(() => default(UdpReceiveResult))
            );

            cancellationToken.ThrowIfCancellationRequested();

            this.currentBuffer = received.Buffer;
            this.start = 0;
        }

        public override async ValueTask<Int32> ReadAsync(
            Memory<Byte> buffer,
            CancellationToken cancellationToken = default) {

            if (this.currentBuffer == null) {
                await this.GetBuffer(cancellationToken);
            }

            var bufferBytes = this.currentBuffer.Length - this.start;
            var readBytes = Math.Min(buffer.Length, bufferBytes);
            new Span<Byte>(this.currentBuffer, start, bufferBytes)
                .CopyTo(buffer.Span);
            if (readBytes < bufferBytes) {
                this.start += readBytes;
            } else {
                this.currentBuffer = null;
            }

            return readBytes;
        }

        public override void Flush() {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(Byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
