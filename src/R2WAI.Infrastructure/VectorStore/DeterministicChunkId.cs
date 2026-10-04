using System.Security.Cryptography;
using System.Text;

namespace R2WAI.Infrastructure.VectorStore;

/// <summary>
/// Derives a stable point id for one chunk of one source, so re-indexing the same source replaces
/// its vectors instead of appending a second copy of them.
///
/// Both ingestion paths used to mint <c>Guid.NewGuid()</c> per chunk. <c>vector_embeddings.id</c>
/// is the primary key and <c>PgVectorService.UpsertVectorsAsync</c> upserts with
/// <c>ON CONFLICT (id) DO UPDATE</c> — so with random ids that conflict clause is unreachable and
/// every re-index silently doubled a source's vectors. The duplicates then both match a search
/// (identical text, identical score), the source's <c>MarkIndexed</c> count disagreed with what was
/// actually in the collection, and <c>RemoveSourceAsync</c>'s delete-by-id could only ever reclaim
/// one of the copies it found in its top-N scan.
///
/// Deriving the id from (sourceId, chunkIndex) makes the upsert a true replace. Note this is what
/// makes a re-index <em>replace</em> rather than <em>append</em> — it deliberately does not handle
/// the "source got shorter, so stale high-index chunks linger" case on its own; that is what
/// <see cref="IVectorStoreService.DeleteVectorsBySourceAsync"/> is for, and callers that re-index
/// an existing source must call it first.
/// </summary>
internal static class DeterministicChunkId
{
    /// <summary>
    /// RFC 4122 name-based (version 5, SHA-1) UUID over a stable, collision-resistant string.
    /// Deterministic by construction: the same <paramref name="sourceId"/> and
    /// <paramref name="chunkIndex"/> always yield the same point id, in this process and the next.
    /// </summary>
    public static Guid ForSourceChunk(Guid sourceId, int chunkIndex)
        => ForName($"{sourceId:N}:{chunkIndex}");

    private static Guid ForName(string name)
    {
        var namespaceBytes = new Guid("6ba7b8119dad11d180b400c04fd430c8").ToByteArray();
        SwapByteOrder(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var buffer = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(buffer, 0);
        nameBytes.CopyTo(buffer, namespaceBytes.Length);

        var hash = SHA1.HashData(buffer);

        // RFC 4122 v5 takes the first 16 bytes of the SHA-1 digest. new Guid(byte[]) rejects
        // anything else outright, so this truncation is load-bearing, not cosmetic.
        Span<byte> digest = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(digest);

        digest[6] = (byte)((digest[6] & 0x0F) | 0x50); // version 5
        digest[8] = (byte)((digest[8] & 0x3F) | 0x80); // IETF variant

        return FromBigEndianBytes(digest);
    }

    private static void SwapByteOrder(Span<byte> guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }

    private static Guid FromBigEndianBytes(ReadOnlySpan<byte> bigEndian)
    {
        Span<byte> b = stackalloc byte[16];
        bigEndian.CopyTo(b);
        SwapByteOrder(b);
        return new Guid(b);
    }
}
