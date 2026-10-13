/// <summary>
/// GameSceneBootstrapper
///
/// Attach to any persistent GameObject in the game scene (e.g. ManagerBrain or a
/// dedicated SceneBootstrap object).
///
/// Fires GameEvents.GameSceneReady() from Start(), which runs after all Awake()
/// calls in the scene — giving ControllerBrain and SaveManager time to register
/// before the load is triggered.
///
/// Replaces the SceneLoader coroutine approach, which was unreliable because
/// SceneLoader lives in the menu scene and is destroyed before its coroutine
/// can fire GameSceneReady after scene activation.
/// </summary>
public class GameSceneBootstrapper : UnityEngine.MonoBehaviour
{
    private void Start()
    {
        GameEvents.GameSceneReady();
    }
}