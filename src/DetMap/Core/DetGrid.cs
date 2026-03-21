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

    /// <param name="type">Use <see cref="DetType.Byte"/>, <see cref="DetType.Int"/>, or <see cref="DetType.Fix64"/>.</param>
    public DetValueLayer<T> CreateValueLayer<T>(string name, DetType<T> type, T defaultValue = default)
        where T : unmanaged
    {
        var layer = new DetValueLayer<T>(name, Width, Height, defaultValue);
        _layers[name] = layer;
        AddLayerName(name);
        return layer;
    }

    public DetBooleanLayer CreateBooleanLayer(string name)
    {
        var layer = new DetBooleanLayer(name, Width, Height);
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

    public DetBooleanLayer GetBooleanLayer(string name)
        => (DetBooleanLayer)_layers[name];

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
