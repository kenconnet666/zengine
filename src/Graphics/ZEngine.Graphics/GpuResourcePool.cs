namespace ZEngine.Graphics;

/// <summary>
/// Dense generational ownership table for backend resources. This type is
/// intentionally not thread-safe; render-thread ownership must remain explicit.
/// </summary>
public sealed class GpuResourcePool<TTag, TResource>
    where TResource : class
{
    private readonly List<Slot> _slots = [];
    private readonly Stack<uint> _free = [];

    public int Count { get; private set; }

    public GpuHandle<TTag> Add(TResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        uint index;
        Slot slot;
        if (_free.TryPop(out index))
        {
            slot = _slots[checked((int)index)];
            slot.Resource = resource;
        }
        else
        {
            index = checked((uint)_slots.Count);
            slot = new()
            {
                Generation = 1,
                Resource = resource
            };
            _slots.Add(slot);
        }

        Count++;
        return new(index, slot.Generation);
    }

    public TResource Get(GpuHandle<TTag> handle)
    {
        if (!TryGet(handle, out TResource? resource))
        {
            throw new InvalidOperationException(
                $"GPU handle {handle} is null, stale, or owned by no live resource.");
        }

        return resource!;
    }

    public bool TryGet(
        GpuHandle<TTag> handle,
        out TResource? resource)
    {
        resource = null;
        if (handle.IsNull || handle.Index >= (uint)_slots.Count)
        {
            return false;
        }

        Slot slot = _slots[checked((int)handle.Index)];
        if (slot.Generation != handle.Generation || slot.Resource is null)
        {
            return false;
        }

        resource = slot.Resource;
        return true;
    }

    public TResource Remove(GpuHandle<TTag> handle)
    {
        TResource resource = Get(handle);
        Slot slot = _slots[checked((int)handle.Index)];
        slot.Resource = null;
        slot.Generation = NextGeneration(slot.Generation);
        _free.Push(handle.Index);
        Count--;
        return resource;
    }

    private static uint NextGeneration(uint generation)
    {
        uint next = generation + 1;
        return next == 0 ? 1 : next;
    }

    private sealed class Slot
    {
        public uint Generation;

        public TResource? Resource;
    }
}
