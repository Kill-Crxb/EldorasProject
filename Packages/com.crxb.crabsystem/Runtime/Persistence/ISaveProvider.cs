using System.Threading.Tasks;

// All file or network I/O is behind this single interface.
// SaveManager and AccountManager never touch the file system directly.
//
// Architecture: Inject LocalSaveProvider now. Swap to RemoteSaveProvider
// later with zero changes to any caller.
//
// All methods are async Task from day one — local I/O awaits trivially;
// network calls await meaningfully. No retrofit required at server migration.

namespace CrabSystem
{
    public interface ISaveProvider
    {
        // ── Account Operations ────────────────────────────────────────────────

        // Create a new account entry in accounts.json.
        // Receives a pre-hashed password — never plaintext.
        // Returns false if username already exists.
        Task<bool> RegisterAccount(string username, string passwordHash);

        // Validate credentials against accounts.json.
        // Receives the same hash produced client-side during login.
        // Returns true only when username exists and hash matches.
        Task<bool> ValidateAccount(string username, string passwordHash);

        // ── Character Operations ──────────────────────────────────────────────

        // Return all character IDs (folder names) available.
        // accountName == null → return every character (open testing policy).
        // accountName supplied → filter to that account's characters.
        Task<string[]> GetCharacters(string accountName);

        // Permanently delete a character folder and all its files.
        // Returns false if the character does not exist.
        Task<bool> DeleteCharacter(string characterId);

        // ── Per-File Save / Load ──────────────────────────────────────────────

        // Write json to {characterId}/{filename}.json.
        // Creates the character folder if it does not exist.
        // Returns false on I/O failure.
        Task<bool> Save(string characterId, string filename, string json);

        // Synchronous write for shutdown, where an async write can be cut off by the process exiting.
        bool SaveNow(string characterId, string filename, string json);

        // Read {characterId}/{filename}.json and return its contents.
        // Returns null if the file does not exist or on I/O failure.
        Task<string> Load(string characterId, string filename);
    }
}
