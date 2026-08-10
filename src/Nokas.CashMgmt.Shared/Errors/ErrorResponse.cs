using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.Shared.Errors;

[XmlRoot("ErrorResponse")]
public sealed class ErrorResponse
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("status")]
    public required int Status { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }

    [JsonPropertyName("controller")]
    public string? Controller { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("exception")]
    public string? Exception { get; init; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; init; }
}