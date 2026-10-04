using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Dev only: lets a game scene be played straight from the editor. The managers normally arrive
// from the menu scene; when this scene starts without them, it spawns the Manager_Brain prefab,
// picks a character and starts the scene the way the menu would. After the menu it does nothing.
// Runs before everything else so PlayerSpawner finds the SaveManager in its Awake.
[DefaultExecutionOrder(-1000)]
public class SceneQuickStart : MonoBehaviour
{
    [SerializeField] private ManagerBrain managerBrainPrefab;
    [Tooltip("The scene's bootstrapper. Held back until the character is chosen, then started here.")]
    [SerializeField] private GameSceneBootstrapper bootstrapper;
    [Tooltip("Character to load, e.g. 'ass/guysix_1791091746'. Empty = the most recently played one.")]
    [SerializeField] private string characterId;

    private void Awake()
    {
        if (!Application.isEditor || ManagerBrain.Instance != null) return;

        if (managerBrainPrefab == null || bootstrapper == null)
        {
            Debug.LogError("[SceneQuickStart] Assign the Manager_Brain prefab and the scene's bootstrapper.", this);
            return;
        }

        Instantiate(managerBrainPrefab);
        bootstrapper.enabled = false;
        _ = StartWithCharacter();
    }

    private async Task StartWithCharacter()
    {
        var save = ManagerBrain.Instance.GetManager<SaveManager>();
        string id = string.IsNullOrEmpty(characterId) ? await LatestCharacterId(save) : characterId;

        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError("[SceneQuickStart] No saved character. Create one from the menu first.", this);
            return;
        }

        GameEvents.CharacterSelected(id);
        GameEvents.GameSceneReady();
    }

    private static async Task<string> LatestCharacterId(SaveManager save)
    {
        var characters = await save.GetAllCharacters();
        if (characters.Count == 0) return null;
        return characters.OrderByDescending(c => c.lastPlayedTime).First().characterId;
    }
}
