using LookUp.Core.Config;
using LookUp.Core.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using NBitcoin;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

public delegate JsonNode Encoder<in T>(T value);
public delegate Result<T, string> Decoder<T>(JsonElement value);

namespace LookUp.Core.Serialization
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

        public static JsonNode Network(Network network) => String(network.Name);

        public static JsonNode Config(Config.Config cfg) =>
            Object([
                ("Network", Network(cfg.Network)),
                ("MainNetBitcoinRpcUri", String(cfg.MainNetBitcoinRpcUri)),
                ("TestNetBitcoinRpcUri", String(cfg.TestNetBitcoinRpcUri)),
                ("RegTestBitcoinRpcUri", String(cfg.RegTestBitcoinRpcUri)),
                ("BitcoinRpcConnectionString", String(cfg.BitcoinRpcConnectionString))
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

        public static Decoder<string> String =>
            value =>
                value.ValueKind == JsonValueKind.String
                    ? Result<string, string>.Ok(value.GetString()!)
                    : Result<string, string>.Fail("It is not a string");

        public static readonly Decoder<Network> Network =
            String.AndThen(name =>
            {
                var network = NBitcoin.Network.GetNetwork(name);
                return network is { }
                    ? Succeed(network)
                    : Fail<Network>($"'{name}' is not a valid network.");
            });
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

        public static class ConfigDecode
        {
            public static Decoder<Config.Config> Config(string filePath) =>
                Object(get => new Config.Config(
                    filePath,
                    get.Required("Network", Decode.Network),
                    get.Required("BitcoinRpcConnectionString", Decode.String),
                    get.Required("MainNetBitcoinCoreRpcEndPoint", Decode.String),
                    get.Required("TestNetBitcoinCoreRpcEndPoint", Decode.String),
                    get.Required("RegTestBitcoinCoreRpcEndPoint", Decode.String)
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
