using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ParaTroop.Web.Internal;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace ParaTroop.Web.Messages {
    public abstract class MessageBase {
        private static readonly JsonSerializer serializer = new JsonSerializer();

        public abstract MessageType Type { get; }

        public Int32 MessageId { get; set; }

        public Int32 SourceClientId { get; set; }

        protected MessageBase() {
        }

        protected MessageBase(Int32 messageId, Int32 sourceClientId) {
            this.MessageId = messageId;
            this.SourceClientId = sourceClientId;
        }

        protected abstract IEnumerable<Object> GetData();

        public IEnumerable<String> GetSerializedData() {
            return new Object[] { (Int32)this.Type, this.MessageId, this.SourceClientId }
                .Concat(this.GetData())
                .Select(Serialize);
        }

        public override string ToString() {
            return $"<{String.Join("><", this.GetSerializedData())}>";
        }

        public static async Task<MessageBase> ReadSingleMessage(String message) {
            using var reader = new StringReader(message);
            using var pushbackReader = new PushbackTextReader(reader);
            return await ReadMessage(pushbackReader, CancellationToken.None);
        }

        public static async Task<MessageBase> ReadMessage(
            PushbackTextReader reader,
            CancellationToken cancellationToken) {

            Console.WriteLine("Begin Reading Message.");
            var type = (MessageType)await ReadValue<Int32>(reader, cancellationToken);
            MessageBase result;

            var messageId = await ReadValue<Int32>(reader, cancellationToken);
            var sourceClientId = await ReadValue<Int32>(reader, cancellationToken);
            Console.WriteLine($"Reading Message Type: {type}");
            switch (type) {
                case MessageType.Connect:
                    result = new ConnectMessage(
                        messageId,
                        sourceClientId,
                        name: await ReadValue<String>(reader, cancellationToken),
                        hostname: await ReadValue<String>(reader, cancellationToken),
                        port: await ReadValue<UInt16>(reader, cancellationToken),
                        dummy: await ReadValue<Boolean>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Operation:
                    result = new OperationMessage(
                        messageId,
                        sourceClientId,
                        operation: UnWrapObjects(await ReadValue<Object[]>(reader, cancellationToken)),
                        revision: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.SetMark:
                    result = new SetMarkMessage(
                        messageId,
                        sourceClientId,
                        index: await ReadValue<Int32>(reader, cancellationToken),
                        reply: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Authenticate:
                    result = new AuthenticateMessage(
                        passwordHash: await ReadValue<String>(reader, cancellationToken),
                        name: await ReadValue<String>(reader, cancellationToken),
                        version: await ReadValue<String>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Remove:
                    result = new RemoveMessage(
                        sourceClientId
                    );
                    break;
                case MessageType.EvaluateString:
                    result = new EvaluateStringMessage(
                        messageId,
                        sourceClientId,
                        @string: await ReadValue<String>(reader, cancellationToken),
                        reply: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.EvaluateBlock:
                    result = new EvaluateBlockMessage(
                        messageId,
                        sourceClientId,
                        start: await ReadValue<Int32>(reader, cancellationToken),
                        end: await ReadValue<Int32>(reader, cancellationToken),
                        reply: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.GetAll:
                    result = new GetAllMessage(
                        messageId,
                        sourceClientId
                    );
                    break;
                case MessageType.SetAll:
                    result = new SetAllMessage(
                        messageId,
                        sourceClientId,
                        document: await ReadValue<String>(reader, cancellationToken),
                        clientRanges: await ReadValue<Int32[][]>(reader, cancellationToken),
                        clientLocations: await ReadValue<Dictionary<String, Int32>>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Select:
                    result = new SelectMessage(
                        messageId,
                        sourceClientId,
                        start: await ReadValue<Int32>(reader, cancellationToken),
                        end: await ReadValue<Int32>(reader, cancellationToken),
                        reply: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Reset:
                    result = new ResetMessage(
                        messageId,
                        sourceClientId,
                        document: await ReadValue<String>(reader, cancellationToken),
                        clientRanges: await ReadValue<Int32[][]>(reader, cancellationToken),
                        clientLocations: await ReadValue<Dictionary<String, Int32>>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Kill:
                    result = new KillMessage(
                        messageId,
                        sourceClientId,
                        @string: await ReadValue<String>(reader, cancellationToken)
                    );
                    break;
                case MessageType.ConnectAck:
                    result = new ConnectAckMessage(
                        messageId,
                        sourceClientId,
                        reply: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.RequestAck:
                    result = new RequestAckMessage(
                        messageId,
                        sourceClientId,
                        flag: await ReadValue<Int32>(reader, cancellationToken),
                        reply: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Constraint:
                    result = new ConstraintMessage(
                        messageId,
                        sourceClientId,
                        constraintId: await ReadValue<Int32>(reader, cancellationToken)
                    );
                    break;
                case MessageType.Console:
                    result = new ConsoleMessage(
                        messageId,
                        sourceClientId,
                        @string: await ReadValue<String>(reader, cancellationToken)
                    );
                    break;
                case MessageType.KeepAlive:
                    result = new KeepAliveMessage(
                        messageId,
                        sourceClientId
                    );
                    break;
                case MessageType.StopAll:
                    result = new StopAllMessage(
                        messageId,
                        sourceClientId
                    );
                    break;
                default:
                    throw new InvalidDataException($"Unrecognized message type: {type}");
            }

            Console.WriteLine($"Successfully Read {type}: {result}");
            return result;
        }

        public static Object[] UnWrapObjects(Object[] objects) {
            for (var i = 0; i < objects.Length; i++) {
                if (objects[i] is JToken jObj) {
                    switch (jObj.Type) {
                        case JTokenType.Boolean:
                            objects[i] = jObj.Value<Boolean>();
                            break;
                        case JTokenType.Integer:
                            // Is there a way to determine the original data type?
                            objects[i] = jObj.Value<Int32>();
                            break;
                        case JTokenType.Float:
                            // Is there a way to determine the original data type?
                            objects[i] = jObj.Value<Double>();
                            break;
                        case JTokenType.String:
                            objects[i] = jObj.Value<String>();
                            break;
                        case JTokenType.Null:
                            objects[i] = null;
                            break;
                    }
                } else if (objects[i] is JsonElement jElem) {
                    switch (jElem.ValueKind) {
                        case JsonValueKind.Undefined:
                        case JsonValueKind.Null:
                            objects[i] = null;
                            break;
                        case JsonValueKind.String:
                            objects[i] = jElem.GetString();
                            break;
                        case JsonValueKind.Number:
                            // TODO: test this. Not sure what really happens when we read a decimal.
                            objects[i] =
                                jElem.TryGetInt32(out var intValue) ?
                                    intValue :
                                    (jElem.TryGetDecimal(out var decValue) ?
                                        (Object)decValue :
                                        jElem.GetDouble());
                            break;
                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            objects[i] = jElem.GetBoolean();
                            break;
                    }
                }
            }

            return objects;
        }

        private static async Task<T> ReadValue<T>(
            PushbackTextReader reader,
            CancellationToken cancellationToken) {
            var startChar = (Char)reader.Read();

            if (startChar < 0) {
                throw new InvalidDataException("Unexpected end of buffer attempting to read Troop message.");
            } else if (startChar != '<') {
                throw new InvalidDataException($"Expected a value string with '<value>' but found a starting character of: '{startChar}'");
            }

            using var jsonReader = new MessageJsonReader(reader);
            var token = await JToken.LoadAsync(jsonReader, cancellationToken);
            // Hopefully this works with value types
            var result = token.ToObject<T>();
            // Calling close pushes unused buffer back to the reader
            jsonReader.Close();

            var endChar = (Char)reader.Read();
            if (endChar != '>') {
                throw new InvalidDataException($"Expected a value string with '<value>' but found a ending character of: '{startChar}'");
            }

            return result;
        }

        private static String Serialize(Object value) {
            using var writer = new StringWriter();
            serializer.Serialize(writer, value);
            return writer.ToString().Replace(">", "\\>");
        }
    }
}
