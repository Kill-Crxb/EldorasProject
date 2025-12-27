using System;

public class FrequencyTimer : Timer
{
    private float interval;
    private float ticksPerSecond;

    public event Action OnInterval;
    public int TickCount { get; private set; }

    public FrequencyTimer(float ticksPerSecond)
    {
        this.ticksPerSecond = ticksPerSecond;
        interval = 1f / ticksPerSecond;
        CurrentTime = 0f;
    }

    public override bool IsFinished => false;
    public override float Progress => (CurrentTime % interval) / interval;

    public override void Tick(float deltaTime)
    {
        if (!IsRunning) return;

        CurrentTime += deltaTime;

        while (CurrentTime >= interval)
        {
            CurrentTime -= interval;
            TickCount++;
            OnInterval?.Invoke();
        }
    }

    public override void Reset()
    {
        CurrentTime = 0f;
        TickCount = 0;
        IsRunning = false;
    }

    public override void Reset(float newTime)
    {
        ticksPerSecond = newTime;
        interval = 1f / newTime;
        Reset();
    }
}