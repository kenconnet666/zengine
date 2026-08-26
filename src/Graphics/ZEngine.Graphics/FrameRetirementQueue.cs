namespace ZEngine.Graphics;

/// <summary>
/// Releases resources only after the GPU timeline has reached their last use.
/// Registrations may arrive out of order; collection is ordered by timeline.
/// </summary>
public sealed class FrameRetirementQueue
{
    private readonly PriorityQueue<Action, ulong> _pending = new();

    public int Count => _pending.Count;

    public void Retire(ulong afterTimelineValue, Action release)
    {
        ArgumentNullException.ThrowIfNull(release);
        _pending.Enqueue(release, afterTimelineValue);
    }

    public int Collect(ulong completedTimelineValue)
    {
        int released = 0;
        while (_pending.TryPeek(out _, out ulong timelineValue)
               && timelineValue <= completedTimelineValue)
        {
            Action release = _pending.Dequeue();
            release();
            released++;
        }

        return released;
    }
}
