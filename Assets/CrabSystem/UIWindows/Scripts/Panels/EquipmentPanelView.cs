using UnityEngine;

/// <summary>
/// Binds every authored equipment socket beneath this view to the player's equipment.
/// Sockets carry their own slot id, so adding or moving one needs no code change.
/// </summary>
public class EquipmentPanelView : UIPanelView
{
    [Header("Source")]
    [Tooltip("Leave empty to bind the player found at runtime.")]
    [SerializeField] private ControllerBrain sourceBrain;

    private EquipmentSocket[] sockets;

    protected override void OnShown()
    {
        if (sourceBrain == null) sourceBrain = PlayerBrainAccess.Find();

        if (sourceBrain == null)
        {
            Debug.LogWarning($"[EquipmentPanelView] no player brain for {name}");
            return;
        }

        if (!sourceBrain.IsInitialized)
        {
            sourceBrain.OnInitialized += HandleBrainReady;
            return;
        }

        Bind(sourceBrain);
    }

    protected override void OnHidden()
    {
        if (sourceBrain != null) sourceBrain.OnInitialized -= HandleBrainReady;

        if (sockets == null) return;

        for (int i = 0; i < sockets.Length; i++)
            sockets[i].Unbind();
    }

    private void HandleBrainReady(ControllerBrain ready)
    {
        ready.OnInitialized -= HandleBrainReady;
        Bind(ready);
    }

    private void Bind(ControllerBrain owner)
    {
        var system = owner.GetModule<EquipmentSystem>();

        if (system == null)
        {
            Debug.LogWarning($"[EquipmentPanelView] {owner.name} has no EquipmentSystem");
            return;
        }

        sockets = GetComponentsInChildren<EquipmentSocket>(true);

        for (int i = 0; i < sockets.Length; i++)
            sockets[i].Bind(system);
    }
}
