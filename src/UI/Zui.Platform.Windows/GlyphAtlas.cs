namespace ZEngine.UI.Windows;

public readonly record struct GlyphKey(
    string FontPath,
    int FaceOffset,
    int CodePoint,
    int PixelSize);

public readonly record struct GlyphSlot(
    int X,
    int Y,
    int Width,
    int Height,
    ulong Generation);

public sealed class GlyphAtlas(
    int width = 2048,
    int height = 2048,
    int cellSize = 64)
{
    private readonly Dictionary<GlyphKey, GlyphSlot> _slots = [];
    private readonly Queue<GlyphKey> _order = [];
    private ulong _generation;

    public int Capacity { get; } = (width / cellSize) * (height / cellSize);

    public int Count => _slots.Count;

    public GlyphSlot GetOrAdd(GlyphKey key)
    {
        if (_slots.TryGetValue(key, out GlyphSlot existing))
        {
            return existing;
        }

        if (_slots.Count == Capacity)
        {
            GlyphKey evicted = _order.Dequeue();
            _slots.Remove(evicted);
        }

        int index = _slots.Count;
        int columns = width / cellSize;
        GlyphSlot slot = new(
            index % columns * cellSize,
            index / columns * cellSize,
            cellSize,
            cellSize,
            ++_generation);
        _slots.Add(key, slot);
        _order.Enqueue(key);
        return slot;
    }
}
