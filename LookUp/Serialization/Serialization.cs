using LookUp.Models;
using LookUp.Serialization;
using NBitcoin;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

public delegate JsonNode Encoder<in T>(T value);
public delegate Result<T, string> Decoder<T>(JsonElement value);

namespace LookUp.Serialization
{
    public static class JsonEncoder
    {
        private static readonly JsonSerializerOptions Indented = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string ToReadableString<T>(T obj, Encoder<T> encoder) =>
            encoder(obj).ToJsonString(Indented);
    }

    public static partial class Encode
    {
        [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonNode String(string value) => JsonValue.Create(value);

        [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonNode Object(IEnumerable<(string, JsonNode?)> values) => new JsonObject(values.ToDictionary(x => x.Item1, x => x.Item2));

        [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonNode Int64(long value) => JsonValue.Create(value);

        [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonNode DatetimeOffset(DateTimeOffset value) => JsonValue.Create(value);

        [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsonNode Guid(Guid value) => JsonValue.Create(value);

        public static JsonNode Network(Network network) =>
            String(network.Name);


        public static JsonNode Message(MessageModel message) =>
            Object([
                ("ID", String(message.ID.ToString())),
                ("TransactionID", String(message.TransactionID)),
                ("Message", String(message.Message)),
                ("Hex", String(message.Hex)),
                ("BlockHash", String(message.BlockHash)),
                ("BlockMinedAt", String(message.BlockMinedAt.UtcDateTime.ToString()))
            ]);

        public static class ConfigEncode
        {
            public static JsonNode Config(Config.Config cfg) =>
                Object([
                    ("Network", Network(cfg.Network)),
                    ("BitcoinRpcConnectionString", String(cfg.BitcoinRpcConnectionString)),
                    ("MainNetBitcoinCoreRpcEndPoint", String(cfg.MainNetBitcoinRpcUri)),
                    ("TestNetBitcoinCoreRpcEndPoint", String(cfg.TestNetBitcoinRpcUri)),
                    ("RegTestBitcoinCoreRpcEndPoint", String(cfg.RegTestBitcoinRpcUri)),
                    ("SQLConnectionString", String(cfg.SQLConnectionString))
                    ]);
        }

        public static class WebsiteConfigEncode
        {
            public static JsonNode WebsiteConfig(Config.WebsiteConfig cfg) =>
                Object([
                    ("BackendUri", String(cfg.BackendUri)),
                ]);
        }

    }

    public static partial class Decode
    {
        public static Decoder<T> Catch<T>(this Decoder<T> decoder) =>
            value =>
            {
                try
                {
                    return decoder(value);
                }
                catch (Exception e)
                {
                    return Result<T, string>.Fail(e.Message);
                }
            };

        public static Decoder<T> Object<T>(Func<Getters, T> builder) =>
            value =>
            {
                var getters = new Getters(value);
                var result = builder(getters);
                return getters.Errors is [] ? result : Result<T, string>.Fail(string.Join("; ", getters.Errors));
            };

        private static Result<string, string> GetString(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                if (value.GetString() is { } str)
                {
                    return Result<string, string>.Ok(str);
                }

                return Result<string, string>.Fail("It is empty");
            }

            return Result<string, string>.Fail("It is not a string");
        }

        public static Decoder<string> String =>
            value =>
                value.ValueKind == JsonValueKind.String
                    ? Result<string, string>.Ok(value.GetString()!)
                    : Result<string, string>.Fail("It is not a string");

        public static Decoder<long> Int64 =>
        value => Integral("a long integer", long.TryParse, long.MinValue, long.MaxValue, Convert.ToInt64, value);

        public static Decoder<DateTimeOffset> DateTimeOffset =
            String.Map(System.DateTimeOffset.Parse);

        public static Decoder<Guid> Guid =>
            String.AndThen(str => System.Guid.TryParse(str, out var guid)
                ? Succeed(guid)
                : Fail<Guid>("The string is empty"));

        public static readonly Decoder<Network> Network =
            String.AndThen(name =>
            {
                var network = NBitcoin.Network.GetNetwork(name);
                return network is { }
                    ? Succeed(network)
                    : Fail<Network>($"'{name}' is not a valid network.");
            });

        private static Result<T, string> Integral<T>(
        string name,
        TryParse<T> tryParse,
        long min,
        long max,
        Func<double, T> conv,
        JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Number)
            {
                var rawText = value.GetRawText();
                if (!rawText.Contains('.'))
                {
                    var doubleValue = value.GetDouble();
                    return doubleValue >= min && doubleValue <= max
                        ? conv(doubleValue)
                        : Result<T, string>.Fail($"'{name}' is out of range for {typeof(T).Name}");
                }
            }
            else if (value.ValueKind == JsonValueKind.String)
            {
                return GetString(value).Then(str => tryParse(str, out T? parsedValue)
                    ? parsedValue
                    : Result<T, string>.Fail($"The string is not a valid integral number of '{name}' type '{typeof(T).Name}'"));
            }
            return Result<T, string>.Fail($"It is not '{name}'");
        }


        public class Getters(JsonElement value)
        {
            public List<string> Errors { get; } = [];
            public JsonElement Value => value;

            public T Required<T>(string fileName, Decoder<T> decoder) =>
                Field(fileName, decoder)(value)
                    .Match(v => v, e =>
                    {
                        Errors.Add(e);
                        return default!;
                    });
        }

        private delegate bool TryParse<T>(string input, [NotNullWhen(true)] out T? result);

        public static Decoder<T> AndThen<T, R>(this Decoder<R> decoder, Func<R, Decoder<T>> cb) => AndThen(cb, decoder);

        public static Decoder<T> AndThen<T, R>(Func<R, Decoder<T>> cb, Decoder<R> decoder) => value => decoder(value).Match(r => cb(r)(value), s => s);

        public static Decoder<T> Succeed<T>(T output) =>
        _ => output;

        public static Decoder<T> Fail<T>(string message) =>
            _ => Result<T, string>.Fail(message);

        public static Decoder<T> Field<T>(string fieldName, Decoder<T> decoder) =>
        value =>
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                return Result<T, string>.Fail($"It is not an object. Try to access field '{fieldName}'");
            }

            // this is because some coordinators serialize the message in pascal case
            var pascalCasedFieldName = string.Join("", fieldName[..1].ToUpperInvariant().Concat(fieldName[1..]));
            if (!value.TryGetProperty(fieldName, out var p) && !value.TryGetProperty(pascalCasedFieldName, out p))
            {
                return Result<T, string>.Fail($"Object does not contain a property called '{fieldName}'");
            }

            return decoder(p);
        };
        public static Decoder<T> Map<T, R>(this Decoder<R> decoder, Func<R, T> f) =>
            value =>
            {
                var m = decoder(value);
                return m.IsOk ? f(m.Value) : m.Error;
            };

        public static class ConfigDecode
        {
            public static Decoder<Config.Config> Config(string filePath) =>
                Object(get => new Config.Config(
                    filePath,
                    get.Required("Network", Network),
                    get.Required("BitcoinRpcConnectionString", String),
                    get.Required("MainNetBitcoinCoreRpcEndPoint", String),
                    get.Required("TestNetBitcoinCoreRpcEndPoint", String),
                    get.Required("RegTestBitcoinCoreRpcEndPoint", String),
                    get.Required("SQLConnectionString", String)
                ));
        }

        public static class WebsiteConfigDecode
        {
            public static Decoder<Config.WebsiteConfig> WebsiteConfig(string filePath) =>
                Object(get => new Config.WebsiteConfig(
                    filePath,
                    get.Required("BackendUri", String)
                ));
        }

        public static class MessageDecode
        {
            public static Decoder<MessageModel> MessageModel() =>
                Object(get => new MessageModel(
                    get.Required("ID", Guid),
                    get.Required("TransactionID", Decode.String),
                    get.Required("Message", String),
                    get.Required("Hex", String),
                    get.Required("BlochHash", String),
                    get.Required("BlockMinedAt", Decode.DateTimeOffset)));
        }
    }

    public static class JsonDecoder
    {
        public static Func<Stream, Result<T, string>> FromStream<T>(Decoder<T> decoder) =>
            value =>
            {
                try
                {
                    var jsonDocument = JsonDocument.Parse(value);
                    return decoder(jsonDocument.RootElement);
                }
                catch (JsonException e)
                {
                    return Result<T, string>.Fail(e.Message);
                }
            };
    }
}
