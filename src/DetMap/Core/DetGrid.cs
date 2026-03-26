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

    internal bool HasCompatibleStructure(DetGrid source)
    {
        if (Width != source.Width || Height != source.Height)
            return false;
        if (_layerOrder.Count != source._layerOrder.Count)
            return false;

        for (int i = 0; i < _layerOrder.Count; i++)
        {
            string name = _layerOrder[i];
            if (!string.Equals(name, source._layerOrder[i], StringComparison.Ordinal))
                return false;
            if (_layers[name].Kind != source._layers[name].Kind)
                return false;
        }

        return true;
    }

    internal void CopyFrom(DetGrid source)
    {
        if (!HasCompatibleStructure(source))
            throw new InvalidOperationException("Cannot copy from a grid with a different schema.");

        foreach (string name in _layerOrder)
        {
            IDetLayer destinationLayer = _layers[name];
            IDetLayer sourceLayer = source._layers[name];

            switch (destinationLayer)
            {
                case DetValueLayer<byte> destination when sourceLayer is DetValueLayer<byte> sourceValue:
                    destination.CopyFrom(sourceValue);
                    break;
                case DetValueLayer<int> destination when sourceLayer is DetValueLayer<int> sourceValue:
                    destination.CopyFrom(sourceValue);
                    break;
                case DetValueLayer<Fix64> destination when sourceLayer is DetValueLayer<Fix64> sourceValue:
                    destination.CopyFrom(sourceValue);
                    break;
                case DetBitLayer destination when sourceLayer is DetBitLayer sourceBit:
                    destination.CopyFrom(sourceBit);
                    break;
                case DetCellIndex destination when sourceLayer is DetCellIndex sourceIndex:
                    destination.CopyFrom(sourceIndex);
                    break;
                case DetTagLayer destination when sourceLayer is DetTagLayer sourceTag:
                    destination.CopyFrom(sourceTag);
                    break;
                case DetFlowLayer destination when sourceLayer is DetFlowLayer sourceFlow:
                    destination.CopyFrom(sourceFlow);
                    break;
                default:
                    throw new NotSupportedException($"Layer copy is not supported for '{name}' ({destinationLayer.GetType().Name}).");
            }
        }
    }

    private void AddLayerName(string name)
    {
        if (!_layerOrder.Contains(name))
            _layerOrder.Add(name);
    }
}
