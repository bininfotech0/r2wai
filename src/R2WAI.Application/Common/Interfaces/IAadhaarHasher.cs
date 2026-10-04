namespace R2WAI.Application.Common.Interfaces;

/// <summary>
/// Deterministic, keyed hash for Aadhaar-number duplicate-detection lookups
/// (RegisterMemberCommand — "does an account with this Aadhaar number already exist?").
///
/// Aadhaar numbers are low-entropy, structured 12-digit identifiers (roughly 10^11 possibilities
/// before the Verhoeff checksum narrows it further, per AadhaarValidator) — a *plain* unsalted hash
/// of one is brute-forceable offline in practical time on modern hardware if the hash column ever
/// leaks (a DB breach, an overloaded backup, etc.), unlike hashing a genuinely high-entropy secret
/// (e.g. a refresh token) the same way, where that's a non-issue. A random per-row salt isn't an
/// option either — the whole point is an exact-match lookup, which needs the same input to always
/// produce the same output. HMAC keyed with an application-wide secret is the standard answer to
/// exactly this "deterministic but low-entropy" shape: reversing a leaked hash then requires the
/// key too, not just the hash column.
/// </summary>
public interface IAadhaarHasher
{
    string Hash(string aadhaarNumber);
}
