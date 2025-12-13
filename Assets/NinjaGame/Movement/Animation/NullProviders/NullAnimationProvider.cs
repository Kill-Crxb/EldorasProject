using UnityEngine;

public class NullAnimationProvider : MonoBehaviour, IAnimationProvider
{
    public void SetTrigger(string triggerName) { }
    public void SetBool(string paramName, bool value) { }
    public void SetFloat(string paramName, float value) { }
    public void SetInteger(string paramName, int value) { }

    public bool GetBool(string paramName) => false;
    public float GetFloat(string paramName) => 0f;
    public int GetInteger(string paramName) => 0;
    public void TriggerCombatAnimation(string triggerName)
    {
        // Null implementation - does nothing
    }
    public AnimatorStateInfo GetCurrentStateInfo(int layerIndex = 0) => new AnimatorStateInfo();
    public bool IsInTransition(int layerIndex = 0) => false;

    public void Play(string stateName, int layerIndex = 0) { }
    public void CrossFade(string stateName, float transitionDuration, int layerIndex = 0) { }
}