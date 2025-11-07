using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RPG.Factions;

namespace RPG.NPC.UI
{
    [RequireComponent(typeof(Canvas))]
    public class NPCNameplate : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image healthBarFill;
        [SerializeField] private GameObject healthBarPanel;

        [Header("Settings")]
        [SerializeField] private Vector3 nameplateOffset = new Vector3(0, 2.5f, 0);
        [SerializeField] private bool alwaysFaceCamera = true;
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private float healthBarUpdateSpeed = 5f;

        [Header("Level Color Coding")]
        [SerializeField] private bool useLevelColorCoding = true;
        [SerializeField] private int levelDifferenceForGreen = -5;
        [SerializeField] private int levelDifferenceForRed = 5;
        [SerializeField] private Color easyLevelColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color normalLevelColor = Color.white;
        [SerializeField] private Color hardLevelColor = new Color(1f, 0.5f, 0.5f);
        [SerializeField] private Color skullLevelColor = new Color(1f, 0.2f, 0.2f);

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private Transform npcTransform;
        private Camera mainCamera;
        private Canvas canvas;
        private ControllerBrain targetPlayer;

        private string npcName;
        private int npcLevel;
        private FactionType npcFaction;
        private FactionRelationship cachedRelationship;

        private float currentHealthPercent = 1f;
        private float targetHealthPercent = 1f;

        private bool hasRefreshedAfterStart = false;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            mainCamera = Camera.main;

            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = mainCamera;
            }

            if (!showHealthBar && healthBarPanel != null)
            {
                healthBarPanel.SetActive(false);
            }
        }

        private void Start()
        {
            if (nameText == null)
            {
                Debug.LogError("[NPCNameplate] Name Text not assigned!", this);
            }

            if (levelText == null)
            {
                Debug.LogWarning("[NPCNameplate] Level Text not assigned. Level display disabled.", this);
            }

            if (mainCamera == null)
            {
                Debug.LogError("[NPCNameplate] Main Camera not found! Make sure camera has 'MainCamera' tag.", this);
            }
        }

        private void LateUpdate()
        {
            if (!hasRefreshedAfterStart && Time.frameCount > 5)
            {
                FactionType playerFaction = GetPlayerFaction();

                if (playerFaction != FactionType.Player || Time.frameCount > 10)
                {
                    hasRefreshedAfterStart = true;
                    UpdateDisplay();
                }
            }

            if (npcTransform != null)
            {
                transform.position = npcTransform.position + nameplateOffset;
            }

            if (alwaysFaceCamera && mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
            }

            if (showHealthBar && healthBarFill != null && currentHealthPercent != targetHealthPercent)
            {
                currentHealthPercent = Mathf.Lerp(currentHealthPercent, targetHealthPercent, Time.deltaTime * healthBarUpdateSpeed);
                healthBarFill.fillAmount = currentHealthPercent;
            }
        }

        public void Initialize(Transform npcTransform, string npcName, int npcLevel, FactionType npcFaction, ControllerBrain targetPlayer = null)
        {
            this.npcTransform = npcTransform;
            this.npcName = npcName;
            this.npcLevel = npcLevel;
            this.npcFaction = npcFaction;
            this.targetPlayer = targetPlayer;

            UpdateDisplay();

            if (enableDebugLogs)
            {
                Debug.Log($"[NPCNameplate] Initialized for {npcName} (Level {npcLevel}, Faction: {npcFaction})");
            }
        }

        public void UpdateDisplay()
        {
            FactionType playerFaction = GetPlayerFaction();

            cachedRelationship = FactionManager.GetRelationship(npcFaction, playerFaction);
            Color relationshipColor = FactionColors.GetRelationshipColor(cachedRelationship);

            Debug.Log($"<color=magenta>[NPCNameplate] {npcName}: NPC Faction={npcFaction}, Player Faction={playerFaction}, Relationship={cachedRelationship}, Color={relationshipColor}</color>");

            if (nameText != null)
            {
                nameText.text = npcName;
                nameText.color = relationshipColor;
            }

            if (levelText != null)
            {
                levelText.text = $"(Lv.{npcLevel})";

                if (useLevelColorCoding)
                {
                    levelText.color = GetLevelColor();
                }
                else
                {
                    levelText.color = relationshipColor;
                }
            }

            if (showHealthBar && healthBarFill != null)
            {
                healthBarFill.color = relationshipColor;
            }
        }

        public void UpdateHealth(float healthPercent)
        {
            targetHealthPercent = Mathf.Clamp01(healthPercent);

            if (showHealthBar && healthBarPanel != null)
            {
                healthBarPanel.SetActive(healthPercent < 1f);
            }
        }

        public void UpdateLevel(int newLevel)
        {
            npcLevel = newLevel;
            UpdateDisplay();
        }

        public void UpdateName(string newName)
        {
            npcName = newName;
            if (nameText != null)
            {
                nameText.text = npcName;
            }
        }

        public void UpdateFaction(FactionType newFaction)
        {
            npcFaction = newFaction;
            UpdateDisplay();
        }

        public void SetVisible(bool visible)
        {
            if (canvas != null)
            {
                canvas.enabled = visible;
            }
        }

        public void SetOffset(Vector3 offset)
        {
            nameplateOffset = offset;
        }

        public void SetTargetPlayer(ControllerBrain player)
        {
            targetPlayer = player;
            UpdateDisplay();
        }

        public FactionRelationship GetCachedRelationship()
        {
            return cachedRelationship;
        }

        private FactionType GetPlayerFaction()
        {
            ControllerBrain playerBrain = null;

            if (targetPlayer != null)
            {
                playerBrain = targetPlayer;
            }
            else
            {
                var allBrains = FindObjectsByType<ControllerBrain>(FindObjectsSortMode.None);

                foreach (var brain in allBrains)
                {
                    var playerInfo = brain.GetModule<PlayerInfoModule>();
                    if (playerInfo != null)
                    {
                        playerBrain = brain;
                        break;
                    }
                }

                if (playerBrain == null)
                {
                    if (enableDebugLogs)
                    {
                        Debug.LogWarning("[NPCNameplate] Could not find local player's ControllerBrain");
                    }
                    return FactionType.Player;
                }
            }

            var info = playerBrain.GetModule<PlayerInfoModule>();
            if (info != null)
            {
                if (info.FactionHandler != null)
                {
                    FactionType faction = info.GetPlayerFaction();
                    return faction;
                }
                else if (enableDebugLogs)
                {
                    Debug.LogWarning("[NPCNameplate] PlayerInfoModule.FactionHandler is NULL (not initialized yet)");
                }
            }

            return FactionType.Player;
        }

        private Color GetLevelColor()
        {
            int playerLevel = GetPlayerLevel();

            int levelDifference = npcLevel - playerLevel;

            if (levelDifference >= 10)
            {
                return skullLevelColor;
            }
            else if (levelDifference >= levelDifferenceForRed)
            {
                return hardLevelColor;
            }
            else if (levelDifference <= levelDifferenceForGreen)
            {
                return easyLevelColor;
            }
            else
            {
                return normalLevelColor;
            }
        }

        private int GetPlayerLevel()
        {
            var playerBrain = FindFirstObjectByType<ControllerBrain>();
            if (playerBrain != null)
            {
                var playerInfo = playerBrain.GetComponentInChildren<PlayerInfoModule>();
                if (playerInfo != null)
                {
                    return playerInfo.GetCharacterLevel();
                }
            }

            return 10;
        }

        private void OnDrawGizmosSelected()
        {
            if (npcTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(npcTransform.position + nameplateOffset, 0.1f);
                Gizmos.DrawLine(npcTransform.position, npcTransform.position + nameplateOffset);
            }
        }
    }
}