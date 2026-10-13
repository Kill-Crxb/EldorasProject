using UnityEditor;
using UnityEngine;

// Tools → Crab → Statuses → Set Up Control (12 Oct). Gives the existing statuses their crowd-control category and
// saving throw (Combat_Framework §11, Stat_Resolution §6), tags the hit states "hitstate" (not CC, kept out of the
// status bar and combat text), fills empty descriptions for the status bar's tooltip, makes a test Stunned (Hard,
// Toughness, an overhead aura).
// Rerunnable: it overwrites only the fields it sets, and never a description someone has written.
public static class ControlSetupBuilder
{
    const string Tag = "ControlSetup";
    const string Folder = "Assets/Database/Resources/Statuses";
    const string StunVfx = "Assets/Database/VFX/Prefab/Stun.prefab";

    static readonly (string asset, string text)[] Descriptions =
    {
        ("Status_Rooted", "Can't move or jump; can still act and turn."),
        ("Status_Silenced", "Can't cast techniques."),
        ("Status_Crippled", "Slowed; at more stacks it can't attack, block or act."),
        ("Status_Sundered", "Armour shield reduced, more per stack."),
        ("Status_Stoneskin", "An extra armour shield die."),
        ("Status_Hasted", "Sprint granted."),
        ("Status_Stunned", "Can't act or block."),
        ("HitStates/Status_Flinched", "Interrupted — can't act."),
        ("HitStates/Status_Staggered", "Can't act or block."),
        ("HitStates/Status_Prone", "Knocked down — can't dodge, sprint or jump."),
        ("HitStates/Status_Launched", "In the air — can't act or block."),
        ("HitStates/Status_GuardBroken", "Guard broken — open to the next hit."),
    };

    [MenuItem("Tools/Crab/Statuses/Set Up Control")]
    public static void Build()
    {
        Set("Status_Rooted", ControlCategory.Root, SaveType.Dexterity);
        Set("Status_Silenced", ControlCategory.SilenceDisarm, SaveType.Alteration);
        Set("Status_Crippled", ControlCategory.Snare, SaveType.Toughness);
        Set("Status_Sundered", ControlCategory.None, SaveType.Toughness);

        foreach (string name in new[] { "Status_Flinched", "Status_Staggered", "Status_Prone", "Status_Launched", "Status_GuardBroken" })
            AddTag(Load($"HitStates/{name}"), "hitstate");

        Downgrade("HitStates/Status_Prone", SaveType.Toughness, "HitStates/Status_Staggered");
        Downgrade("HitStates/Status_Launched", SaveType.Dexterity, "HitStates/Status_Flinched");

        Stunned();
        foreach ((string asset, string text) in Descriptions) Describe(Load(asset), text);
        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. Categories and saves set; hit states tagged; descriptions filled; Status_Stunned ready.");
    }

    // A test hard CC: 1.75 s of CannotAct and CannotBlock, Toughness save. Made once; later runs only re-set fields.
    public static StatusDefinition Stunned()
    {
        string path = $"{Folder}/Status_Stunned.asset";
        var status = AssetDatabase.LoadAssetAtPath<StatusDefinition>(path);
        if (status == null)
        {
            status = ScriptableObject.CreateInstance<StatusDefinition>();
            status.id = "stunned";
            status.displayName = "Stunned";
            status.beneficial = false;
            status.duration = 1.75f;
            status.blackboardFlags.Add(new StatusFlag { fact = "CannotAct", minStacks = 1 });
            status.blackboardFlags.Add(new StatusFlag { fact = "CannotBlock", minStacks = 1 });
            AssetDatabase.CreateAsset(status, path);
        }

        status.control = ControlCategory.Hard;
        status.save = SaveType.Toughness;
        status.auraAnchor = VFXAnchor.Overhead;
        if (status.auraVfx == null) status.auraVfx = AssetDatabase.LoadAssetAtPath<GameObject>(StunVfx);
        AddTag(status, "control");
        EditorUtility.SetDirty(status);
        return status;
    }

    static StatusDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<StatusDefinition>($"{Folder}/{name}.asset");

    static void Set(string name, ControlCategory control, SaveType save)
    {
        StatusDefinition status = Load(name);
        if (status == null)
        {
            Debug.LogWarning($"[{Tag}] {name} not found.");
            return;
        }

        status.control = control;
        status.save = save;
        EditorUtility.SetDirty(status);
    }

    static void Downgrade(string name, SaveType save, string downgrade)
    {
        StatusDefinition status = Load(name);
        if (status == null) return;

        status.save = save;
        status.saveDowngrade = Load(downgrade);
        EditorUtility.SetDirty(status);
    }

    static void Describe(StatusDefinition status, string text)
    {
        if (status == null || !string.IsNullOrEmpty(status.description)) return;

        status.description = text;
        EditorUtility.SetDirty(status);
    }

    static void AddTag(StatusDefinition status, string tag)
    {
        if (status == null || status.HasTag(tag)) return;

        status.tags.Add(tag);
        EditorUtility.SetDirty(status);
    }
}
