using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ParaTroop.Web.Messages {
    public partial class MessageJsonReader {
        public override async Task<Boolean> ReadAsync(
            CancellationToken cancellationToken = default) {

            while (true) {
                switch (this.CurrentState) {
                    case State.Start:
                    case State.Property:
                    case State.Array:
                    case State.ArrayStart:
                    case State.Constructor:
                    case State.ConstructorStart:
                        return await ParseValueAsync(cancellationToken);
                    case State.Object:
                    case State.ObjectStart:
                        await ParseObjectAsync(cancellationToken);
                        return true;
                    case State.PostValue:
                        // returns true if it hits
                        // end of object or array
                        if (await ParsePostValueAsync(cancellationToken)) {
                            return true;
                        }

                        break;
                    case State.Finished:
                        if (await EnsureCharsAsync(cancellationToken: cancellationToken)) {
                            // Consume trailing whitespace & check for end of file.
                            await EatWhitespaceAsync(cancellationToken);
                            if (isEndOfFile || this.buffer[this.bufferPosition] == endOfFile) {
                                SetToken(JsonToken.None);
                                return false;
                            }

                            throw new JsonReaderException(
                                $"Additional text encountered after finished reading JSON content: {buffer[this.bufferPosition]}."
                            );
                        }

                        SetToken(JsonToken.None);
                        return false;
                    default:
                        throw new JsonReaderException(
                            $"Unexpected state: {this.CurrentState}."
                        );
                }
            }
        }

        private async Task<Boolean> ParseValueAsync(CancellationToken cancellationToken) {
            while (this.bufferPosition < this.bufferLength || await EnsureCharsAsync(cancellationToken: cancellationToken)) {
                var currentChar = this.buffer[this.bufferPosition];

                switch (currentChar) {
                    case '"':
                    case '\'':
                        await ParseStringAsync(currentChar, cancellationToken);
                        return true;
                    case 't':
                        await ParseExactAsync("true", JsonToken.Boolean, true, cancellationToken);
                        return true;
                    case 'f':
                        await ParseExactAsync("false", JsonToken.Boolean, false, cancellationToken);
                        return true;
                    case 'n':
                        await ParseExactAsync("null", JsonToken.Null, null, cancellationToken);
                        return true;
                    case 'u':
                        await ParseExactAsync("undefined", JsonToken.Undefined, null, cancellationToken);
                        return true;
                    case '/':
                        await ParseCommentAsync(true, cancellationToken);
                        return true;
                    case '{':
                        this.bufferPosition++;
                        SetToken(JsonToken.StartObject);
                        return true;
                    case '[':
                        this.bufferPosition++;
                        SetToken(JsonToken.StartArray);
                        return true;
                    case ']':
                        this.bufferPosition++;
                        SetToken(JsonToken.EndArray);
                        return true;
                    default:
                        if (Char.IsWhiteSpace(currentChar)) {
                            // Consume whitespace
                            this.bufferPosition++;
                        } else if (Char.IsNumber(currentChar) || currentChar == '-' || currentChar == '.') {
                            await ParseNumberAsync(cancellationToken);
                            return true;
                        } else {
                            throw new JsonReaderException(
                                $"Unexpected character when attempting to read value: {currentChar}"
                            );
                        }

                        break;
                }
            }

            this.SetToken(JsonToken.None);
            return false;
        }

        private async Task<Boolean> ParsePostValueAsync(CancellationToken cancellationToken) {
            while (this.bufferPosition < this.bufferLength || await EnsureCharsAsync(cancellationToken: cancellationToken)) {
                switch (this.buffer[this.bufferPosition]) {
                    case '/':
                        await this.ParseCommentAsync(false, cancellationToken);
                        break;
                    case '}':
                        this.bufferPosition++;
                        SetToken(JsonToken.EndObject);
                        return true;
                    case ']':
                        this.bufferPosition++;
                        SetToken(JsonToken.EndArray);
                        return true;
                    case ',':
                        this.bufferPosition++;
                        SetStateBasedOnCurrent();
                        return false;
                    case {} c when Char.IsWhiteSpace(c):
                        // Consume whitespace
                        this.bufferPosition++;
                        break;
                    case {} c when c == this.endOfFile:
                        this.isEndOfFile = true;
                        return false;
                    case {} c:
                        throw new JsonReaderException(
                            $"Unexpected trailing character found after value: {c}."
                        );
                }
            }

            // EOF
            return false;
        }

        private readonly ISet<Char> numberCharacters =
            ImmutableHashSet.Create(
                OrdinalIgnoreCaseCharacterComparer.Instance,
                new[] { '-', '+', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'e' }
            );

        private async Task ParseNumberAsync(CancellationToken cancellationToken) {
            var numberLength = 0;
            var hasDecimal = false;
            var hasExponent = false;
            var end = false;
            // Consume all legal number characters
            while (!end && await EnsureCharsAsync(numberLength + 1, cancellationToken)) {
                switch (this.buffer[this.bufferPosition + numberLength]) {
                    case '.':
                        hasDecimal = true;
                        numberLength++;
                        break;
                    case 'e':
                    case 'E':
                        hasExponent = true;
                        numberLength++;
                        break;
                    case {} c:
                        if (numberCharacters.Contains(c)) {
                            numberLength++;
                        } else {
                            end = true;
                        }

                        break;
                }
            }

            var str = new String(buffer, bufferPosition, numberLength);
            if (hasExponent) {
                if (Double.TryParse(str, out var value)) {
                    this.bufferPosition += numberLength;
                    this.SetToken(JsonToken.Float, value, false);
                } else {
                    throw new JsonReaderException(
                        $"Invalid Number: {str}"
                    );
                }
            } else if (hasDecimal) {
                if (Decimal.TryParse(str, out var value)) {
                    this.bufferPosition += numberLength;
                    this.SetToken(JsonToken.Float, value, false);
                } else {
                    throw new JsonReaderException(
                        $"Invalid Number: {str}"
                    );
                }
            } else {
                if (Int64.TryParse(str, out var value)) {
                    this.bufferPosition += numberLength;
                    this.SetToken(
                        JsonToken.Integer,
                        value < Int32.MaxValue && value > Int32.MinValue ? (Int32)value : value,
                        false
                    );
                } else {
                    throw new JsonReaderException(
                        $"Invalid Number: {str}"
                    );
                }
            }
        }

        private async Task ParseStringAsync(Char quoteChar, CancellationToken cancellationToken) {
            this.SetToken(JsonToken.String, await this.GetQuotedStringAsync(quoteChar, cancellationToken));
        }

        private async Task<String> GetQuotedStringAsync(Char quoteChar, CancellationToken cancellationToken) {
            var stringBuilder = new StringBuilder();
            // Consume start quote
            this.bufferPosition++;
            var position = 0;
            while (this.bufferPosition + position < this.bufferLength ||
                await EnsureCharsAsync(position + 1, cancellationToken)) {

                switch (this.buffer[this.bufferPosition + position]) {
                    case '\\':
                        if (position > 0) {
                            stringBuilder.Append(this.buffer, this.bufferPosition, position);
                        }

                        if (!await EnsureCharsAsync(position + 2, cancellationToken)) {
                            throw new JsonReaderException("Unexpected end of file attempting to read string.");
                        }

                        var consumed = 2;
                        switch (this.buffer[this.bufferPosition + position + 1]) {
                            case 'b':
                                stringBuilder.Append('\b');
                                break;
                            case 'f':
                                stringBuilder.Append('\f');
                                break;
                            case 'r':
                                stringBuilder.Append('\r');
                                break;
                            case 'n':
                                stringBuilder.Append('\n');
                                break;
                            case '0':
                                stringBuilder.Append('\0');
                                break;
                            case 't':
                                stringBuilder.Append('\t');
                                break;
                            case 'u':
                                if (!await this.EnsureCharsAsync(position + 6, cancellationToken)) {
                                    throw new JsonReaderException(
                                        "Unexpected end of file attempting to read string."
                                    );
                                }

                                var str = new String(this.buffer, this.bufferPosition + 2, 4);
                                try {
                                    var bytes = BitConverter.GetBytes(Convert.ToUInt16(str, 16));
                                    stringBuilder.Append(Encoding.Unicode.GetString(bytes));
                                    consumed = 6;
                                } catch (FormatException) {
                                    throw new JsonReaderException(
                                        $"Invalid Unicode character escape sequence: \\u{str}"
                                    );
                                }

                                break;
                            case {} c:
                                stringBuilder.Append(c);
                                break;
                        }

                        this.bufferPosition += position + consumed;
                        position = 0;

                        break;
                    case {} c when c == quoteChar:
                        if (position > 0) {
                            stringBuilder.Append(this.buffer, this.bufferPosition, position);
                        }

                        this.bufferPosition += position + 1;
                        return stringBuilder.ToString();
                    default:
                        position++;
                        break;
                }
            }

            throw new JsonReaderException("Unexpected end of file attempting to read string.");
        }

        private Task ParseExactAsync(
            String expected,
            CancellationToken cancellationToken) {

            return this.ParseExactAsync(expected, null, null, cancellationToken);
        }

        private async Task ParseExactAsync(
            String expected,
            JsonToken? tokenType,
            Object value,
            CancellationToken cancellationToken) {

            if (await this.EnsureCharsAsync(expected.Length, cancellationToken)) {
                var str = new String(this.buffer, this.bufferPosition, expected.Length);
                if (String.Equals(expected, str, StringComparison.OrdinalIgnoreCase)) {
                    this.bufferPosition += expected.Length;
                    if (tokenType.HasValue) {
                        this.SetToken(tokenType.Value, value);
                    }
                } else {
                    throw new JsonReaderException(
                        $"Unexpected value while reading {expected}: {str}"
                    );
                }
            } else {
                throw new JsonReaderException(
                    $"Unexpected EOF reading expected value: {expected}"
                );
            }
        }

        private async Task ParseCommentAsync(Boolean setToken, CancellationToken cancellationToken) {
            if (!await this.EnsureCharsAsync(2, cancellationToken)) {
                throw new JsonReaderException(
                    "Unexpected end of file attempting to read comment."
                );
            }

            switch (this.buffer[this.bufferPosition + 1]) {
                case '/':
                    // Read single line comment
                    {
                        this.bufferPosition += 2;
                        var position = 0;
                        while (this.bufferPosition + position < this.bufferLength ||
                            await EnsureCharsAsync(position + 1, cancellationToken)) {

                            if (this.buffer[this.bufferPosition + position] == '\n') {
                                if (setToken) {
                                    this.SetToken(JsonToken.Comment, new String(this.buffer, this.bufferPosition, position));
                                }

                                this.bufferPosition += position;
                                return;
                            }

                            position++;
                        }
                    }
                    break;
                case '*':
                    // Read multi-line/in-line comment
                    {
                        this.bufferPosition += 2;
                        var position = 0;
                        while (this.bufferPosition + position < this.bufferLength ||
                            await EnsureCharsAsync(position + 1, cancellationToken)) {

                            if (this.buffer[this.bufferPosition + position] == '*') {
                                if (!await this.EnsureCharsAsync(position + 2, cancellationToken)) {
                                    // Unexpected EOF
                                    break;
                                } else if (this.buffer[this.bufferPosition + position + 1] == '/') {
                                    if (setToken) {
                                        this.SetToken(JsonToken.Comment, new String(this.buffer, this.bufferPosition, position));
                                    }

                                    this.bufferPosition += position + 2;
                                    return;
                                }
                            }

                            position++;
                        }
                    }
                    break;
                default:
                    var str = new String(this.buffer, this.bufferPosition, 2);
                    throw new JsonReaderException(
                        $"Unexpected character sequence. Expected comment to begin with // or /* but encountered: {str}"
                    );
            }

            throw new JsonReaderException(
                "Unexpected end of file attempting to read comment."
            );
        }

        private async Task ParseObjectAsync(CancellationToken cancellationToken) {
            while (this.bufferPosition < this.bufferLength ||
                await this.EnsureCharsAsync(cancellationToken: cancellationToken)) {

                switch (this.buffer[this.bufferPosition]) {
                    case '/':
                        await this.ParseCommentAsync(false, cancellationToken);
                        break;
                    case '}':
                        this.bufferPosition++;
                        this.SetToken(JsonToken.EndObject);
                        break;
                    case {} c:
                        if (Char.IsWhiteSpace(c)) {
                            // Consume whitespace
                            this.bufferPosition++;
                        } else {
                            await this.ParsePropertyAsync(cancellationToken);

                            await this.EatWhitespaceAsync(cancellationToken);
                            await this.ParseExactAsync(":", cancellationToken);

                            return;
                        }

                        break;
                }
            }
        }

        private async Task ParsePropertyAsync(CancellationToken cancellationToken) {
            var currentChar = this.buffer[this.bufferPosition];
            switch (currentChar) {
                case '\'':
                case '"':
                    var property = await this.GetQuotedStringAsync(currentChar, cancellationToken);
                    this.SetToken(JsonToken.PropertyName, property);

                    break;
                default:

                    // Read Unquoted

                    var position = 0;
                    while (this.bufferPosition + position < this.bufferLength ||
                        await EnsureCharsAsync(position + 1, cancellationToken)) {

                        switch (this.buffer[this.bufferPosition + position]) {
                            case ':':
                            case {} c when Char.IsWhiteSpace(c):
                                this.SetToken(
                                    JsonToken.PropertyName,
                                    new String(this.buffer, this.bufferPosition, position)
                                );
                                break;
                            case {} c when IsValidIdentifierChar(c):
                                position++;
                                break;
                            case {} c:
                                throw new JsonReaderException(
                                    $"Invalid JavaScript property identifier character: {c}."
                                );
                        }
                    }

                    break;
            }
        }

        private static Boolean IsValidIdentifierChar(Char value) {
            return Char.IsLetterOrDigit(value) || value == '_' || value == '$';
        }

        private async Task EatWhitespaceAsync(CancellationToken cancellationToken) {
            while (this.bufferPosition < this.bufferLength || await EnsureCharsAsync(cancellationToken: cancellationToken)) {
                if (Char.IsWhiteSpace(this.buffer[this.bufferPosition])) {
                    this.bufferPosition++;
                } else {
                    break;
                }
            }
        }

        // Ensure that the buffer has at least the requested number of characters.
        private async Task<Boolean> EnsureCharsAsync(
            Int32 reserveCharacters = 1,
            CancellationToken cancellationToken = default) {
            while (this.bufferPosition + reserveCharacters > this.bufferLength) {
                if (this.isEndOfFile) {
                    return false;
                }

                // Read another block of data until we have the requested number of characters in the buffer.
                var existingCharacters = this.bufferLength - this.bufferPosition;

                // Shift any pending data to the beginning of the buffer and enlarge it as needed.
                if (reserveCharacters > this.buffer.Length / 2) {
                    // If our buffer is already > 50% full double it
                    var oldBuffer = this.buffer;
                    this.buffer = new Char[this.buffer.Length * 2];
                    Array.Copy(
                        oldBuffer,
                        this.bufferPosition,
                        this.buffer,
                        0,
                        existingCharacters
                    );
                    this.bufferPosition = 0;
                } else if (this.bufferPosition > 0) {
                    if (reserveCharacters > 0) {
                        // Shift the data in the buffer to the beginning
                        Array.Copy(this.buffer, this.bufferPosition, this.buffer, 0, existingCharacters);
                    }

                    this.bufferPosition = 0;
                }

                var memory = new Memory<Char>(
                    this.buffer,
                    existingCharacters,
                    this.buffer.Length - existingCharacters
                );

                var len = await this.reader.ReadAsync(memory, cancellationToken);
                this.bufferLength = existingCharacters + len;

                if (len == 0) {
                    this.isEndOfFile = true;
                    return false;
                }
            }

            return true;
        }

        internal class OrdinalIgnoreCaseCharacterComparer : EqualityComparer<Char>, IComparer<Char> {
            public static OrdinalIgnoreCaseCharacterComparer Instance { get; } =
                new OrdinalIgnoreCaseCharacterComparer();

            private OrdinalIgnoreCaseCharacterComparer() {
            }

            public Int32 Compare(Char x, Char y) {
                return StringComparer.OrdinalIgnoreCase.Compare(x.ToString(), y.ToString());
            }

            public override Boolean Equals(Char x, Char y) {
                return StringComparer.OrdinalIgnoreCase.Equals(x.ToString(), y.ToString());
            }

            public override Int32 GetHashCode(Char value) {
                return StringComparer.OrdinalIgnoreCase.GetHashCode(value.ToString());
            }
        }
    }
}
