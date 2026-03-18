using UnityEngine;

public interface IAnimationProvider
{
    void SetTrigger(string triggerName);
    void SetBool(string paramName, bool value);
    void SetFloat(string paramName, float value);
    void SetInteger(string paramName, int value);

    bool GetBool(string paramName);
    float GetFloat(string paramName);
    int GetInteger(string paramName);

    void TriggerCombatAnimation(string triggerName);

    AnimatorStateInfo GetCurrentStateInfo(int layerIndex = 0);
    bool IsInTransition(int layerIndex = 0);

    void Play(string stateName, int layerIndex = 0);
    void CrossFade(string stateName, float transitionDuration, int layerIndex = 0);
}