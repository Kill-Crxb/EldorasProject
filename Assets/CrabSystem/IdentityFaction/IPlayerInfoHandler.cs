using UnityEngine;

public interface IPlayerInfoHandler
{
    void Initialize(IdentitySystem identitySystem);
    void UpdateHandler();
    string GetHandlerSaveData();
    void LoadHandlerData(string json);
    void ResetHandler();
}