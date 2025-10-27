using UnityEngine;
using UnityEngine.InputSystem;
using CrabThirdPerson.Character;

public class ModelSwapTester : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool enableTesting = true;
    [SerializeField] private InputActionReference swapAction;

    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = true;

    private ModelModule modelModule;
    private ModelDatabase database;
    private int currentModelIndex = 0;

    void Start()
    {
        // Find ModelModule (should be on the same GameObject)
        modelModule = GetComponent<ModelModule>();

        if (modelModule == null)
        {
            Debug.LogError("ModelSwapTester: No ModelModule found on this GameObject!");
            enabled = false;
            return;
        }

        // Get the database from ModelModule
        database = GetModelDatabase();

        if (database == null)
        {
            Debug.LogError("ModelSwapTester: No ModelDatabase assigned to ModelModule!");
            enabled = false;
            return;
        }

        // Set up input action if not assigned
        if (swapAction == null)
        {
            CreateSwapAction();
        }

        // Find current model index
        FindCurrentModelIndex();
    }

    void OnEnable()
    {
        if (swapAction != null && swapAction.action != null)
        {
            swapAction.action.Enable();
            swapAction.action.performed += OnSwapPerformed;
        }
    }

    void OnDisable()
    {
        if (swapAction != null && swapAction.action != null)
        {
            swapAction.action.performed -= OnSwapPerformed;
            swapAction.action.Disable();
        }
    }

    private void OnSwapPerformed(InputAction.CallbackContext context)
    {
        if (enableTesting && context.performed)
        {
            SwapToNextModel();
        }
    }

    void CreateSwapAction()
    {
        // Create a simple keyboard action for Z key
        var action = new InputAction("ModelSwap", InputActionType.Button);
        action.AddBinding("<Keyboard>/z");

        // Enable the action
        action.Enable();
        action.performed += OnSwapPerformed;
    }

    void SwapToNextModel()
    {
        var availableModels = database.AllModels;
        if (availableModels.Length <= 1)
        {
            return;
        }

        // Move to next model (loop back to 0 if at end)
        currentModelIndex = (currentModelIndex + 1) % availableModels.Length;
        var nextModel = availableModels[currentModelIndex];

        // Skip invalid models
        while (!nextModel.IsValid() && currentModelIndex < availableModels.Length)
        {
            currentModelIndex = (currentModelIndex + 1) % availableModels.Length;
            nextModel = availableModels[currentModelIndex];
        }

        if (nextModel.IsValid())
        {
            modelModule.SwapModel(nextModel.modelId);
        }
    }

    void FindCurrentModelIndex()
    {
        if (database == null) return;

        string currentId = modelModule.CurrentModelId;
        var availableModels = database.AllModels;

        for (int i = 0; i < availableModels.Length; i++)
        {
            if (availableModels[i].modelId == currentId)
            {
                currentModelIndex = i;
                return;
            }
        }

        // If not found, start at 0
        currentModelIndex = 0;
    }

    string GetCurrentModelName()
    {
        if (database == null) return "Unknown";

        var availableModels = database.AllModels;
        if (currentModelIndex >= 0 && currentModelIndex < availableModels.Length)
        {
            return availableModels[currentModelIndex].displayName;
        }

        return "Unknown";
    }

    // Get database through reflection (since it might be private)
    ModelDatabase GetModelDatabase()
    {
        var field = typeof(ModelModule).GetField("modelDatabase",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            return field.GetValue(modelModule) as ModelDatabase;
        }

        Debug.LogWarning("ModelSwapTester: Could not access ModelDatabase from ModelModule");
        return null;
    }

    void DisplayDebugInfo()
    {
        if (database == null) return;

        var availableModels = database.AllModels;
        string info = $"Model: {currentModelIndex + 1}/{availableModels.Length} - {GetCurrentModelName()}";

        // Display on screen (simple GUI)
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        string inputText = swapAction != null ? "Use assigned input action" : "Press Z key";
        GUI.Label(new Rect(10, 10, 400, 30), $"{inputText} to swap models", style);
        GUI.Label(new Rect(10, 35, 400, 30), info, style);

        // Show available models
        GUI.Label(new Rect(10, 65, 400, 30), "Available Models:", style);
        for (int i = 0; i < availableModels.Length; i++)
        {
            var model = availableModels[i];
            string prefix = (i == currentModelIndex) ? "-> " : "   ";
            string status = model.IsValid() ? "" : " (Invalid)";
            GUI.Label(new Rect(10, 90 + (i * 20), 400, 20),
                $"{prefix}{model.displayName}{status}", style);
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo || !enableTesting) return;

        DisplayDebugInfo();
    }

    // Public methods for other scripts to use
    public void SwapToModel(string modelId)
    {
        if (modelModule != null)
        {
            modelModule.SwapModel(modelId);
            FindCurrentModelIndex();
        }
    }

    public void SwapToRandomModel()
    {
        if (database == null) return;

        var randomModel = database.GetRandomModel();
        if (randomModel != null && randomModel.IsValid())
        {
            modelModule.SwapModel(randomModel.modelId);
            FindCurrentModelIndex();
        }
    }

    public string[] GetAvailableModelNames()
    {
        if (database == null) return new string[0];

        var models = database.AllModels;
        string[] names = new string[models.Length];

        for (int i = 0; i < models.Length; i++)
        {
            names[i] = models[i].displayName;
        }

        return names;
    }

    // Inspector helper
    [ContextMenu("Swap to Next Model")]
    void SwapToNextModelContextMenu()
    {
        SwapToNextModel();
    }

    [ContextMenu("Swap to Random Model")]
    void SwapToRandomModelContextMenu()
    {
        SwapToRandomModel();
    }
}