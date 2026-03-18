using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SceneLoader
///
/// Handles the MainMenu → Game scene transition.
/// Called by CharacterSelectScreen once a character is confirmed.
///
/// Flow:
///   1. CharacterSelectScreen calls SceneLoader.LoadGameScene(characterId)
///   2. SceneLoader fires GameEvents.OnCharacterSelected (SaveManager stores the id)
///   3. Async scene load begins (optional loading screen can hook here)
///   4. Once scene is fully active, fires GameEvents.OnGameSceneReady
///   5. SaveManager receives OnGameSceneReady and calls LoadCharacter()
///
/// Setup:
///   - Attach to a GameObject in the MainMenu scene (or ManagerBrain)
///   - Set gameSceneName to match your Game scene name in Build Settings
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Called by CharacterSelectScreen when the player confirms a character.
    /// </summary>
    public void LoadGameScene(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            Debug.LogError("[SceneLoader] LoadGameScene called with empty characterId!");
            return;
        }

        if (debugLogging)
            Debug.Log($"[SceneLoader] Loading game scene for character: {characterId}");

        // Broadcast selection — SaveManager stores it before scene unloads
        GameEvents.CharacterSelected(characterId);

        StartCoroutine(LoadSceneAsync(gameSceneName));
    }

    /// <summary>
    /// Return to the main menu from the game scene (e.g. logout or quit to menu).
    /// Saves first, then transitions.
    /// </summary>
    public void LoadMainMenu()
    {
        GameEvents.SaveRequested();
        StartCoroutine(LoadSceneAsync(mainMenuSceneName));
    }

    private IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        // Wait until nearly ready
        while (op.progress < 0.9f)
            yield return null;

        // Activate the scene
        op.allowSceneActivation = true;

        // Wait one frame for the scene to fully initialise
        yield return null;

        if (sceneName == gameSceneName)
        {
            if (debugLogging)
                Debug.Log("[SceneLoader] Game scene ready — firing OnGameSceneReady");

            GameEvents.GameSceneReady();
        }
    }
}
