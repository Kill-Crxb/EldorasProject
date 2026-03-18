using System;
using UnityEngine;

public abstract class Timer
{
    public float CurrentTime { get; protected set; }
    public bool IsRunning { get; protected set; }
    public abstract bool IsFinished { get; }
    public abstract float Progress { get; }

    public event Action OnTimerStart;
    public event Action OnTimerStop;
    public event Action OnTimerFinished;

    protected void InvokeOnTimerFinished()
    {
        OnTimerFinished?.Invoke();
    }

    public void Start()
    {
        IsRunning = true;
        OnTimerStart?.Invoke();
    }

    public void Pause() => IsRunning = false;
    public void Resume() => IsRunning = true;

    public void Stop()
    {
        IsRunning = false;
        OnTimerStop?.Invoke();
    }

    public abstract void Tick(float deltaTime);
    public abstract void Reset();
    public abstract void Reset(float newTime);
}