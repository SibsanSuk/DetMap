using DetMath;

namespace DetMap.Core;

/// <summary>
/// Compile-time token that restricts grid layers and table columns to deterministic types only.
/// The internal constructor makes it impossible to create a token for float, double, or any
/// reference type — ensuring cross-platform determinism is enforced at compile time.
/// </summary>
public sealed class DetType<T> where T : unmanaged
{
    internal DetType() { }
}

/// <summary>
/// Allowed deterministic types for <see cref="DetGrid.CreateValueLayer{T}"/> and <see cref="DetTable.CreateColumn{T}"/>.
/// </summary>
public static class DetType
{
    /// <summary>8-bit unsigned integer. Use for flags, zone type, unit count (0–255).</summary>
    public static readonly DetType<byte> Byte = new();

    /// <summary>32-bit signed integer. Use for placement type, terrain type, row id.</summary>
    public static readonly DetType<int> Int = new();

    /// <summary>Deterministic fixed-point (DetMath.Fix64). Use for height, resource amount, cost.</summary>
    public static readonly DetType<Fix64> Fix64 = new();
}
