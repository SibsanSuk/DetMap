using DetMath;
using DetMap.Layers;
using DetMap.Schema;

namespace DetMap.Core;

public sealed class DetGrid
{
    public readonly int Width;
    public readonly int Height;

    private readonly Dictionary<string, IDetLayer> _layers = new();
    private readonly List<string> _layerOrder = new();

    public DetGrid(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Low-level generic layer factory. Prefer the typed helpers when the value kind is known.
    /// </summary>
    /// <param name="type">Use <see cref="DetType.Byte"/>, <see cref="DetType.Int"/>, or <see cref="DetType.Fix64"/>.</param>
    public DetValueLayer<T> CreateValueLayer<T>(string name, DetType<T> type, T defaultValue = default)
        where T : unmanaged
    {
        var layer = new DetValueLayer<T>(name, Width, Height, defaultValue);
        _layers[name] = layer;
        AddLayerName(name);
        return layer;
    }

    public DetValueLayer<byte> CreateByteLayer(string name, byte defaultValue = default)
        => CreateValueLayer(name, DetType.Byte, defaultValue);

    public DetValueLayer<int> CreateIntLayer(string name, int defaultValue = default)
        => CreateValueLayer(name, DetType.Int, defaultValue);

    public DetValueLayer<Fix64> CreateFix64Layer(string name, Fix64 defaultValue = default)
        => CreateValueLayer(name, DetType.Fix64, defaultValue);

    public DetBitLayer CreateBitLayer(string name)
    {
        var layer = new DetBitLayer(name, Width, Height);
        _layers[name] = layer;
        AddLayerName(name);
        return layer;
    }

    public DetCellIndex CreateCellIndex(string name)
    {
        var layer = new DetCellIndex(name, Width, Height);
        _layers[name] = layer;
        AddLayerName(name);
        return layer;
    }

    public DetTagLayer CreateTagLayer(string name)
    {
        var layer = new DetTagLayer(name, Width, Height);
        _layers[name] = layer;
        AddLayerName(name);
        return layer;
    }

    public DetFlowLayer CreateFlowLayer(string name)
    {
        var layer = new DetFlowLayer(name, Width, Height);
        _layers[name] = layer;
        AddLayerName(name);
        return layer;
    }

    public DetValueLayer<T> GetValueLayer<T>(string name) where T : unmanaged
        => (DetValueLayer<T>)_layers[name];

    public DetValueLayer<byte> GetByteLayer(string name)
        => (DetValueLayer<byte>)_layers[name];

    public DetValueLayer<int> GetIntLayer(string name)
        => (DetValueLayer<int>)_layers[name];

    public DetValueLayer<Fix64> GetFix64Layer(string name)
        => (DetValueLayer<Fix64>)_layers[name];

    public DetBitLayer GetBitLayer(string name)
        => (DetBitLayer)_layers[name];

    public DetCellIndex GetCellIndex(string name)
        => (DetCellIndex)_layers[name];

    public DetTagLayer GetTagLayer(string name)
        => (DetTagLayer)_layers[name];

    public DetFlowLayer GetFlowLayer(string name)
        => (DetFlowLayer)_layers[name];

    public IReadOnlyDictionary<string, IDetLayer> AllLayers => _layers;
    public IReadOnlyList<string> LayerOrder => _layerOrder;

    public bool InBounds(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public IReadOnlyList<DetLayerSchema> GetLayerSchemas()
    {
        var schemas = new DetLayerSchema[_layerOrder.Count];
        for (int i = 0; i < _layerOrder.Count; i++)
        {
            string name = _layerOrder[i];
            schemas[i] = new DetLayerSchema(name, _layers[name].Kind);
        }

        return schemas;
    }

    private void AddLayerName(string name)
    {
        if (!_layerOrder.Contains(name))
            _layerOrder.Add(name);
    }
}
