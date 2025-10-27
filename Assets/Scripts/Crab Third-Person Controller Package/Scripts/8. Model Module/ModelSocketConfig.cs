using System.Linq;
using UnityEngine;


namespace CrabThirdPerson.Character
{
    [CreateAssetMenu(fileName = "ModelSocketConfig", menuName = "Character/Model Socket Configuration")]
    public class ModelSocketConfig : ScriptableObject
    {
        [System.Serializable]
        public class SocketDefinition
        {
            [Header("Socket Info")]
            public string socketName;
            [Tooltip("Hierarchical path from model root (e.g., 'Armature/Hand/WeaponSocket')")]
            public string socketPath;
            [Tooltip("Optional description of what this socket is used for")]
            public string description;

            [Header("Validation")]
            [Tooltip("Is this socket required for the model to function properly?")]
            public bool isRequired = false;

            public bool IsValid()
            {
                return !string.IsNullOrEmpty(socketName) && !string.IsNullOrEmpty(socketPath);
            }
        }

        [Header("Weapon Sockets")]
        [SerializeField] private string weaponSocketPath = "Armature/Hand/WeaponSocket";
        [SerializeField] private string shieldSocketPath = "Armature/Hand_L/ShieldSocket";
        [SerializeField] private string bowSocketPath = "Armature/Spine/BowSocket";

        [Header("Equipment Sockets")]
        [SerializeField] private string helmetSocketPath = "Armature/Head/HelmetSocket";
        [SerializeField] private string chestSocketPath = "Armature/Spine/ChestSocket";
        [SerializeField] private string bootsSocketPath = "Armature/Foot/BootsSocket";
        [SerializeField] private string glovesSocketPath = "Armature/Hand/GlovesSocket";
        [SerializeField] private string beltSocketPath = "Armature/Spine/BeltSocket";

        [Header("Effect Points")]
        [SerializeField] private string feetEffectsPath = "Armature/Foot/FeetEffects";
        [SerializeField] private string backEffectsPath = "Armature/Spine/BackEffects";
        [SerializeField] private string headEffectsPath = "Armature/Head/HeadEffects";
        [SerializeField] private string handEffectsPath = "Armature/Hand/HandEffects";

        [Header("Special Sockets")]
        [SerializeField] private string capeSocketPath = "Armature/Spine/CapeSocket";
        [SerializeField] private string necklaceSocketPath = "Armature/Neck/NecklaceSocket";
        [SerializeField] private string ringSocketPath = "Armature/Hand/RingSocket";

        [Header("Custom Socket Definitions")]
        [SerializeField] private SocketDefinition[] customSockets = new SocketDefinition[0];

        [Header("Configuration Settings")]
        [SerializeField] private bool validateSocketsOnLoad = true;
        [SerializeField] private bool showMissingSocketWarnings = true;

        // Properties for easy access
        public string WeaponSocketPath => weaponSocketPath;
        public string ShieldSocketPath => shieldSocketPath;
        public string BowSocketPath => bowSocketPath;
        public string HelmetSocketPath => helmetSocketPath;
        public string ChestSocketPath => chestSocketPath;
        public string BootsSocketPath => bootsSocketPath;
        public string GlovesSocketPath => glovesSocketPath;
        public string BeltSocketPath => beltSocketPath;
        public string FeetEffectsPath => feetEffectsPath;
        public string BackEffectsPath => backEffectsPath;
        public string HeadEffectsPath => headEffectsPath;
        public string HandEffectsPath => handEffectsPath;
        public string CapeSocketPath => capeSocketPath;
        public string NecklaceSocketPath => necklaceSocketPath;
        public string RingSocketPath => ringSocketPath;
        public SocketDefinition[] CustomSockets => customSockets;

        #region Socket Management

        /// <summary>
        /// Gets all socket definitions including built-in and custom sockets
        /// </summary>
        public SocketDefinition[] GetAllSocketDefinitions()
        {
            var allSockets = new System.Collections.Generic.List<SocketDefinition>();

            // Add built-in sockets
            allSockets.AddRange(GetBuiltInSocketDefinitions());

            // Add custom sockets
            if (customSockets != null)
            {
                foreach (var socket in customSockets)
                {
                    if (socket != null && socket.IsValid())
                        allSockets.Add(socket);
                }
            }

            return allSockets.ToArray();
        }

        /// <summary>
        /// Gets built-in socket definitions
        /// </summary>
        private SocketDefinition[] GetBuiltInSocketDefinitions()
        {
            return new SocketDefinition[]
            {
                new SocketDefinition { socketName = "weapon", socketPath = weaponSocketPath, description = "Primary weapon attachment point", isRequired = false },
                new SocketDefinition { socketName = "shield", socketPath = shieldSocketPath, description = "Shield attachment point", isRequired = false },
                new SocketDefinition { socketName = "bow", socketPath = bowSocketPath, description = "Bow attachment point when not in use", isRequired = false },
                new SocketDefinition { socketName = "helmet", socketPath = helmetSocketPath, description = "Head equipment socket", isRequired = false },
                new SocketDefinition { socketName = "chest", socketPath = chestSocketPath, description = "Chest equipment socket", isRequired = false },
                new SocketDefinition { socketName = "boots", socketPath = bootsSocketPath, description = "Foot equipment socket", isRequired = false },
                new SocketDefinition { socketName = "gloves", socketPath = glovesSocketPath, description = "Hand equipment socket", isRequired = false },
                new SocketDefinition { socketName = "belt", socketPath = beltSocketPath, description = "Belt equipment socket", isRequired = false },
                new SocketDefinition { socketName = "feeteffects", socketPath = feetEffectsPath, description = "Foot particle effects spawn point", isRequired = false },
                new SocketDefinition { socketName = "backeffects", socketPath = backEffectsPath, description = "Back particle effects spawn point", isRequired = false },
                new SocketDefinition { socketName = "headeffects", socketPath = headEffectsPath, description = "Head particle effects spawn point", isRequired = false },
                new SocketDefinition { socketName = "handeffects", socketPath = handEffectsPath, description = "Hand particle effects spawn point", isRequired = false },
                new SocketDefinition { socketName = "cape", socketPath = capeSocketPath, description = "Cape attachment point", isRequired = false },
                new SocketDefinition { socketName = "necklace", socketPath = necklaceSocketPath, description = "Necklace attachment point", isRequired = false },
                new SocketDefinition { socketName = "ring", socketPath = ringSocketPath, description = "Ring attachment point", isRequired = false }
            };
        }

        /// <summary>
        /// Gets a socket path by name
        /// </summary>
        public string GetSocketPath(string socketName)
        {
            if (string.IsNullOrEmpty(socketName))
                return null;

            var lowerName = socketName.ToLower();

            // Check built-in sockets first
            switch (lowerName)
            {
                case "weapon": return weaponSocketPath;
                case "shield": return shieldSocketPath;
                case "bow": return bowSocketPath;
                case "helmet": return helmetSocketPath;
                case "chest": return chestSocketPath;
                case "boots": return bootsSocketPath;
                case "gloves": return glovesSocketPath;
                case "belt": return beltSocketPath;
                case "feeteffects": return feetEffectsPath;
                case "backeffects": return backEffectsPath;
                case "headeffects": return headEffectsPath;
                case "handeffects": return handEffectsPath;
                case "cape": return capeSocketPath;
                case "necklace": return necklaceSocketPath;
                case "ring": return ringSocketPath;
            }

            // Check custom sockets
            if (customSockets != null)
            {
                foreach (var socket in customSockets)
                {
                    if (socket != null && socket.socketName.ToLower() == lowerName)
                        return socket.socketPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a socket is required
        /// </summary>
        public bool IsSocketRequired(string socketName)
        {
            if (string.IsNullOrEmpty(socketName))
                return false;

            var allSockets = GetAllSocketDefinitions();
            var socket = System.Array.Find(allSockets, s => s.socketName.ToLower() == socketName.ToLower());

            return socket != null && socket.isRequired;
        }

        /// <summary>
        /// Gets all required socket names
        /// </summary>
        public string[] GetRequiredSocketNames()
        {
            var allSockets = GetAllSocketDefinitions();
            var requiredSockets = new System.Collections.Generic.List<string>();

            foreach (var socket in allSockets)
            {
                if (socket.isRequired)
                    requiredSockets.Add(socket.socketName);
            }

            return requiredSockets.ToArray();
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates this socket configuration against a model
        /// </summary>
        public ValidationResult ValidateAgainstModel(GameObject model)
        {
            var result = new ValidationResult();

            if (model == null)
            {
                result.isValid = false;
                result.errors.Add("Model is null");
                return result;
            }

            var allSockets = GetAllSocketDefinitions();

            foreach (var socketDef in allSockets)
            {
                var socketTransform = FindSocketInModel(model, socketDef.socketPath);

                if (socketTransform == null)
                {
                    if (socketDef.isRequired)
                    {
                        result.isValid = false;
                        result.errors.Add($"Required socket '{socketDef.socketName}' not found at path: {socketDef.socketPath}");
                    }
                    else if (showMissingSocketWarnings)
                    {
                        result.warnings.Add($"Optional socket '{socketDef.socketName}' not found at path: {socketDef.socketPath}");
                    }
                }
                else
                {
                    result.foundSockets.Add(socketDef.socketName);
                }
            }

            return result;
        }

        /// <summary>
        /// Finds a socket transform in a model by path
        /// </summary>
        private Transform FindSocketInModel(GameObject model, string socketPath)
        {
            if (string.IsNullOrEmpty(socketPath))
                return null;

            var parts = socketPath.Split('/');
            Transform current = model.transform;

            foreach (var part in parts)
            {
                current = current.Find(part);
                if (current == null)
                    break;
            }

            return current;
        }

        /// <summary>
        /// Validates all custom socket definitions
        /// </summary>
        public void ValidateCustomSockets()
        {
            if (customSockets == null)
                return;

            int validCount = 0;
            int invalidCount = 0;

            for (int i = 0; i < customSockets.Length; i++)
            {
                var socket = customSockets[i];
                if (socket == null)
                {
                    invalidCount++;
                    Debug.LogWarning($"[ModelSocketConfig] Custom socket at index {i} is null");
                    continue;
                }

                if (socket.IsValid())
                {
                    validCount++;
                }
                else
                {
                    invalidCount++;
                    Debug.LogWarning($"[ModelSocketConfig] Invalid custom socket at index {i}: {socket.socketName}");

                    if (string.IsNullOrEmpty(socket.socketName))
                        Debug.LogWarning("  - Missing socket name");
                    if (string.IsNullOrEmpty(socket.socketPath))
                        Debug.LogWarning("  - Missing socket path");
                }
            }

            if (validCount > 0 || invalidCount > 0)
            {
                Debug.Log($"[ModelSocketConfig] Custom socket validation: {validCount} valid, {invalidCount} invalid");
            }
        }

        #endregion

        #region Unity Callbacks

        private void OnValidate()
        {
            if (validateSocketsOnLoad)
                ValidateCustomSockets();

            // Check for duplicate socket names in custom sockets
            if (customSockets != null)
            {
                var socketNames = new System.Collections.Generic.HashSet<string>();
                foreach (var socket in customSockets)
                {
                    if (socket != null && !string.IsNullOrEmpty(socket.socketName))
                    {
                        if (!socketNames.Add(socket.socketName.ToLower()))
                        {
                            Debug.LogWarning($"[ModelSocketConfig] Duplicate custom socket name: {socket.socketName}");
                        }
                    }
                }
            }
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        [ContextMenu("Reset to Default Paths")]
        public void ResetToDefaultPaths()
        {
            weaponSocketPath = "Armature/Hand/WeaponSocket";
            shieldSocketPath = "Armature/Hand_L/ShieldSocket";
            bowSocketPath = "Armature/Spine/BowSocket";
            helmetSocketPath = "Armature/Head/HelmetSocket";
            chestSocketPath = "Armature/Spine/ChestSocket";
            bootsSocketPath = "Armature/Foot/BootsSocket";
            glovesSocketPath = "Armature/Hand/GlovesSocket";
            beltSocketPath = "Armature/Spine/BeltSocket";
            feetEffectsPath = "Armature/Foot/FeetEffects";
            backEffectsPath = "Armature/Spine/BackEffects";
            headEffectsPath = "Armature/Head/HeadEffects";
            handEffectsPath = "Armature/Hand/HandEffects";
            capeSocketPath = "Armature/Spine/CapeSocket";
            necklaceSocketPath = "Armature/Neck/NecklaceSocket";
            ringSocketPath = "Armature/Hand/RingSocket";

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ModelSocketConfig] Reset to default socket paths");
        }

        [ContextMenu("Add Common Custom Sockets")]
        public void AddCommonCustomSockets()
        {
            var commonSockets = new SocketDefinition[]
            {
                new SocketDefinition { socketName = "quiver", socketPath = "Armature/Spine/QuiverSocket", description = "Arrow quiver attachment", isRequired = false },
                new SocketDefinition { socketName = "pouch", socketPath = "Armature/Hip/PouchSocket", description = "Item pouch attachment", isRequired = false },
                new SocketDefinition { socketName = "lantern", socketPath = "Armature/Hip/LanternSocket", description = "Light source attachment", isRequired = false },
                new SocketDefinition { socketName = "scroll", socketPath = "Armature/Spine/ScrollSocket", description = "Scroll case attachment", isRequired = false }
            };

            var socketList = new System.Collections.Generic.List<SocketDefinition>();
            if (customSockets != null)
                socketList.AddRange(customSockets);

            foreach (var newSocket in commonSockets)
            {
                // Check if socket already exists
                bool exists = socketList.Any(s => s != null && s.socketName.ToLower() == newSocket.socketName.ToLower());
                if (!exists)
                    socketList.Add(newSocket);
            }

            customSockets = socketList.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ModelSocketConfig] Added common custom sockets");
        }
#endif

        #endregion

        #region Supporting Classes

        [System.Serializable]
        public class ValidationResult
        {
            public bool isValid = true;
            public System.Collections.Generic.List<string> errors = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<string> warnings = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<string> foundSockets = new System.Collections.Generic.List<string>();

            public void LogResults(string modelName = "Unknown")
            {
                Debug.Log($"[ModelSocketConfig] Validation results for {modelName}:");
                Debug.Log($"  Valid: {isValid}");
                Debug.Log($"  Found sockets: {foundSockets.Count}");
                Debug.Log($"  Errors: {errors.Count}");
                Debug.Log($"  Warnings: {warnings.Count}");

                foreach (var error in errors)
                    Debug.LogError($"  ERROR: {error}");

                foreach (var warning in warnings)
                    Debug.LogWarning($"  WARNING: {warning}");
            }
        }

        #endregion
    }
}