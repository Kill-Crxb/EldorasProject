// Game-wide moments only: no single character owns them. Per-character news goes on the
// character's own modules (brain.OnLoaded, DamageSystem events), never through here.
using System;

namespace CrabSystem
{
    public static class GameEvents
    {
        public static event Action<string> OnCharacterSelected;
        public static event Action OnLoadCompleted;
        public static event Action OnGameSceneReady;

        public static event Action OnSaveRequested;

        public static event Action<string> OnCameraModeChangeRequested;

        public static event Action<ControllerBrain, ControllerBrain> OnDialogueStarted;
        public static event Action<ControllerBrain> OnDialogueEnded;

        // A chest, corpse or vendor asks to be shown to a viewer; the window manager listens, so gameplay never
        // reaches for the UI.
        public static event Action<ControllerBrain, ControllerBrain> OnContainerOpenRequested;

        public static void CharacterSelected(string characterId) =>
            OnCharacterSelected?.Invoke(characterId);

        public static void LoadCompleted() =>
            OnLoadCompleted?.Invoke();

        public static void GameSceneReady() =>
            OnGameSceneReady?.Invoke();

        public static void SaveRequested() =>
            OnSaveRequested?.Invoke();

        public static void CameraModeChangeRequested(string modeName) =>
            OnCameraModeChangeRequested?.Invoke(modeName);

        public static void DialogueStarted(ControllerBrain npc, ControllerBrain actor) =>
            OnDialogueStarted?.Invoke(npc, actor);

        public static void DialogueEnded(ControllerBrain npc) =>
            OnDialogueEnded?.Invoke(npc);

        public static void ContainerOpenRequested(ControllerBrain viewer, ControllerBrain container) =>
            OnContainerOpenRequested?.Invoke(viewer, container);

        public static void ClearAll()
        {
            OnCharacterSelected = null;
            OnLoadCompleted = null;
            OnGameSceneReady = null;
            OnSaveRequested = null;
            OnCameraModeChangeRequested = null;
            OnDialogueStarted = null;
            OnDialogueEnded = null;
            OnContainerOpenRequested = null;
        }
    }
}
