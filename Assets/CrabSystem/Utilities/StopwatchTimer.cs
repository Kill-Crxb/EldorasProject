using System;

public class StopwatchTimer : Timer
{
    public override bool IsFinished => false;
    public override float Progress => CurrentTime;

    public override void Tick(float deltaTime)
    {
        if (!IsRunning) return;
        CurrentTime += deltaTime;
    }

    public override void Reset()
    {
        CurrentTime = 0f;
        IsRunning = false;
    }

    public override void Reset(float newTime)
    {
        CurrentTime = newTime;
        IsRunning = false;
    }
}