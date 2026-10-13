using System;
using UnityEngine;

/// <summary>
/// Generic state machine container for managing a single state type.
/// Handles transitions, events, and state tracking.
/// This is the core building block - completely genre-agnostic.
/// </summary>
/// <typeparam name="T">Enum type representing states</typeparam>
public class StateMachine<T> where T : struct, Enum
{
    // Current and previous states
    private T currentState;
    private T previousState;

    // Timing
    private float stateEnterTime;

    // Events
    public event Action<T, T> OnStateChanged; // (previousState, newState)
    public event Action<T> OnStateEntered;    // (newState)
    public event Action<T> OnStateExited;     // (exitedState)

    // Properties
    public T Current => currentState;
    public T Previous => previousState;
    public float TimeInState => Time.time - stateEnterTime;

    /// <summary>
    /// Constructor - initialize with starting state
    /// </summary>
    public StateMachine(T initialState)
    {
        currentState = initialState;
        previousState = initialState;
        stateEnterTime = Time.time;
    }

    /// <summary>
    /// Transition to a new state
    /// </summary>
    public void TransitionTo(T newState)
    {
        // Check if already in this state
        if (currentState.Equals(newState))
            return;

        // Store previous state
        previousState = currentState;

        // Fire exit event
        OnStateExited?.Invoke(currentState);

        // Change state
        currentState = newState;
        stateEnterTime = Time.time;

        // Fire enter event
        OnStateEntered?.Invoke(currentState);

        // Fire change event
        OnStateChanged?.Invoke(previousState, currentState);
    }

    /// <summary>
    /// Check if currently in a specific state
    /// </summary>
    public bool IsInState(T state)
    {
        return currentState.Equals(state);
    }

    /// <summary>
    /// Check if currently in any of the provided states
    /// </summary>
    public bool IsInAnyState(params T[] states)
    {
        foreach (T state in states)
        {
            if (currentState.Equals(state))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Force set state without events (use sparingly - for initialization)
    /// </summary>
    public void ForceSet(T state)
    {
        currentState = state;
        previousState = state;
        stateEnterTime = Time.time;
    }

    /// <summary>
    /// Return to previous state
    /// </summary>
    public void ReturnToPrevious()
    {
        TransitionTo(previousState);
    }

    /// <summary>
    /// Get state name as string
    /// </summary>
    public string GetCurrentStateName()
    {
        return currentState.ToString();
    }

    /// <summary>
    /// Get previous state name as string
    /// </summary>
    public string GetPreviousStateName()
    {
        return previousState.ToString();
    }
}