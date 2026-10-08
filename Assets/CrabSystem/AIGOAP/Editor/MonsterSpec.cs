using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// One monster as Tools → AI → Build Monsters reads it. The builder makes this asset with defaults the
// first time; after that the asset is the source, and a rebuild keeps your edits. Abilities and goals
// are their own assets — edit those directly.
[CreateAssetMenu(fileName = "Monster_Spec", menuName = "CrabSystem/AI/Monster Spec")]
public class MonsterSpec : ScriptableObject
{
    [Header("Identity")]
    public string monsterId;
    public string displayName;
    [IdRef(IdKind.Model)] public string modelId;

    [Header("Model")]
    [Tooltip("The FBX the clips live in. Its attack clip gets the animation events below.")]
    public GameObject sourceModel;
    [Tooltip("The look: one of the pack's coloured prefabs.")]
    public GameObject sourcePrefab;
    [Tooltip("Clip names are this plus idle, move, attack, damage, die.")]
    public string clipPrefix;
    [Tooltip("Height of the model in metres. The builder scales it to fit.")]
    public float height = 0.7f;
    [Tooltip("Metres off the ground — for flyers.")]
    public float lift;
    [Tooltip("Turn the model if it faces the wrong way.")]
    public float yaw;

    [Header("Attack")]
    [Tooltip("Animator trigger for the attack state. The attack abilities use it.")]
    public string attackTrigger;
    [Tooltip("Playback speed of the attack clip. Under 1 telegraphs.")]
    public float attackSpeed = 1f;

    [Header("Attack clip events — 0 to 1 through the clip, below 0 for none")]
    [Tooltip("Tell: the read, in the wind-up. None tells as the attack starts.")]
    public float tell = -1f;
    [Tooltip("Cue(1): the ability's effects — a lunge's push, a bolt's release.")]
    [FormerlySerializedAs("effect1")] public float cue = -1f;
    [Tooltip("Strike(0): the hit check.")]
    [FormerlySerializedAs("hitboxStart")] public float strike = 0.4f;
    public float unlock = 0.95f;

    [Header("Natural weapon reach")]
    [Tooltip("Metres, edge to edge from the monster's body. Strikes are a range, facing and height check, no hitbox.")]
    public float reach = 1f;

    [Header("Body")]
    public float health = 30f;
    public float runSpeed = 4f;
    public float aggroRange = 14f;

    [Header("Natural weapon dice — 0 dice for none")]
    public int diceCount;
    public int diceFaces = 4;
    public int diceFlat;

    [Header("Behaviour")]
    public List<AIRoleAbility> abilities = new List<AIRoleAbility>();
    public List<GOAPGoal> goals = new List<GOAPGoal>();

    [Tooltip("Wire Goal_React (made by Build Goal Test Variants) into the GOAP interrupt slot: step away when hit, punish whiffs.")]
    public bool reactions = true;
}
