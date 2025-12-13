using UnityEngine;
using CrabThirdPerson.Character;

public class IdentityProviderCoordinator : ProviderCoordinator
{
    [Header("Player Identity (Optional)")]
    [SerializeField] private MonoBehaviour playerInfoProvider;
    [SerializeField] private MonoBehaviour identityHandlerProvider;
    [SerializeField] private MonoBehaviour playerFactionProvider;

    [Header("NPC Identity (Optional)")]
    [SerializeField] private MonoBehaviour npcModuleProvider;

    [Header("Shared Identity")]
    [SerializeField] private MonoBehaviour modelProvider;
    [SerializeField] private MonoBehaviour saveSystemProvider;

    private IPlayerInfoHandler playerInfo;
    private IdentityHandler identityHandler;
    private PlayerFactionHandler playerFaction;
    private NPCModule npcModule;
    private ModelModule model;
    private SaveSystemModule saveSystem;

    public IPlayerInfoHandler PlayerInfo => playerInfo;
    public IdentityHandler IdentityHandler => identityHandler;
    public PlayerFactionHandler PlayerFaction => playerFaction;
    public NPCModule NPCModule => npcModule;
    public ModelModule Model => model;
    public SaveSystemModule SaveSystem => saveSystem;

    public bool IsPlayer => playerInfo != null;
    public bool IsNPC => npcModule != null;

    protected override bool ValidateSlots()
    {
        bool valid = true;

        if (playerInfoProvider != null)
            valid &= ValidateOptionalProvider<IPlayerInfoHandler>(playerInfoProvider, "Player Info Provider");

        if (identityHandlerProvider != null)
            valid &= ValidateOptionalProvider<IdentityHandler>(identityHandlerProvider, "Identity Handler");

        if (playerFactionProvider != null)
            valid &= ValidateOptionalProvider<PlayerFactionHandler>(playerFactionProvider, "Player Faction");

        if (npcModuleProvider != null)
            valid &= ValidateOptionalProvider<NPCModule>(npcModuleProvider, "NPC Module");

        if (modelProvider != null)
            valid &= ValidateOptionalProvider<ModelModule>(modelProvider, "Model Module");

        if (saveSystemProvider != null)
            valid &= ValidateOptionalProvider<SaveSystemModule>(saveSystemProvider, "Save System");

        return valid;
    }

    protected override void CacheProviders()
    {
        playerInfo = playerInfoProvider as IPlayerInfoHandler;
        identityHandler = identityHandlerProvider as IdentityHandler;
        playerFaction = playerFactionProvider as PlayerFactionHandler;
        npcModule = npcModuleProvider as NPCModule;
        model = modelProvider as ModelModule;
        saveSystem = saveSystemProvider as SaveSystemModule;
    }

    protected override void OnInitialized()
    {
        if (playerInfoProvider is IBrainModule m1) m1.Initialize(brain);
        if (identityHandlerProvider is IBrainModule m2) m2.Initialize(brain);
        if (playerFactionProvider is IBrainModule m3) m3.Initialize(brain);
        if (npcModuleProvider is IBrainModule m4) m4.Initialize(brain);
        if (modelProvider is IBrainModule m5) m5.Initialize(brain);
        if (saveSystemProvider is IBrainModule m6) m6.Initialize(brain);
    }
}