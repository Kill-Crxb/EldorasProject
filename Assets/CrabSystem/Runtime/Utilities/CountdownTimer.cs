using System;

public class CountdownTimer : Timer
{
    private float initialTime;

    public CountdownTimer(float duration)
    {
        initialTime = duration;
        CurrentTime = duration;
        IsRunning = true;  // Auto-start on creation (cooldowns start immediately)
    }

    public override bool IsFinished => CurrentTime <= 0f;
    public override float Progress => 1f - (CurrentTime / initialTime);

    public override void Tick(float deltaTime)
    {
        if (!IsRunning || IsFinished) return;

        CurrentTime -= deltaTime;

        if (CurrentTime <= 0f)
        {
            CurrentTime = 0f;
            IsRunning = false;
            InvokeOnTimerFinished();
        }
    }

    public override void Reset()
    {
        CurrentTime = initialTime;
        IsRunning = false;
    }

    public override void Reset(float newTime)
    {
        initialTime = newTime;
        CurrentTime = newTime;
        IsRunning = false;
    }
}