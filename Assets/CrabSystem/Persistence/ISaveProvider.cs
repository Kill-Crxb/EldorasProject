using System.Threading.Tasks;

/// <summary>
/// All file or network I/O is behind this single interface.
/// SaveManager and AccountManager never touch the file system directly.
///
/// Architecture: Inject LocalSaveProvider now. Swap to RemoteSaveProvider
/// later with zero changes to any caller.
///
/// All methods are async Task from day one — local I/O awaits trivially;
/// network calls await meaningfully. No retrofit required at server migration.
/// </summary>
public interface ISaveProvider
{
    // ── Account Operations ────────────────────────────────────────────────

    /// <summary>
    /// Create a new account entry in accounts.json.
    /// Receives a pre-hashed password — never plaintext.
    /// Returns false if username already exists.
    /// </summary>
    Task<bool> RegisterAccount(string username, string passwordHash);

    /// <summary>
    /// Validate credentials against accounts.json.
    /// Receives the same hash produced client-side during login.
    /// Returns true only when username exists and hash matches.
    /// </summary>
    Task<bool> ValidateAccount(string username, string passwordHash);

    // ── Character Operations ──────────────────────────────────────────────

    /// <summary>
    /// Return all character IDs (folder names) available.
    /// accountName == null → return every character (open testing policy).
    /// accountName supplied → filter to that account's characters.
    /// </summary>
    Task<string[]> GetCharacters(string accountName);

    /// <summary>
    /// Permanently delete a character folder and all its files.
    /// Returns false if the character does not exist.
    /// </summary>
    Task<bool> DeleteCharacter(string characterId);

    // ── Per-File Save / Load ──────────────────────────────────────────────

    /// <summary>
    /// Write json to {characterId}/{filename}.json.
    /// Creates the character folder if it does not exist.
    /// Returns false on I/O failure.
    /// </summary>
    Task<bool> Save(string characterId, string filename, string json);

    /// <summary>
    /// Read {characterId}/{filename}.json and return its contents.
    /// Returns null if the file does not exist or on I/O failure.
    /// </summary>
    Task<string> Load(string characterId, string filename);
}