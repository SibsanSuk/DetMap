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

    /// <param name="type">Use <see cref="LayerType.Byte"/>, <see cref="LayerType.Int"/>, or <see cref="LayerType.Fix64"/>.</param>
    public DetLayer<T> CreateLayer<T>(string name, LayerType<T> type, T defaultValue = default)
        where T : unmanaged
    {
        var layer = new DetLayer<T>(name, Width, Height, defaultValue);
        _layers[name] = layer;
        return layer;
    }

    public DetBitLayer CreateBitLayer(string name)
    {
        var layer = new DetBitLayer(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetEntityMap CreateEntityMap(string name)
    {
        var layer = new DetEntityMap(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetTagMap CreateTagMap(string name)
    {
        var layer = new DetTagMap(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetFlowField CreateFlowField(string name)
    {
        var layer = new DetFlowField(name, Width, Height);
        _layers[name] = layer;
        return layer;
    }

    public DetLayer<T> Layer<T>(string name) where T : unmanaged
        => (DetLayer<T>)_layers[name];

    public T Structure<T>(string name) where T : class, IDetLayer
        => (T)_layers[name];

    public IReadOnlyDictionary<string, IDetLayer> AllLayers => _layers;

    public bool InBounds(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height;
}
