using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RPG.Factions;

namespace RPG.NPC.UI
{
    /// <summary>
    /// World-space nameplate that displays NPC name, level, and faction relationship.
    /// Auto-follows NPC position and rotates to face camera.
    /// Color-coded based on faction relationship to player.
    /// 
    /// Hierarchy Setup:
    /// NPCNameplate (this script + Canvas)
    /// └── Content (Panel with VerticalLayoutGroup)
    ///     ├── NameText (TextMeshProUGUI)
    ///     ├── LevelText (TextMeshProUGUI)
    ///     └── HealthBarPanel (optional)
    ///         ├── Background (Image)
    ///         └── Fill (Image - Filled type)
    /// 
    /// Usage:
    /// 1. Create nameplate prefab with this component
    /// 2. Assign to NPCModule's nameplatePrefab field
    /// 3. NPCModule will automatically create and initialize it
    /// </summary>
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
        [SerializeField] private int levelDifferenceForGreen = -5;  // 5+ levels below player = green
        [SerializeField] private int levelDifferenceForRed = 5;     // 5+ levels above player = red
        [SerializeField] private Color easyLevelColor = new Color(0.5f, 1f, 0.5f);    // Green (easy)
        [SerializeField] private Color normalLevelColor = Color.white;                 // White (normal)
        [SerializeField] private Color hardLevelColor = new Color(1f, 0.5f, 0.5f);    // Red (hard)
        [SerializeField] private Color skullLevelColor = new Color(1f, 0.2f, 0.2f);   // Bright red (skull level)

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Cached references
        private Transform npcTransform;
        private Camera mainCamera;
        private Canvas canvas;

        // NPC data
        private string npcName;
        private int npcLevel;
        private FactionType npcFaction;
        private FactionRelationship cachedRelationship;

        // Health tracking
        private float currentHealthPercent = 1f;
        private float targetHealthPercent = 1f;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            mainCamera = Camera.main;

            // Setup canvas for world space
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = mainCamera;
            }

            // Hide health bar initially if set to not show
            if (!showHealthBar && healthBarPanel != null)
            {
                healthBarPanel.SetActive(false);
            }
        }

        private void Start()
        {
            // Validate references
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
            // Update position to follow NPC
            if (npcTransform != null)
            {
                transform.position = npcTransform.position + nameplateOffset;
            }

            // Always face camera
            if (alwaysFaceCamera && mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
            }

            // Smooth health bar updates
            if (showHealthBar && healthBarFill != null && currentHealthPercent != targetHealthPercent)
            {
                currentHealthPercent = Mathf.Lerp(currentHealthPercent, targetHealthPercent, Time.deltaTime * healthBarUpdateSpeed);
                healthBarFill.fillAmount = currentHealthPercent;
            }
        }

        #region Public API

        /// <summary>
        /// Initialize the nameplate with NPC data.
        /// Called by NPCModule during initialization.
        /// </summary>
        /// <param name="npcTransform">The NPC's root transform to follow</param>
        /// <param name="npcName">Display name</param>
        /// <param name="npcLevel">NPC level</param>
        /// <param name="npcFaction">NPC's faction for color coding</param>
        public void Initialize(Transform npcTransform, string npcName, int npcLevel, FactionType npcFaction)
        {
            this.npcTransform = npcTransform;
            this.npcName = npcName;
            this.npcLevel = npcLevel;
            this.npcFaction = npcFaction;

            UpdateDisplay();

            if (enableDebugLogs)
            {
                Debug.Log($"[NPCNameplate] Initialized for {npcName} (Level {npcLevel}, Faction: {npcFaction})");
            }
        }

        /// <summary>
        /// Update the nameplate display with current information.
        /// Call this when faction relationship or other data changes.
        /// </summary>
        public void UpdateDisplay()
        {
            // Get relationship to player
            cachedRelationship = FactionManager.GetRelationship(npcFaction, FactionType.Player);
            Color relationshipColor = FactionColors.GetRelationshipColor(cachedRelationship);

            // Update name text
            if (nameText != null)
            {
                nameText.text = npcName;
                nameText.color = relationshipColor;
            }

            // Update level text
            if (levelText != null)
            {
                levelText.text = $"(Lv.{npcLevel})";

                // Apply level color coding if enabled
                if (useLevelColorCoding)
                {
                    levelText.color = GetLevelColor();
                }
                else
                {
                    levelText.color = relationshipColor;
                }
            }

            // Update health bar color to match faction
            if (showHealthBar && healthBarFill != null)
            {
                healthBarFill.color = relationshipColor;
            }
        }

        /// <summary>
        /// Update the health bar display (0.0 to 1.0).
        /// Call this when NPC takes damage or heals.
        /// </summary>
        /// <param name="healthPercent">Current health as percentage (0-1)</param>
        public void UpdateHealth(float healthPercent)
        {
            targetHealthPercent = Mathf.Clamp01(healthPercent);

            // Show health bar when damaged, hide when full
            if (showHealthBar && healthBarPanel != null)
            {
                healthBarPanel.SetActive(healthPercent < 1f);
            }
        }

        /// <summary>
        /// Update the NPC's level and refresh display.
        /// </summary>
        public void UpdateLevel(int newLevel)
        {
            npcLevel = newLevel;
            UpdateDisplay();
        }

        /// <summary>
        /// Update the NPC's name and refresh display.
        /// </summary>
        public void UpdateName(string newName)
        {
            npcName = newName;
            if (nameText != null)
            {
                nameText.text = npcName;
            }
        }

        /// <summary>
        /// Update the NPC's faction and refresh display.
        /// Call this when NPC changes faction dynamically.
        /// </summary>
        public void UpdateFaction(FactionType newFaction)
        {
            npcFaction = newFaction;
            UpdateDisplay();
        }

        /// <summary>
        /// Show or hide the entire nameplate.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (canvas != null)
            {
                canvas.enabled = visible;
            }
        }

        /// <summary>
        /// Set the nameplate offset from NPC position.
        /// Useful for differently sized NPCs.
        /// </summary>
        public void SetOffset(Vector3 offset)
        {
            nameplateOffset = offset;
        }

        /// <summary>
        /// Get current cached faction relationship
        /// </summary>
        public FactionRelationship GetCachedRelationship()
        {
            return cachedRelationship;
        }

        #endregion

        #region Level Color Coding

        /// <summary>
        /// Get the appropriate color for the level text based on player level difference.
        /// Green = Easy, White = Normal, Red = Hard, Bright Red = Skull
        /// </summary>
        private Color GetLevelColor()
        {
            // Get player level
            int playerLevel = GetPlayerLevel();

            int levelDifference = npcLevel - playerLevel;

            // Skull level (10+ levels above player) - extremely dangerous
            if (levelDifference >= 10)
            {
                return skullLevelColor;
            }
            // Hard level (5-9 levels above) - challenging
            else if (levelDifference >= levelDifferenceForRed)
            {
                return hardLevelColor;
            }
            // Easy level (5+ levels below) - trivial
            else if (levelDifference <= levelDifferenceForGreen)
            {
                return easyLevelColor;
            }
            // Normal level (within ±4 levels) - appropriate challenge
            else
            {
                return normalLevelColor;
            }
        }

        /// <summary>
        /// Get player level for comparison.
        /// Tries to find PlayerInfoModule, falls back to default.
        /// </summary>
        private int GetPlayerLevel()
        {
            // Try to find player's brain and get level
            var playerBrain = FindObjectOfType<ControllerBrain>();
            if (playerBrain != null)
            {
                var playerInfo = playerBrain.GetComponentInChildren<PlayerInfoModule>();
                if (playerInfo != null)
                {
                    return playerInfo.GetCharacterLevel();
                }
            }

            // Default to level 10 if player not found
            return 10;
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            if (npcTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(npcTransform.position + nameplateOffset, 0.1f);
                Gizmos.DrawLine(npcTransform.position, npcTransform.position + nameplateOffset);
            }
        }

        #endregion
    }
}