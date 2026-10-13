using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// Account Manager — Phase 2 of the Persistence System
//
// Responsibilities:
// - Session state: holds ActiveAccountName for the duration of the session
// - Account registry: reads/writes accounts.json via ISaveProvider
// - Credential validation: SHA-256 hashes passwords locally before storage
// - Provider ownership: holds the active ISaveProvider, shared with SaveManager
//
// Architecture:
// - IGameManager child of ManagerBrain (auto-discovered via GetComponentsInChildren)
// - Priority 20 — initialises before SaveManager (priority 25)
// - ISaveProvider created here and injected into SaveManager during LateInitialize
//
// Server migration: swap LocalSaveProvider for RemoteSaveProvider in the inspector.
// No other files change.

namespace CrabSystem
{
    public class AccountManager : MonoBehaviour, IGameManager
    {
        public string ManagerName => "Account Manager";
        public int InitializationPriority => 20;
        public bool IsEnabled => enabled;
        public bool IsInitialized { get; private set; }

        [Header("Provider")]
        [Tooltip("Which save provider to use. Swap to RemoteSaveProvider for server migration.")]
        [SerializeField] private SaveProviderType providerType = SaveProviderType.Local;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // Username of the currently logged-in account. Empty string if not logged in.
        public string ActiveAccountName { get; private set; } = string.Empty;

        // True when a successful Login() has been completed this session.
        public bool IsLoggedIn { get; private set; }

        // The active save provider. SaveManager reads this reference during LateInitialize.
        // Never null after Initialize() completes.
        public ISaveProvider SaveProvider { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            SaveProvider = CreateProvider();

            IsInitialized = true;

            if (debugLogging)
                Debug.Log($"[{ManagerName}] Initialized — provider: {SaveProvider.GetType().Name}");
        }

        public void LateInitialize()
        {
            // SaveManager picks up SaveProvider during its own LateInitialize.
            // Nothing to do here — provider is already set.
        }

        public void Shutdown()
        {
            Logout();

            if (debugLogging)
                Debug.Log($"[{ManagerName}] Shutdown");
        }

        public ValidationResult Validate()
        {
            var result = ValidationResult.Success();

            if (SaveProvider == null)
            {
                result.IsFatal = true;
                result.Errors.Add("SaveProvider is null after Initialize()");
            }

            return result;
        }

        // Register a new account. Password is SHA-256 hashed before storage — never plaintext.
        // Returns false if the username is already taken or if registration fails.
        public async Task<bool> Register(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (string.IsNullOrWhiteSpace(password)) return false;

            string hash = HashPassword(password);
            bool success = await SaveProvider.RegisterAccount(username, hash);

            if (debugLogging)
                Debug.Log($"[{ManagerName}] Register '{username}': {(success ? "SUCCESS" : "FAILED — username taken")}");

            return success;
        }

        // Validate credentials and start a session.
        // Password is SHA-256 hashed locally before the provider call — never sent plaintext.
        // Returns false if credentials are invalid.
        public async Task<bool> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            if (string.IsNullOrWhiteSpace(password)) return false;

            string hash = HashPassword(password);
            bool valid = await SaveProvider.ValidateAccount(username, hash);

            if (!valid)
            {
                if (debugLogging)
                    Debug.Log($"[{ManagerName}] Login '{username}': FAILED — invalid credentials");
                return false;
            }

            ActiveAccountName = username;
            IsLoggedIn = true;

            if (debugLogging)
                Debug.Log($"[{ManagerName}] Login '{username}': SUCCESS");

            return true;
        }

        // Clear session state. Does not delete any save data.
        public void Logout()
        {
            if (!IsLoggedIn) return;

            if (debugLogging)
                Debug.Log($"[{ManagerName}] Logout '{ActiveAccountName}'");

            ActiveAccountName = string.Empty;
            IsLoggedIn = false;
        }

        private ISaveProvider CreateProvider()
        {
            return providerType switch
            {
                SaveProviderType.Local => new LocalSaveProvider(),
                // RemoteSaveProvider slot — uncomment when built:
                // SaveProviderType.Remote => new RemoteSaveProvider(),
                _ => new LocalSaveProvider()
            };
        }

        // SHA-256 hash of the raw password. Produces a deterministic hex string.
        // Called before any provider interaction — passwords never leave this method unhashed.
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        [ContextMenu("Debug: Print Session State")]
        private void DebugPrintState()
        {
            Debug.Log($"=== ACCOUNT MANAGER ===\n" +
                      $"IsLoggedIn:        {IsLoggedIn}\n" +
                      $"ActiveAccountName: {ActiveAccountName}\n" +
                      $"Provider:          {SaveProvider?.GetType().Name ?? "null"}");
        }
    }

    // Inspector enum to select the active ISaveProvider implementation.
    public enum SaveProviderType
    {
        Local,
        Remote
    }
}
