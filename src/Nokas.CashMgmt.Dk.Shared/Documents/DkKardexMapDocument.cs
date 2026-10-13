using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Dk.Shared.Documents;

/// <summary>
/// Claims which onboarded partner (clientId) owns a given ServiceNow "kardex"
/// (<c>customerRefNumber</c>).
///
/// ShopBox is one client id shared by many underlying retail customers, each identified in
/// ServiceNow by their own kardex. The registration endpoint is deliberately generic, so
/// nothing upstream tells us which client id a kardex belongs to except that a partner using
/// that client id was the one who first registered a bag for it. This document makes that
/// observation permanent: the first client id to use a kardex owns it, forever - a second
/// client id claiming the same kardex is a tenancy conflict, not a valid re-registration, and
/// is rejected rather than silently overwriting the mapping.
///
/// Written via Cosmos <c>CreateItemAsync</c> only (never Upsert) so the write itself enforces
/// the claim atomically: a second create for the same id fails with 409 Conflict, which is
/// exactly the signal needed to detect a conflicting claim.
/// </summary>
public sealed class DkKardexMapDocument
{
    /// <summary>Equal to the kardex (customerRefNumber). Point-read/point-create by id.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("customerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("claimedAtUtc")]
    public DateTime ClaimedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>What first established the claim - "registration" or "sync" (a counting-result
    /// row synced for a kardex before any registration claimed it).</summary>
    [JsonPropertyName("claimedBy")]
    public required string ClaimedBy { get; init; }

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "dkKardexMap";
}
