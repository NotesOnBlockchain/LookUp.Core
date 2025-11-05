using LookUp.Core.Serialization;
using LookUp.Models;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

public delegate JsonNode Encoder<in T>(T value);
public delegate Result<T, string> Decoder<T>(JsonElement value);

namespace LookUp.Scanner.Serialization
{
    public static class JsonEncoder
    {
        private static readonly JsonSerializerOptions Indented = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string ToString<T>(T obj, Encoder<T> encoder) =>
            encoder(obj).ToJsonString();

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

        private static JsonNode Hexadecimal(byte[] bytes) =>
        String(Convert.ToHexString(bytes));

        public static JsonNode Network(Network network) =>
            String(network.Name);

        public static JsonNode UInt256(uint256 n) =>
            String(n.ToString());

        public static JsonNode Outpoint(OutPoint outPoint) =>
            Hexadecimal(outPoint.ToBytes());

        private static JsonNode Script(Script script) =>
            String(script.ToString());

        public static JsonNode MoneySatoshis(Money money) =>
            Int64(money.Satoshi);

        public static JsonNode MoneyBitcoins(Money money) =>
            String(money.ToString(fplus: false, trimExcessZero: true));

        private static JsonNode TxOut(TxOut txo) =>
            Object([
                ("ScriptPubKey", Script(txo.ScriptPubKey)),
                ("Value", MoneySatoshis(txo.Value))
            ]);

        private static JsonNode Coin(Coin coin) =>
            Object([
                ("Outpoint", Outpoint(coin.Outpoint)),
                ("TxOut", TxOut(coin.TxOut))
            ]);

        private static JsonNode FeeRate(FeeRate feeRate) =>
            MoneySatoshis(feeRate.FeePerK);

        private static JsonNode WitScript(WitScript witScript) =>
            Hexadecimal(witScript.ToBytes());

        public static JsonNode Config(Config.Config cfg) =>
            Object([
                ("Network", Network(cfg.Network)),
                ("MainNetBitcoinRpcUri", String(cfg.MainNetBitcoinRpcUri)),
                ("TestNetBitcoinRpcUri", String(cfg.TestNetBitcoinRpcUri)),
                ("RegTestBitcoinRpcUri", String(cfg.RegTestBitcoinRpcUri)),
                ("BitcoinRpcConnectionString", String(cfg.BitcoinRpcConnectionString))
            ]);

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
                    ("Network", Network(cfg.Network) ),
                    ("BitcoinRpcConnectionString", String(cfg.BitcoinRpcConnectionString) ),
                    ("MainNetBitcoinCoreRpcEndPoint", String(cfg.MainNetBitcoinRpcUri) ),
                    ("TestNetBitcoinCoreRpcEndPoint", String(cfg.TestNetBitcoinRpcUri) ),
                    ("RegTestBitcoinCoreRpcEndPoint", String(cfg.RegTestBitcoinRpcUri) )
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
        public static Decoder<Config.Config> Config(string filePath) =>
            Object(get => new Config.Config(filePath)
            {
                Network = get.Required("Network", Network),
                MainNetBitcoinRpcUri = get.Required("MainNetBitcoinRpcUri", String),
                TestNetBitcoinRpcUri = get.Required("TestNetBitcoinRpcUri", String),
                RegTestBitcoinRpcUri = get.Required("RegTestBitcoinRpcUri", String),
                BitcoinRpcConnectionString = get.Required("BitcoinRpcConnectionString", String)
            });

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

        public static readonly Decoder<uint256> UInt256 =
            String.Map(s => new uint256(s)).Catch();

        public static readonly Decoder<Network> Network =
            String.AndThen(name =>
            {
                var network = NBitcoin.Network.GetNetwork(name);
                return network is { }
                    ? Succeed(network)
                    : Fail<Network>($"'{name}' is not a valid network.");
            });

        public static readonly Decoder<byte[]> Hexadecimal =
        String.Map(Encoders.Hex.DecodeData).Catch();

        public static readonly Decoder<Money> MoneySatoshis =
            Int64.Map(Money.Satoshis);

        public static readonly Decoder<Money> MoneyBitcoins =
            String.Map(Money.Parse).Catch();

        public static readonly Decoder<FeeRate> FeeRate =
            MoneySatoshis.Map(m => new FeeRate(m)).Catch();

        public static readonly Decoder<WitScript> WitScript =
            Hexadecimal.Map(hex => new WitScript(hex)).Catch();

        public static readonly Decoder<OutPoint> OutPoint =
            Hexadecimal.Map(bytes =>
            {
                var op = new OutPoint();
                op.FromBytes(bytes);
                return op;
            }).Catch();

        public static readonly Decoder<Script> Script =
            String.Map(s => new Script(s)).Catch();

        public static readonly Decoder<TxOut> TxOut =
            Object(get => new TxOut(
                get.Required("Value", MoneySatoshis),
                get.Required("ScriptPubKey", Script)
            ));

        public static readonly Decoder<Coin> Coin =
            Object(get => new Coin(
                get.Required("Outpoint", OutPoint),
                get.Required("TxOut", TxOut)
            ));

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

            public T? Optional<T>(string fileName, Decoder<T> decoder) =>
                Field(fileName, decoder)(value).Match(
                    v => v,
                    _ => (T?)(object?)null);

            public T Optional<T>(string fileName, Decoder<T> decoder, T def) where T : struct =>
                Field(fileName, decoder)(value).Match(v => v, _ => def);
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
    }

    public static class JsonDecoder
    {
        public static Func<string, Result<T, string>> FromString<T>(Decoder<T> decoder) =>
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

        internal static T? FromString<T>(string json, Decoder<T> decoder) =>
            FromString(decoder)(json).AsNullable();

        public static Func<Stream, Task<Result<T, string>>> FromStreamAsync<T>(Decoder<T> decoder) =>
            async value =>
            {
                try
                {
                    var jsonDocument = await JsonDocument.ParseAsync(value).ConfigureAwait(false);
                    return decoder(jsonDocument.RootElement);
                }
                catch (JsonException e)
                {
                    return Result<T, string>.Fail(e.Message);
                }
            };

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
