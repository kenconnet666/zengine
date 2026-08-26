namespace ZEngine.Ecs;

public readonly record struct EngineLoopOptions(
    TimeSpan FixedDelta,
    TimeSpan MaximumFrameDelta,
    int MaximumFixedSteps)
{
    public static EngineLoopOptions Default { get; } = new(
        TimeSpan.FromSeconds(1.0 / 60),
        TimeSpan.FromMilliseconds(250),
        8);
}

public readonly record struct FrameSchedule(
    ulong FrameIndex,
    int FixedStepCount,
    TimeSpan FixedDelta,
    TimeSpan VariableDelta,
    double InterpolationAlpha,
    bool DroppedAccumulatedTime);

public sealed class EngineLoopClock
{
    private readonly EngineLoopOptions _options;
    private TimeSpan _accumulator;
    private ulong _frameIndex;

    public EngineLoopClock(EngineLoopOptions? options = null)
    {
        _options = options ?? EngineLoopOptions.Default;
        if (_options.FixedDelta <= TimeSpan.Zero
            || _options.MaximumFrameDelta <= TimeSpan.Zero
            || _options.MaximumFixedSteps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    public FrameSchedule Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        TimeSpan clamped = elapsed > _options.MaximumFrameDelta
            ? _options.MaximumFrameDelta
            : elapsed;
        _accumulator += clamped;
        int steps = 0;
        while (_accumulator >= _options.FixedDelta
               && steps < _options.MaximumFixedSteps)
        {
            _accumulator -= _options.FixedDelta;
            steps++;
        }

        bool dropped = false;
        if (_accumulator >= _options.FixedDelta)
        {
            _accumulator = TimeSpan.FromTicks(
                _accumulator.Ticks % _options.FixedDelta.Ticks);
            dropped = true;
        }

        return new(
            ++_frameIndex,
            steps,
            _options.FixedDelta,
            elapsed,
            _accumulator.TotalSeconds / _options.FixedDelta.TotalSeconds,
            dropped);
    }
}
