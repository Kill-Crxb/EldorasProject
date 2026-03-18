using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Local-disk implementation of ISaveProvider.
/// Stores all save data under Application.persistentDataPath/Saves/.
///
/// File layout:
///   Saves/
///     accounts.json
///     {accountName}/
///       {characterId}/
///         metadata.json
///         inventory.json
///         equipment.json
///         stats.json
///
/// Server migration: Replace with RemoteSaveProvider — no other files change.
/// </summary>
public class LocalSaveProvider : ISaveProvider
{
    // ── Constants ─────────────────────────────────────────────────────────

    private const string SavesFolder = "Saves";
    private const string AccountsFile = "accounts.json";

    // ── Derived Paths ─────────────────────────────────────────────────────

    private readonly string savesRoot;
    private readonly string accountsFilePath;

    // ── Constructor ───────────────────────────────────────────────────────

    public LocalSaveProvider()
    {
        savesRoot = Path.Combine(Application.persistentDataPath, SavesFolder);
        accountsFilePath = Path.Combine(savesRoot, AccountsFile);

        EnsureDirectoryExists(savesRoot);
    }

    // ── Account Operations ────────────────────────────────────────────────

    public async Task<bool> RegisterAccount(string username, string passwordHash)
    {
        if (string.IsNullOrEmpty(username)) return false;
        if (string.IsNullOrEmpty(passwordHash)) return false;

        var registry = await LoadAccountRegistry();

        // Guard: username already taken
        if (registry.AccountExists(username)) return false;

        registry.accounts.Add(new AccountEntry
        {
            username = username,
            passwordHash = passwordHash,
            createdAt = DateTime.UtcNow.ToString("O")
        });

        return await WriteAccountRegistry(registry);
    }

    public async Task<bool> ValidateAccount(string username, string passwordHash)
    {
        if (string.IsNullOrEmpty(username)) return false;
        if (string.IsNullOrEmpty(passwordHash)) return false;

        var registry = await LoadAccountRegistry();
        return registry.Validate(username, passwordHash);
    }

    // ── Character Operations ──────────────────────────────────────────────

    public async Task<string[]> GetCharacters(string accountName)
    {
        await Task.Yield(); // Keep async contract consistent

        // Guard: saves root missing
        if (!Directory.Exists(savesRoot)) return Array.Empty<string>();

        // Open testing policy: null accountName → all characters across all accounts
        if (string.IsNullOrEmpty(accountName))
        {
            return CollectAllCharacterIds();
        }

        string accountFolder = Path.Combine(savesRoot, accountName);
        if (!Directory.Exists(accountFolder)) return Array.Empty<string>();

        return Directory.GetDirectories(accountFolder);
    }

    public async Task<bool> DeleteCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return false;

        string path = ResolveCharacterPath(characterId);

        await Task.Yield();

        if (!Directory.Exists(path)) return false;

        try
        {
            Directory.Delete(path, recursive: true);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveProvider] DeleteCharacter failed: {e.Message}");
            return false;
        }
    }

    // ── Per-File Save / Load ──────────────────────────────────────────────

    public async Task<bool> Save(string characterId, string filename, string json)
    {
        if (string.IsNullOrEmpty(characterId)) return false;
        if (string.IsNullOrEmpty(filename)) return false;
        if (string.IsNullOrEmpty(json)) return false;

        string characterPath = ResolveCharacterPath(characterId);
        EnsureDirectoryExists(characterPath);

        string filePath = Path.Combine(characterPath, $"{filename}.json");

        try
        {
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveProvider] Save failed ({characterId}/{filename}): {e.Message}");
            return false;
        }
    }

    public async Task<string> Load(string characterId, string filename)
    {
        if (string.IsNullOrEmpty(characterId)) return null;
        if (string.IsNullOrEmpty(filename)) return null;

        string filePath = Path.Combine(ResolveCharacterPath(characterId), $"{filename}.json");

        if (!File.Exists(filePath)) return null;

        try
        {
            return await File.ReadAllTextAsync(filePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveProvider] Load failed ({characterId}/{filename}): {e.Message}");
            return null;
        }
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Characters are stored under {savesRoot}/{accountName}/{characterId}.
    /// characterId encodes the accountName prefix (e.g. "Crxb/Hanzo_1740220800").
    /// </summary>
    private string ResolveCharacterPath(string characterId)
    {
        return Path.Combine(savesRoot, characterId);
    }

    private string[] CollectAllCharacterIds()
    {
        var characterIds = new System.Collections.Generic.List<string>();

        foreach (string accountFolder in Directory.GetDirectories(savesRoot))
        {
            foreach (string characterFolder in Directory.GetDirectories(accountFolder))
            {
                // Return as relative paths from savesRoot for portability
                string accountName = Path.GetFileName(accountFolder);
                string characterName = Path.GetFileName(characterFolder);
                characterIds.Add($"{accountName}/{characterName}");
            }
        }

        return characterIds.ToArray();
    }

    private async Task<AccountRegistry> LoadAccountRegistry()
    {
        if (!File.Exists(accountsFilePath))
            return new AccountRegistry();

        try
        {
            string json = await File.ReadAllTextAsync(accountsFilePath);
            return JsonUtility.FromJson<AccountRegistry>(json) ?? new AccountRegistry();
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveProvider] Failed to load accounts.json: {e.Message}");
            return new AccountRegistry();
        }
    }

    private async Task<bool> WriteAccountRegistry(AccountRegistry registry)
    {
        try
        {
            string json = JsonUtility.ToJson(registry, prettyPrint: true);
            await File.WriteAllTextAsync(accountsFilePath, json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[LocalSaveProvider] Failed to write accounts.json: {e.Message}");
            return false;
        }
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    // ── Data Structures ───────────────────────────────────────────────────

    [Serializable]
    private class AccountRegistry
    {
        public System.Collections.Generic.List<AccountEntry> accounts
            = new System.Collections.Generic.List<AccountEntry>();

        public bool AccountExists(string username)
        {
            foreach (var a in accounts)
                if (string.Equals(a.username, username, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        public bool Validate(string username, string passwordHash)
        {
            foreach (var a in accounts)
                if (string.Equals(a.username, username, StringComparison.OrdinalIgnoreCase)
                    && a.passwordHash == passwordHash)
                    return true;
            return false;
        }
    }

    [Serializable]
    private class AccountEntry
    {
        public string username;
        public string passwordHash;
        public string createdAt;
    }
}