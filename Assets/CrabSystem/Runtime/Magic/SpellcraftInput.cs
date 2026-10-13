using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NinjaGame.Magic
{
    /// <summary>One key, one element. Explicit rather than positional so the list can be reordered.</summary>
    [Serializable]
    public class ElementKeyBinding
    {
        public SpellElement element = SpellElement.Fire;
        public Key key = Key.Numpad1;
    }

    /// <summary>
    /// TEMPORARY. Drives SpellcraftSystem from the keyboard so the grammar can be played before
    /// the element hotbar page exists. Delete this component the day it does.
    ///
    /// It is a separate component rather than part of SpellcraftSystem for the same reason
    /// IAbilityControlSource is separate from AbilitySystem: what decides to cast and what knows
    /// how to cast are different jobs, and keeping them apart is what lets an AI, a test, or a
    /// hotbar drive the same system.
    ///
    /// Reads Keyboard.current directly rather than going through PlayerInputControls, because
    /// adding actions to the generated input asset for a throwaway driver is a worse trade than
    /// a dozen lines here. That also means these keys DO NOT respect the UI input router — typing
    /// in a text field will still enter elements. Another reason this is temporary.
    ///
    /// Defaults are the numpad, because 1-6 are already bound to Hotbar1-6 in
    /// PlayerInputControls and a collision would fire a hotbar slot on every element press.
    /// </summary>
    public class SpellcraftInput : MonoBehaviour, IBrainModule
    {
        [Header("Module Settings")]
        [SerializeField] private bool isEnabled = true;

        [Header("Bindings")]
        [Tooltip("Numpad by default — the number row is taken by Hotbar1-9.")]
        [SerializeField]
        private ElementKeyBinding[] elementKeys =
        {
            new ElementKeyBinding { element = SpellElement.Fire,   key = Key.Numpad1 },
            new ElementKeyBinding { element = SpellElement.Water,  key = Key.Numpad2 },
            new ElementKeyBinding { element = SpellElement.Air,    key = Key.Numpad3 },
            new ElementKeyBinding { element = SpellElement.Earth,  key = Key.Numpad4 },
            new ElementKeyBinding { element = SpellElement.Nature, key = Key.Numpad5 },
            new ElementKeyBinding { element = SpellElement.Aether, key = Key.Numpad6 },
        };

        [Tooltip("Resolve and cast the sequence.")]
        [SerializeField] private Key castKey = Key.NumpadEnter;

        [Tooltip("Drop the last element.")]
        [SerializeField] private Key backKey = Key.NumpadMinus;

        [Tooltip("Abandon the sequence.")]
        [SerializeField] private Key clearKey = Key.NumpadPeriod;

        private ControllerBrain brain;
        private SpellcraftSystem spellcraft;

        public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
        }

        public void LateInitialize()
        {
            // ControllerBrain updates every module on every entity, NPCs included. Keyboard input
            // on an NPC would let the player cast through whatever they are standing next to.
            if (!brain.IsPlayer)
            {
                isEnabled = false;
                return;
            }

            spellcraft = brain.GetModule<SpellcraftSystem>();

            if (spellcraft == null)
            {
                isEnabled = false;
                Debug.LogError($"[SpellcraftInput] No SpellcraftSystem on {brain.EntityName} — nothing to drive.", this);
            }
        }

        public void UpdateModule()
        {
            if (!isEnabled) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < elementKeys.Length; i++)
            {
                ElementKeyBinding binding = elementKeys[i];

                if (binding != null && keyboard[binding.key].wasPressedThisFrame)
                    spellcraft.PushElement(binding.element);
            }

            if (keyboard[castKey].wasPressedThisFrame) spellcraft.Cast();
            if (keyboard[backKey].wasPressedThisFrame) spellcraft.PopElement();
            if (keyboard[clearKey].wasPressedThisFrame) spellcraft.ClearSequence();
        }
    }
}
