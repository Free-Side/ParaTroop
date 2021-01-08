using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Internal;

namespace ParaTroop.Web.Internal {
    public class PushbackTextReader : TextReader {
        private readonly TextReader innerReader;

        private readonly Stack<(Char[] buffer, Int32 start, Int32 length)> buffers =
            new Stack<(Char[], Int32, Int32)>();

        public PushbackTextReader(TextReader innerReader) {
            this.innerReader = innerReader ?? throw new ArgumentNullException(nameof(innerReader));
        }

        public override Int32 Read() {
            if (this.buffers.TryPop(out var buffer)) {
                Console.Write(',');
                var value = buffer.buffer[buffer.start];
                if (buffer.length > 1) {
                    this.buffers.Push((buffer.buffer, buffer.start + 1, buffer.length - 1));
                }

                return value;
            } else {
                Console.Write('.');
                return this.innerReader.Read();
            }
        }

        public override Int32 Read(Char[] buffer, Int32 index, Int32 count) {
            if (this.buffers.TryPop(out var sourceBuffer)) {
                Console.Write(',');
                Array.Copy(sourceBuffer.buffer, sourceBuffer.start, buffer, index, Math.Min(count, sourceBuffer.length));
                if (count < sourceBuffer.length) {
                    this.buffers.Push((sourceBuffer.buffer, sourceBuffer.start + count, sourceBuffer.length - count));
                    return count;
                } else {
                    return sourceBuffer.length;
                }
            } else {
                Console.Write('.');
                return this.innerReader.Read(buffer, index, count);
            }
        }

        public override Task<Int32> ReadAsync(Char[] buffer, Int32 index, Int32 count) {
            if (this.buffers.TryPop(out var sourceBuffer)) {
                Console.Write(',');
                Array.Copy(sourceBuffer.buffer, sourceBuffer.start, buffer, index, Math.Min(count, sourceBuffer.length));
                if (count < sourceBuffer.length) {
                    this.buffers.Push(
                        (sourceBuffer.buffer, sourceBuffer.start + count, sourceBuffer.length - count)
                    );
                    return Task.FromResult(count);
                } else {
                    return Task.FromResult(sourceBuffer.length);
                }
            } else {
                Console.Write('.');
                return this.innerReader.ReadAsync(buffer, index, count);
            }
        }

        public override async Task<Int32> ReadBlockAsync(Char[] buffer, Int32 index, Int32 count) {
            var bytesRead = 0;
            while (bytesRead < count && this.buffers.TryPop(out var sourceBuffer)) {
                Console.Write(',');
                var readCount = Math.Min(count - bytesRead, sourceBuffer.length);
                Array.Copy(sourceBuffer.buffer, sourceBuffer.start, buffer, index, readCount);
                index += readCount;
                bytesRead += readCount;

                if (readCount < sourceBuffer.length) {
                    this.buffers.Push((sourceBuffer.buffer, sourceBuffer.start + readCount, sourceBuffer.length - readCount));
                }
            }

            if (bytesRead < count) {
                Console.Write('.');
                bytesRead += await this.innerReader.ReadBlockAsync(buffer, index, count - bytesRead);
            }

            return bytesRead;
        }

        public override ValueTask<Int32> ReadAsync(
            Memory<Char> buffer,
            CancellationToken cancellationToken = new CancellationToken()) {

            if (this.buffers.TryPop(out var sourceBuffer)) {
                Console.Write(',');
                var length = Math.Min(buffer.Length, sourceBuffer.length);
                new Span<Char>(sourceBuffer.buffer, sourceBuffer.start, length)
                    .CopyTo(buffer.Span);

                if (length < sourceBuffer.length) {
                    this.buffers.Push(
                        (sourceBuffer.buffer, sourceBuffer.start + length, sourceBuffer.length - length)
                    );
                }

                return new ValueTask<Int32>(length);
            } else {
                Console.Write('.');
                return this.innerReader.ReadAsync(buffer, cancellationToken);
            }
        }

        public override async ValueTask<Int32> ReadBlockAsync(
            Memory<Char> buffer,
            CancellationToken cancellationToken = new CancellationToken()) {

            var bytesRead = 0;
            var index = 0;
            while (bytesRead < buffer.Length && this.buffers.TryPop(out var sourceBuffer)) {
                Console.Write(',');
                var readCount = Math.Min(buffer.Length - bytesRead, sourceBuffer.length);
                new Span<Char>(sourceBuffer.buffer, sourceBuffer.start, readCount)
                    .CopyTo(buffer.Span.Slice(index));
                index += readCount;
                bytesRead += readCount;

                if (readCount < sourceBuffer.length) {
                    this.buffers.Push((sourceBuffer.buffer, sourceBuffer.start + readCount, sourceBuffer.length - readCount));
                }
            }

            if (bytesRead < buffer.Length) {
                Console.Write('.');
                bytesRead += await this.innerReader.ReadBlockAsync(buffer, cancellationToken);
            }

            return bytesRead;
        }

        public override async Task<String> ReadLineAsync() {
            var stringBuilder = new StringBuilder();

            while (this.buffers.TryPop(out var buffer)) {
                Console.Write(',');
                var newlineIx = Array.IndexOf(buffer.buffer, '\n', buffer.start, buffer.length);

                if (newlineIx >= 0) {
                    var len = newlineIx - buffer.start;
                    stringBuilder.Append(buffer.buffer, buffer.start, len);
                    if (len + 1 < buffer.length) {
                        // TODO: \r handling?
                        this.buffers.Push((buffer.buffer, buffer.start + len + 1, buffer.length - (len + 1)));
                    }

                    return stringBuilder.ToString();
                } else {
                    stringBuilder.Append(buffer.buffer, buffer.start, buffer.length);
                }
            }

            Console.Write('.');
            stringBuilder.Append(await this.innerReader.ReadLineAsync());

            return stringBuilder.ToString();
        }

        public override async Task<String> ReadToEndAsync() {
            var builder = new StringBuilder();

            while (this.buffers.TryPop(out var buffer)) {
                Console.Write(',');
                builder.Append(buffer.buffer, buffer.start, buffer.length);
            }

            Console.Write('.');
            builder.Append(await this.innerReader.ReadToEndAsync());

            return builder.ToString();
        }

        /// <summary>
        /// Make a previously read buffer available upon the next read.
        /// </summary>
        /// <param name="buffer">The buffer to make available. This buffer should not be mutated after it is passed to this method.</param>
        /// <param name="start">The start position in the buffer.</param>
        /// <param name="length">The number of characters to make available from the buffer.</param>
        public void Pushback(Char[] buffer, Int32 start, Int32 length) {
            this.buffers.Push((buffer, start, length));
        }
    }
}
