using System;
using Newtonsoft.Json;
using ParaTroop.Web.Internal;

namespace ParaTroop.Web.Messages {
    /// <summary>
    /// An implementation of <see cref="JsonReader" /> that tracks how many characters have been read and supports a non-standard EndOfFile character.
    /// </summary>
    public partial class MessageJsonReader : JsonReader {
        private readonly PushbackTextReader reader;
        private readonly Char endOfFile;

        private Char[] buffer;
        private Int32 bufferLength;
        private Boolean isEndOfFile;

        private Int32 bufferPositionValue;

        private Int32 bufferPosition {
            get => this.bufferPositionValue;
            set {
                if (value > this.bufferPositionValue) {
                    // Any time buffer position is advanced also update the number bytes read.
                    this.ReadBytes += (value - this.bufferPositionValue);
                }

                this.bufferPositionValue = value;
            }
        }

        public Int32 ReadBytes { get; private set; }

        public MessageJsonReader(PushbackTextReader reader, Char endOfFile = '\0') {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.endOfFile = endOfFile;
            this.buffer = new Char[1024];
        }

        public override void Close() {
            base.Close();
            if (this.bufferPosition < this.bufferLength) {
                this.reader.Pushback(this.buffer, this.bufferPosition, this.bufferLength - this.bufferPosition);
            }
        }

        public String GetUnreadBuffer() {
            return this.bufferPosition < this.bufferLength ?
                new String(this.buffer, this.bufferPosition, this.bufferLength - this.bufferPosition) :
                String.Empty;
        }

        public override Boolean Read() {
            return Synchronously.Await(() => this.ReadAsync());
        }
    }
}
