using Nokas.CashMgmt.Dk.Shared.Documents;

namespace Nokas.CashMgmt.Dk.Shared.Repositories;

public enum KardexClaimOutcome
{
    /// <summary>No prior claim existed; this clientId now owns the kardex.</summary>
    ClaimedNew,

    /// <summary>The kardex was already owned by this same clientId - nothing changed.</summary>
    AlreadyOwnedBySameClient,

    /// <summary>The kardex is owned by a DIFFERENT clientId. The caller must not attribute this record to clientId.</summary>
    OwnedByDifferentClient
}

public interface IDkKardexMapRepository
{
    /// <summary>
    /// Atomically claims a kardex for a clientId if unclaimed, or verifies the existing claim.
    /// Never overwrites an existing claim for a different clientId - see
    /// DkKardexMapDocument for why that must never happen silently.
    /// </summary>
    Task<KardexClaimOutcome> ClaimOrVerifyAsync(
        string customerRefNumber, string clientId, string claimedBy, CancellationToken cancellationToken);

    /// <summary>The clientId owning a kardex, or null if unclaimed.</summary>
    Task<string?> TryGetOwnerAsync(string customerRefNumber, CancellationToken cancellationToken);
}
