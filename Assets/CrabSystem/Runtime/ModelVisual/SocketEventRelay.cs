using System;
using System.Collections.Generic;

using UnityEngine;

// Moves a model's blade and sheath between sockets when its clips say so. The CombatGirls clips call
// SwitchSocket("To_Hand_R_Socket-Blade, To_Hand_L_Socket-Sheath"): each command names a named socket
// on this model's ModelSocketProvider and the part to put there. Presentation only: it moves
// transforms and writes no gameplay state, so every client replays it from the clip it plays.
//
// The blade is the equipped item's visual (the item owns its reach); the sheath belongs to the
// model. Between moves both sit at the stance's rest pose. A move opens the relay with its clip's
// first event (OpenSockets); the move's end (Unlocked) or an interruption (a ResetSockets event on
// reaction clips) closes it. Socket events from the tail of a clip still fading out after that are
// ignored, or the sheath could be left in the wrong hand. While the guard is up the blade's rest is
// the guard socket (the guard clips carry no socket events), so it snaps to the hand when the guard
// rises and back when it drops.
//
// Sits next to the Animator, because Unity only calls clip events there. A model without a socket a
// command names ignores the command, so every humanoid can play these clips without errors.
public class SocketEventRelay : MonoBehaviour, IModelPart
{
    private enum Part { None, Blade, Sheath }

    private struct Placement
    {
        public string socketId;
        public Part part;
    }

    [Tooltip("Equipment slot whose visual is the blade.")]
    [IdRef(IdKind.EquipmentSlot)] [SerializeField] private string weaponSlotId = "mainwep";

    [Tooltip("The model's own sheath. Leave empty on a model without one.")]
    [SerializeField] private Transform sheath;

    [Header("Rest pose (named sockets on ModelSocketProvider)")]
    [SerializeField] private string armedBladeSocket = "Katana_Close";
    [SerializeField] private string armedSheathSocket = "Hand_L_Socket";
    [SerializeField] private string unarmedBladeSocket = "Katana_Close";
    [SerializeField] private string unarmedSheathSocket = "Put_Socket_Katana";

    [Tooltip("Where the blade rests while the guard is up. Empty = it stays at the armed rest socket.")]
    [SerializeField] private string guardBladeSocket = "Hand_R_Socket";

    [Header("Command format")]
    [SerializeField] private string commandPrefix = "To_";
    [SerializeField] private string bladePart = "Blade";
    [SerializeField] private string sheathPart = "Sheath";

    private ModelSocketProvider sockets;
    private AnimationEventForwarder forwarder;
    private ModelModule model;
    private CombatStanceModule stance;
    private GuardModule guard;
    private bool moveOpen;

    // A clip set uses a handful of distinct command strings, so each is parsed once.
    private readonly Dictionary<string, Placement[]> parsed = new Dictionary<string, Placement[]>();

    private void Awake()
    {
        sockets = GetComponentInParent<ModelSocketProvider>();
        forwarder = GetComponent<AnimationEventForwarder>();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void Bind(ControllerBrain brain, ModelModule owner)
    {
        Unbind();

        model = owner;
        stance = brain.GetModule<CombatStanceModule>();
        guard = brain.GetModule<GuardModule>();

        if (stance != null) stance.OnStanceChanged += HandleStanceChanged;
        if (guard != null) guard.OnBlockStart += ResetSockets;
        if (guard != null) guard.OnBlockEnd += ResetSockets;
        if (forwarder != null) forwarder.OnMoveEvent += HandleMoveEvent;

        ResetSockets();
    }

    private void Unbind()
    {
        if (stance != null) stance.OnStanceChanged -= HandleStanceChanged;
        if (forwarder != null) forwarder.OnMoveEvent -= HandleMoveEvent;
        if (guard != null) guard.OnBlockStart -= ResetSockets;
        if (guard != null) guard.OnBlockEnd -= ResetSockets;

        stance = null;
        guard = null;
    }

    // Clip event: the first event of a move's clip.
    public void OpenSockets()
    {
        moveOpen = true;
    }

    // Clip event on reaction and dodge clips, and the end of every move: back to the rest pose.
    public void ResetSockets()
    {
        moveOpen = false;

        bool unarmed = stance != null && stance.IsUnarmed;
        Place(Blade(), RestBladeSocket(unarmed));
        Place(sheath, unarmed ? unarmedSheathSocket : armedSheathSocket);
    }

    // A parry's Unlocked resets mid-guard, so the guard decides the rest, not only its events.
    private string RestBladeSocket(bool unarmed)
    {
        if (unarmed) return unarmedBladeSocket;

        bool guarding = guard != null && guard.IsGuarding && !string.IsNullOrEmpty(guardBladeSocket);
        return guarding ? guardBladeSocket : armedBladeSocket;
    }

    // Clip event: "To_<socket>-<part>", comma separated.
    public void SwitchSocket(string commands)
    {
        if (!moveOpen || string.IsNullOrEmpty(commands)) return;

        foreach (var placement in Parse(commands))
            Place(PartTransform(placement.part), placement.socketId);
    }

    private void HandleStanceChanged(CombatStance _)
    {
        ResetSockets();
    }

    private void HandleMoveEvent(MoveEvent evt, int value)
    {
        if (evt == MoveEvent.Unlocked)
            ResetSockets();
    }

    private Placement[] Parse(string commands)
    {
        if (parsed.TryGetValue(commands, out var cached)) return cached;

        var list = new List<Placement>();
        foreach (string raw in commands.Split(','))
        {
            string command = raw.Trim();
            int dash = command.LastIndexOf('-');

            if (!command.StartsWith(commandPrefix, StringComparison.Ordinal)) continue;
            if (dash <= commandPrefix.Length) continue;

            list.Add(new Placement
            {
                socketId = command.Substring(commandPrefix.Length, dash - commandPrefix.Length),
                part = ToPart(command.Substring(dash + 1))
            });
        }

        Placement[] placements = list.ToArray();
        parsed[commands] = placements;
        return placements;
    }

    private Part ToPart(string name)
    {
        if (name == bladePart) return Part.Blade;
        if (name == sheathPart) return Part.Sheath;
        return Part.None;
    }

    private Transform PartTransform(Part part)
    {
        if (part == Part.Blade) return Blade();
        if (part == Part.Sheath) return sheath;
        return null;
    }

    private Transform Blade()
    {
        return model != null ? model.GetEquippedVisual(weaponSlotId) : null;
    }

    private void Place(Transform item, string socketId)
    {
        if (item == null || sockets == null) return;

        Transform socket = sockets.GetNamedSocket(socketId);
        if (socket == null || item.parent == socket) return;

        ModelModule.PlaceInSocket(item, socket);
    }
}
