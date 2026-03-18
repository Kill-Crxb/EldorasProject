using System;

public class IntervalTimer : Timer
{
    private float duration;
    private float interval;
    private float nextTickTime;

    public event Action OnInterval;
    public int TickCount { get; private set; }

    public IntervalTimer(float duration, float tickInterval)
    {
        this.duration = duration;
        this.interval = tickInterval;
        CurrentTime = 0f;
        nextTickTime = tickInterval;
    }

    public override bool IsFinished => CurrentTime >= duration;
    public override float Progress => CurrentTime / duration;

    public override void Tick(float deltaTime)
    {
        if (!IsRunning || IsFinished) return;

        CurrentTime += deltaTime;

        while (CurrentTime >= nextTickTime && CurrentTime < duration)
        {
            TickCount++;
            OnInterval?.Invoke();
            nextTickTime += interval;
        }

        if (IsFinished)
        {
            IsRunning = false;
            InvokeOnTimerFinished();
        }
    }

    public override void Reset()
    {
        CurrentTime = 0f;
        TickCount = 0;
        nextTickTime = interval;
        IsRunning = false;
    }

    public override void Reset(float newTime)
    {
        duration = newTime;
        Reset();
    }
}