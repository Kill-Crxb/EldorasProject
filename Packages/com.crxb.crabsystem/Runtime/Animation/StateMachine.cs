using System;
using UnityEngine;

// Generic state machine container for managing a single state type.
// Handles transitions, events, and state tracking.
// This is the core building block - completely genre-agnostic.

namespace CrabSystem
{
    public class StateMachine<T> where T : struct, Enum
    {
        // Current and previous states
        private T currentState;
        private T previousState;

        // Timing
        private float stateEnterTime;
        private readonly EntityClock clock;

        // Events
        public event Action<T, T> OnStateChanged; // (previousState, newState)
        public event Action<T> OnStateEntered;    // (newState)
        public event Action<T> OnStateExited;     // (exitedState)

        // Properties
        public T Current => currentState;
        public T Previous => previousState;
        public float TimeInState => clock.Now - stateEnterTime;

        // Constructor - initialize with starting state
        public StateMachine(T initialState, EntityClock clock)
        {
            currentState = initialState;
            previousState = initialState;
            this.clock = clock;
            stateEnterTime = clock.Now;
        }

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
            stateEnterTime = clock.Now;

            // Fire enter event
            OnStateEntered?.Invoke(currentState);

            // Fire change event
            OnStateChanged?.Invoke(previousState, currentState);
        }

        // Check if currently in a specific state
        public bool IsInState(T state)
        {
            return currentState.Equals(state);
        }

        // Check if currently in any of the provided states
        public bool IsInAnyState(params T[] states)
        {
            foreach (T state in states)
            {
                if (currentState.Equals(state))
                    return true;
            }
            return false;
        }

        // Force set state without events (use sparingly - for initialization)
        public void ForceSet(T state)
        {
            currentState = state;
            previousState = state;
            stateEnterTime = clock.Now;
        }

        // Return to previous state
        public void ReturnToPrevious()
        {
            TransitionTo(previousState);
        }

        // Get state name as string
        public string GetCurrentStateName()
        {
            return currentState.ToString();
        }

        // Get previous state name as string
        public string GetPreviousStateName()
        {
            return previousState.ToString();
        }
    }
}
