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

        private Transform npcTransform;
        private Camera mainCamera;
        private Canvas canvas;
        private ControllerBrain npcBrain;
        private ControllerBrain cachedPlayerBrain;

        private string npcName;
        private int npcLevel;
        private FactionType npcFaction;
        private FactionRelationship cachedRelationship;

        private float currentHealthPercent = 1f;
        private float targetHealthPercent = 1f;
        private System.Action<float> healthChangedCallback;
        private bool hasRefreshedAfterStart = false;

        public string EntityName => npcName;
        public int EntityLevel => npcLevel;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            mainCamera = Camera.main;

            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = mainCamera;
            }

            if (!showHealthBar && healthBarPanel != null)
                healthBarPanel.SetActive(false);
        }

        void OnEnable() => NameplateManager.Instance?.Register(this);
        void OnDisable() => NameplateManager.Instance?.Unregister(this);

        void Start()
        {
            if (nameText == null) Debug.LogError("[NPCNameplate] Name Text not assigned!", this);
            if (mainCamera == null) Debug.LogError("[NPCNameplate] Main Camera not found!", this);
        }

        void LateUpdate()
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
                transform.position = npcTransform.position + nameplateOffset;

            if (alwaysFaceCamera && mainCamera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);

            if (showHealthBar && healthBarFill != null && currentHealthPercent != targetHealthPercent)
            {
                currentHealthPercent = Mathf.Lerp(currentHealthPercent, targetHealthPercent, Time.deltaTime * healthBarUpdateSpeed);
                healthBarFill.fillAmount = currentHealthPercent;
            }
        }

        void OnDestroy()
        {
            UnsubscribeHealthCallback();
            NameplateManager.Instance?.Unregister(this);
        }

        public void Initialize(ControllerBrain brain, ControllerBrain playerBrain = null)
        {
            npcBrain = brain;
            npcTransform = brain.transform;
            cachedPlayerBrain = playerBrain ?? NameplateManager.Instance?.PlayerBrain;

            var identity = brain.Identity;
            if (identity != null)
            {
                npcName = identity.GetEntityName();
                npcLevel = identity.GetLevel();
                npcFaction = identity.GetFaction();
            }
            else
            {
                Debug.LogError($"[NPCNameplate] IdentitySystem missing on {brain.name}!", this);
                npcName = "Unknown";
                npcLevel = 1;
                npcFaction = FactionType.Neutral;
            }

            SubscribeHealthCallback(brain);
            UpdateDisplay();
        }

        public void Initialize(Transform npcTransform, string name, int level, FactionType faction, ControllerBrain targetPlayer = null)
        {
            this.npcTransform = npcTransform;
            npcName = name;
            npcLevel = level;
            npcFaction = faction;
            cachedPlayerBrain = targetPlayer ?? NameplateManager.Instance?.PlayerBrain;
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            FactionType playerFaction = GetPlayerFaction();
            cachedRelationship = FactionManager.GetRelationship(npcFaction, playerFaction);
            Color relationshipColor = FactionColors.GetRelationshipColor(cachedRelationship);

            if (nameText != null)
            {
                nameText.text = npcName;
                nameText.color = relationshipColor;
            }

            if (levelText != null)
            {
                levelText.text = $"(Lv.{npcLevel})";
                levelText.color = useLevelColorCoding ? GetLevelColor() : relationshipColor;
            }

            if (showHealthBar && healthBarFill != null)
                healthBarFill.color = relationshipColor;
        }

        public void UpdateHealth(float healthPercent)
        {
            targetHealthPercent = Mathf.Clamp01(healthPercent);
            if (showHealthBar && healthBarPanel != null)
                healthBarPanel.SetActive(true);
        }

        public void UpdateLevel(int newLevel) { npcLevel = newLevel; UpdateDisplay(); }
        public void UpdateFaction(FactionType f) { npcFaction = f; UpdateDisplay(); }

        public void UpdateName(string newName)
        {
            npcName = newName;
            if (nameText != null) nameText.text = npcName;
        }

        public void SetVisible(bool visible) { if (canvas != null) canvas.enabled = visible; }
        public void SetOffset(Vector3 offset) => nameplateOffset = offset;
        public FactionRelationship GetCachedRelationship() => cachedRelationship;

        public void SetTargetPlayer(ControllerBrain player)
        {
            cachedPlayerBrain = player;
            if (!string.IsNullOrEmpty(npcName)) UpdateDisplay();
        }

        private FactionType GetPlayerFaction()
        {
            if (cachedPlayerBrain == null) return FactionType.Player;
            var identity = cachedPlayerBrain.Identity;
            return identity != null ? identity.GetFaction() : FactionType.Player;
        }

        private int GetPlayerLevel()
        {
            if (cachedPlayerBrain == null) return 10;
            var identity = cachedPlayerBrain.Identity;
            return identity != null ? identity.GetLevel() : 10;
        }

        private Color GetLevelColor()
        {
            int diff = npcLevel - GetPlayerLevel();
            if (diff >= 10) return skullLevelColor;
            if (diff >= levelDifferenceForRed) return hardLevelColor;
            if (diff <= levelDifferenceForGreen) return easyLevelColor;
            return normalLevelColor;
        }

        private void SubscribeHealthCallback(ControllerBrain brain)
        {
            UnsubscribeHealthCallback();
            var resourceSystem = brain.ResourceSys;
            if (resourceSystem == null) return;
            healthChangedCallback = (_) => UpdateHealth(resourceSystem.GetHealthPercentage());
            resourceSystem.OnHealthChanged += healthChangedCallback;
            UpdateHealth(resourceSystem.GetHealthPercentage());
        }

        private void UnsubscribeHealthCallback()
        {
            if (npcBrain == null || healthChangedCallback == null) return;
            var resourceSystem = npcBrain.ResourceSys;
            if (resourceSystem != null) resourceSystem.OnHealthChanged -= healthChangedCallback;
            healthChangedCallback = null;
        }

        void OnDrawGizmosSelected()
        {
            if (npcTransform == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(npcTransform.position + nameplateOffset, 0.1f);
            Gizmos.DrawLine(npcTransform.position, npcTransform.position + nameplateOffset);
        }
    }
}