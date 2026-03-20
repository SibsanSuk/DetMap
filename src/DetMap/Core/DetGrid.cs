using DetMap.Layers;

namespace DetMap.Core;

public sealed class DetGrid
{
    public readonly int Width;
    public readonly int Height;

    private readonly Dictionary<string, IDetLayer> _layers = new();

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
        return layer;
    }

    public DetBitLayer CreateBitLayer(string name)
    {
        var layer = new DetBitLayer(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetEntityLayer CreateEntityLayer(string name)
    {
        var layer = new DetEntityLayer(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetTagLayer CreateTagLayer(string name)
    {
        var layer = new DetTagLayer(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetFlowLayer CreateFlowLayer(string name)
    {
        var layer = new DetFlowLayer(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetValueLayer<T> GetValueLayer<T>(string name) where T : unmanaged
        => (DetValueLayer<T>)_layers[name];

    public DetBitLayer GetBitLayer(string name)
        => (DetBitLayer)_layers[name];

    public DetEntityLayer GetEntityLayer(string name)
        => (DetEntityLayer)_layers[name];

    public DetTagLayer GetTagLayer(string name)
        => (DetTagLayer)_layers[name];

    public DetFlowLayer GetFlowLayer(string name)
        => (DetFlowLayer)_layers[name];

    public IReadOnlyDictionary<string, IDetLayer> AllLayers => _layers;

    public bool InBounds(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height;
}
