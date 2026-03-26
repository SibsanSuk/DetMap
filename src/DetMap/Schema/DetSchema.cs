using DetMap.Core;
using DetMap.Tables;

namespace DetMap.Schema;

public enum DetStoreKind : byte
{
    Path = 0,
}

public sealed class DetColumnOptions
{
    public bool IsDerived { get; }
    public string Source { get; }
    public bool IsEditable { get; }

    public DetColumnOptions(bool isDerived = false, string source = "", bool isEditable = true)
    {
        IsDerived = isDerived;
        Source = source ?? string.Empty;
        IsEditable = isEditable;
    }

    public static DetColumnOptions Derived(string source = "", bool isEditable = false)
        => new(isDerived: true, source: source, isEditable: isEditable);
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
    public bool IsDerived { get; }
    public string Source { get; }
    public bool IsEditable { get; }

    public DetColumnSchema(
        string name,
        DetColumnKind kind,
        bool isDerived = false,
        string source = "",
        bool isEditable = true)
    {
        Name = name;
        Kind = kind;
        IsDerived = isDerived;
        Source = source ?? string.Empty;
        IsEditable = isEditable;
    }
}

public sealed class DetColumnIndexSchema
{
    public string Name { get; }
    public DetColumnKind Kind { get; }
    public string ColumnName { get; }

    public DetColumnIndexSchema(string name, DetColumnKind kind, string columnName)
    {
        Name = name;
        Kind = kind;
        ColumnName = columnName;
    }
}

public sealed class DetTableSchema
{
    public string Name { get; }
    public IReadOnlyList<DetColumnSchema> Columns { get; }
    public IReadOnlyList<DetColumnIndexSchema> Indexes { get; }

    public DetTableSchema(
        string name,
        IReadOnlyList<DetColumnSchema> columns,
        IReadOnlyList<DetColumnIndexSchema>? indexes = null)
    {
        Name = name;
        Columns = columns;
        Indexes = indexes ?? Array.Empty<DetColumnIndexSchema>();
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
