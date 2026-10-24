namespace LookUp.Models
{
    // The message filter's rules as published by the website's /activefilters endpoint.
    public record FilterRules(
        string Summary,
        IReadOnlyList<string> Rules,
        IReadOnlyList<BannedHexPrefix> BannedHexPrefixes,
        IReadOnlyList<string> BannedMessageParts);

    public record BannedHexPrefix(string Hex, string Text);
}
