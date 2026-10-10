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

        [Tooltip("Posture (PostureModule): the bar grows from its pivot as posture builds and hides when " +
                 "empty. Set the fill's pivot X to 0.5 so it grows from the centre.")]
        [SerializeField] private RectTransform postureBarFill;
        [SerializeField] private GameObject postureBarPanel;
        [SerializeField] private Image postureBarImage;

        [Header("Settings")]
        [SerializeField] private Vector3 nameplateOffset = new Vector3(0, 2.5f, 0);
        [SerializeField] private bool alwaysFaceCamera = true;
        [SerializeField] private bool showHealthBar = true;
        [SerializeField] private float healthBarUpdateSpeed = 5f;
        [Tooltip("Health and posture bars show only while this NPC is the player's soft target (TargetingModule).")]
        [SerializeField] private bool barsOnlyOnTarget = true;
        [Tooltip("Seconds the bars stay up after the NPC stops being the target, so a sweep of the camera doesn't flicker them.")]
        [SerializeField] private float targetLinger = 1f;
        [SerializeField] private Color postureColor = new Color(1f, 0.75f, 0.2f);
        [SerializeField] private Color postureDangerColor = new Color(1f, 0.25f, 0.1f);
        [Tooltip("Posture at or above this (0–1) shows the danger colour: close to a guard break.")]
        [SerializeField] private float postureDanger = 0.75f;

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
        private FactionDefinition npcFaction;
        private FactionRelationship cachedRelationship;

        private float currentHealthPercent = 1f;
        private float targetHealthPercent = 1f;
        private System.Action<float> healthChangedCallback;
        private bool hasRefreshedAfterStart = false;
        private float targetedUntil = -999f;

        public string EntityName => npcName;
        public int EntityLevel => npcLevel;

        void Awake()
        {
            canvas = GetComponent<Canvas>();

            if (canvas != null)
                canvas.renderMode = RenderMode.WorldSpace;

            if (!showHealthBar && healthBarPanel != null)
                healthBarPanel.SetActive(false);

            TryResolveCamera();
        }

        void OnEnable() => NameplateManager.Instance?.Register(this);
        void OnDisable() => NameplateManager.Instance?.Unregister(this);

        void Start()
        {
            if (nameText == null) Debug.LogError("[NPCNameplate] Name Text not assigned!", this);
        }

        private void TryResolveCamera()
        {
            if (mainCamera != null) return;

            mainCamera = Camera.main;
            if (mainCamera != null && canvas != null)
                canvas.worldCamera = mainCamera;
        }

        void LateUpdate()
        {
            if (mainCamera == null)
                TryResolveCamera();

            if (!hasRefreshedAfterStart && Time.frameCount > 5)
            {
                FactionDefinition playerFaction = GetPlayerFaction();
                if (playerFaction != null || Time.frameCount > 10)
                {
                    hasRefreshedAfterStart = true;
                    UpdateDisplay();
                }
            }

            if (npcTransform != null)
                transform.position = npcTransform.position + nameplateOffset;

            if (alwaysFaceCamera && mainCamera != null)
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);

            bool bars = ShowBars();
            if (healthBarPanel != null && healthBarPanel.activeSelf != bars) healthBarPanel.SetActive(bars);
            UpdatePosture(bars);

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
                npcName = identity.DisplayName;
                npcLevel = identity.Level;
                npcFaction = identity.GetFaction();
            }
            else
            {
                Debug.LogError($"[NPCNameplate] IdentitySystem missing on {brain.EntityName}!", this);
                npcName = "Unknown";
                npcLevel = 1;
                npcFaction = null;
            }

            SubscribeHealthCallback(brain);
            UpdateDisplay();
        }

        public void Initialize(Transform npcTransform, string name, int level, FactionDefinition faction, ControllerBrain targetPlayer = null)
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
            FactionDefinition playerFaction = GetPlayerFaction();
            cachedRelationship = FactionManager.GetStance(npcFaction, playerFaction);
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
        }

        public void UpdateLevel(int newLevel) { npcLevel = newLevel; UpdateDisplay(); }
        public void UpdateFaction(FactionDefinition f) { npcFaction = f; UpdateDisplay(); }

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

        private FactionDefinition GetPlayerFaction()
        {
            if (cachedPlayerBrain == null) return null;
            var identity = cachedPlayerBrain.Identity;
            return identity != null ? identity.GetFaction() : null;
        }

        private int GetPlayerLevel()
        {
            if (cachedPlayerBrain == null) return 10;
            var identity = cachedPlayerBrain.Identity;
            return identity != null ? identity.Level : 10;
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
            var resourceSystem = brain.Resources;
            if (resourceSystem == null) return;
            healthChangedCallback = (_) => UpdateHealth(resourceSystem.GetHealthPercentage());
            resourceSystem.OnHealthChanged += healthChangedCallback;
            UpdateHealth(resourceSystem.GetHealthPercentage());
        }

        private void UnsubscribeHealthCallback()
        {
            if (npcBrain == null || healthChangedCallback == null) return;
            var resourceSystem = npcBrain.Resources;
            if (resourceSystem != null) resourceSystem.OnHealthChanged -= healthChangedCallback;
            healthChangedCallback = null;
        }

        private bool ShowBars()
        {
            if (!showHealthBar) return false;
            if (!barsOnlyOnTarget) return true;

            TargetingModule targeting = cachedPlayerBrain != null ? cachedPlayerBrain.GetModule<TargetingModule>() : null;
            if (targeting != null && npcBrain != null && targeting.CurrentTarget == npcBrain)
                targetedUntil = Time.time + targetLinger;
            return Time.time < targetedUntil;
        }

        private void UpdatePosture(bool bars)
        {
            if (postureBarFill == null || npcBrain == null) return;

            float posture = PostureOf(npcBrain);
            bool show = bars && posture > 0.01f;
            if (postureBarPanel != null && postureBarPanel.activeSelf != show) postureBarPanel.SetActive(show);

            postureBarFill.localScale = new Vector3(posture, 1f, 1f);
            if (postureBarImage != null) postureBarImage.color = posture >= postureDanger ? postureDangerColor : postureColor;
        }

        private static float PostureOf(ControllerBrain brain)
        {
            PostureModule posture = brain.GetModule<PostureModule>();
            return posture != null ? posture.Fraction : 0f;
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