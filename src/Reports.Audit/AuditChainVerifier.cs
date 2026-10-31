using Reports.Core.Audit;

namespace Reports.Audit;

/// <summary>Outcome of a hash chain check.</summary>
/// <param name="IsIntact">True when no break was found.</param>
/// <param name="CheckedCount">Number of records examined.</param>
/// <param name="FirstBrokenIndex">Zero-based position (oldest first) of the first broken record, or null.</param>
/// <param name="FirstBrokenId">Database id of the first broken record, or null.</param>
/// <param name="Reason">Short description of the break, or null.</param>
public sealed record AuditChainResult(bool IsIntact, int CheckedCount, int? FirstBrokenIndex, long? FirstBrokenId, string? Reason);

/// <summary>Verifies the SEC-13 hash chain: changed content, changed order and removed middle records are detected.</summary>
/// <remarks>The first remaining record is anchored only by its own hash (older ones may be removed by retention); removal of the newest records is not detectable by the chain alone.</remarks>
public sealed class AuditChainVerifier
{
    private const int BatchSize = 500;

    private readonly IAuditRepository _repository;

    /// <summary>Creates the verifier.</summary>
    public AuditChainVerifier(IAuditRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>Walks the whole log oldest first and returns the first broken record, if any.</summary>
    public async Task<AuditChainResult> VerifyAsync(CancellationToken ct = default)
    {
        string? expectedPrev = null;
        long afterId = 0;
        var index = 0;

        while (true)
        {
            var batch = await _repository.GetOldestFirstAsync(afterId, BatchSize, ct).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                return new AuditChainResult(true, index, null, null, null);
            }

            foreach (var e in batch)
            {
                if (expectedPrev is not null && !string.Equals(e.PrevHash, expectedPrev, StringComparison.Ordinal))
                {
                    return new AuditChainResult(false, index + 1, index, e.Id, "Link to the previous record is broken (record removed or reordered).");
                }

                if (!string.Equals(e.Hash, AuditHasher.Compute(e), StringComparison.Ordinal))
                {
                    return new AuditChainResult(false, index + 1, index, e.Id, "Record content does not match its hash.");
                }

                expectedPrev = e.Hash;
                afterId = e.Id;
                index++;
            }
        }
    }
}
