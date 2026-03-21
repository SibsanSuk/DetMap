using DetMap.Core;
using DetMap.Tables;

namespace DetMap.Schema;

public enum DetStoreKind : byte
{
    Path = 0,
}

public sealed class DetLayerSchema
{
    public string Name { get; }
    public DetLayerKind Kind { get; }

    public DetLayerSchema(string name, DetLayerKind kind)
    {
        Name = name;
        Kind = kind;
    }
}

public sealed class DetColumnSchema
{
    public string Name { get; }
    public DetColumnKind Kind { get; }

    public DetColumnSchema(string name, DetColumnKind kind)
    {
        Name = name;
        Kind = kind;
    }
}

public sealed class DetTableSchema
{
    public string Name { get; }
    public IReadOnlyList<DetColumnSchema> Columns { get; }

    public DetTableSchema(string name, IReadOnlyList<DetColumnSchema> columns)
    {
        Name = name;
        Columns = columns;
    }
}

public sealed class DetStoreSchema
{
    public string Name { get; }
    public DetStoreKind Kind { get; }

    public DetStoreSchema(string name, DetStoreKind kind)
    {
        Name = name;
        Kind = kind;
    }
}

public sealed class DetDatabaseSchema
{
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<DetLayerSchema> Layers { get; }
    public IReadOnlyList<DetTableSchema> Tables { get; }
    public IReadOnlyList<string> GlobalKeys { get; }
    public IReadOnlyList<DetStoreSchema> Stores { get; }

    public DetDatabaseSchema(
        int width,
        int height,
        IReadOnlyList<DetLayerSchema> layers,
        IReadOnlyList<DetTableSchema> tables,
        IReadOnlyList<string> globalKeys,
        IReadOnlyList<DetStoreSchema> stores)
    {
        Width = width;
        Height = height;
        Layers = layers;
        Tables = tables;
        GlobalKeys = globalKeys;
        Stores = stores;
    }
}
