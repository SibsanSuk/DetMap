using DetMath;

namespace DetMap.Core;

/// <summary>
/// Compile-time type token for deterministic layer element types.
/// The internal constructor prevents users from creating tokens for non-deterministic types.
/// </summary>
public sealed class LayerType<T> where T : unmanaged
{
    internal LayerType() { }
}

/// <summary>
/// Allowed deterministic element types for <see cref="DetGrid.CreateLayer{T}"/>.
/// </summary>
public static class LayerType
{
    /// <summary>8-bit unsigned integer. Use for flags, zone type, unit count (0–255).</summary>
    public static readonly LayerType<byte> Byte = new();

    /// <summary>32-bit signed integer. Use for building ID, terrain type.</summary>
    public static readonly LayerType<int> Int = new();

    /// <summary>Deterministic fixed-point decimal (DetMath.Fix64). Use for height, resource amount, cost.</summary>
    public static readonly LayerType<Fix64> Fix64 = new();
}
