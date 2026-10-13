using System.Collections.Generic;
using UnityEngine;
using CrabSystem.UI;

namespace CrabSystem
{
    public class NameplateManager : MonoBehaviour
    {
        private static NameplateManager instance;
        public static NameplateManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<NameplateManager>(FindObjectsInactive.Exclude);
                return instance;
            }
        }

        [Header("Nameplate Prefab")]
        [SerializeField] private GameObject nameplatePrefab;
        [SerializeField] private Transform worldSpaceParent;

        private ControllerBrain playerBrain;
        private readonly List<NPCNameplate> registeredNameplates = new List<NPCNameplate>();

        // The player spawns after this object starts, so it's looked up the first time someone asks.
        public ControllerBrain PlayerBrain
        {
            get
            {
                if (playerBrain == null) playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
                return playerBrain;
            }
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        public void SpawnNameplate(ControllerBrain npcBrain)
        {
            if (!enabled) return;
            if (nameplatePrefab == null) { Debug.LogError("[NameplateManager] Nameplate prefab not assigned!"); return; }
            if (npcBrain == null) return;

            Transform parent = worldSpaceParent != null ? worldSpaceParent : transform;
            GameObject go = Instantiate(nameplatePrefab, parent);
            go.name = $"Nameplate_{npcBrain.name}";

            var nameplate = go.GetComponent<NPCNameplate>();
            if (nameplate == null)
            {
                Debug.LogError("[NameplateManager] Nameplate prefab missing NPCNameplate component!", nameplatePrefab);
                Destroy(go);
                return;
            }

            Register(nameplate);
            nameplate.Initialize(npcBrain, PlayerBrain);
        }

        public void Register(NPCNameplate nameplate)
        {
            if (nameplate == null || registeredNameplates.Contains(nameplate)) return;
            registeredNameplates.Add(nameplate);
            if (PlayerBrain != null) nameplate.SetTargetPlayer(PlayerBrain);
        }

        public void Unregister(NPCNameplate nameplate) => registeredNameplates.Remove(nameplate);

        public void BroadcastFactionRefresh()
        {
            foreach (var nameplate in registeredNameplates)
                if (nameplate != null) nameplate.UpdateDisplay();
        }
    }
}
