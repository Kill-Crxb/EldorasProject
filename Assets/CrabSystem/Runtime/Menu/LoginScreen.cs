using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoginScreen
///
/// Handles account registration and login.
/// On success, hides itself and shows the CharacterSelectScreen.
///
/// UI wiring (assign in Inspector):
///   usernameField      — TMP_InputField
///   passwordField      — TMP_InputField (content type: Password)
///   feedbackText       — TextMeshProUGUI  (error / status messages)
///   loginButton        — Button
///   registerButton     — Button
///   characterSelect    — GameObject (the CharacterSelectScreen panel)
/// </summary>
public class LoginScreen : MonoBehaviour
{
    [Header("Input Fields")]
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private TMP_InputField passwordField;

    [Header("Buttons")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;

    [Header("Feedback")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Navigation")]
    [SerializeField] private GameObject characterSelectPanel;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private AccountManager accountManager;
    private bool isBusy = false;

    void Start()
    {
        accountManager = ManagerBrain.Instance?.GetManager<AccountManager>();

        if (accountManager == null)
        {
            SetFeedback("Error: AccountManager not found.", isError: true);
            SetButtonsInteractable(false);
            return;
        }

        loginButton.onClick.AddListener(OnLoginClicked);
        registerButton.onClick.AddListener(OnRegisterClicked);

        ClearFeedback();
    }

    void OnDestroy()
    {
        loginButton?.onClick.RemoveListener(OnLoginClicked);
        registerButton?.onClick.RemoveListener(OnRegisterClicked);
    }

    // ── Button Handlers ───────────────────────────────────────────────────

    private void OnLoginClicked()
    {
        if (isBusy) return;
        _ = HandleLogin();
    }

    private void OnRegisterClicked()
    {
        if (isBusy) return;
        _ = HandleRegister();
    }

    // ── Async Operations ──────────────────────────────────────────────────

    private async Task HandleLogin()
    {
        if (!ValidateInputs()) return;

        SetBusy(true);
        SetFeedback("Logging in...");

        bool success = await accountManager.Login(usernameField.text.Trim(), passwordField.text);

        SetBusy(false);

        if (success)
        {
            if (debugLogging)
                Debug.Log($"[LoginScreen] Login successful: {usernameField.text.Trim()}");

            ShowCharacterSelect();
        }
        else
        {
            SetFeedback("Invalid username or password.", isError: true);
        }
    }

    private async Task HandleRegister()
    {
        if (!ValidateInputs()) return;

        SetBusy(true);
        SetFeedback("Creating account...");

        bool success = await accountManager.Register(usernameField.text.Trim(), passwordField.text);

        SetBusy(false);

        if (success)
        {
            if (debugLogging)
                Debug.Log($"[LoginScreen] Account created: {usernameField.text.Trim()}");

            // Auto-login after registration
            bool loginSuccess = await accountManager.Login(usernameField.text.Trim(), passwordField.text);

            if (loginSuccess)
                ShowCharacterSelect();
            else
                SetFeedback("Account created. Please log in.", isError: false);
        }
        else
        {
            SetFeedback("Username already taken or invalid.", isError: true);
        }
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private void ShowCharacterSelect()
    {
        gameObject.SetActive(false);

        if (characterSelectPanel != null)
            characterSelectPanel.SetActive(true);
        else
            Debug.LogError("[LoginScreen] CharacterSelectPanel not assigned!");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private bool ValidateInputs()
    {
        string username = usernameField?.text.Trim() ?? "";
        string password = passwordField?.text       ?? "";

        if (string.IsNullOrEmpty(username))
        {
            SetFeedback("Please enter a username.", isError: true);
            return false;
        }

        if (string.IsNullOrEmpty(password))
        {
            SetFeedback("Please enter a password.", isError: true);
            return false;
        }

        if (username.Length < 3)
        {
            SetFeedback("Username must be at least 3 characters.", isError: true);
            return false;
        }

        return true;
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        SetButtonsInteractable(!busy);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginButton    != null) loginButton.interactable    = interactable;
        if (registerButton != null) registerButton.interactable = interactable;
    }

    private void SetFeedback(string message, bool isError = false)
    {
        if (feedbackText == null) return;
        feedbackText.text  = message;
        feedbackText.color = isError ? Color.red : Color.white;
    }

    private void ClearFeedback()
    {
        if (feedbackText != null) feedbackText.text = "";
    }
}
