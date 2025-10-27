using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace SceneAnalysis
{
    public class HierarchicalObjectInspector : EditorWindow
    {
        private Vector2 hierarchyScrollPosition;
        private Vector2 detailScrollPosition;
        private GameObject selectedObject;
        private List<GameObject> sceneObjects = new List<GameObject>();
        private string searchFilter = "";
        private bool includeInactiveObjects = true;
        private bool showPrivateFields = false;
        private bool showComponentDetails = true;
        private bool showBuiltInComponents = false;
        private bool expandAllByDefault = false;
        private int maxDepth = 5;
        private bool exportToFile = false;
        private string exportPath = "";
        private string inspectionReport = "";

        // For hierarchical display
        private GUIStyle hierarchyItemStyle;
        private GUIStyle selectedItemStyle;
        private GUIStyle componentStyle;
        private GUIStyle childStyle;

        [MenuItem("Tools/Scene Analysis/Hierarchical Object Inspector")]
        public static void ShowWindow()
        {
            var window = GetWindow<HierarchicalObjectInspector>("Hierarchical Object Inspector");
            window.minSize = new Vector2(900, 500);
            window.Show();
        }

        private void OnEnable()
        {
            RefreshSceneObjects();
            SetupStyles();
        }

        private void SetupStyles()
        {
            hierarchyItemStyle = new GUIStyle(EditorStyles.label);
            hierarchyItemStyle.normal.textColor = EditorStyles.label.normal.textColor;

            selectedItemStyle = new GUIStyle(EditorStyles.label);
            selectedItemStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node1 on.png") as Texture2D;
            selectedItemStyle.normal.textColor = Color.white;

            componentStyle = new GUIStyle(EditorStyles.miniLabel);
            componentStyle.normal.textColor = new Color(0.7f, 0.9f, 1f, 1f); // Light blue

            childStyle = new GUIStyle(EditorStyles.label);
            childStyle.normal.textColor = new Color(1f, 1f, 0.7f, 1f); // Light yellow
        }

        private void OnGUI()
        {
            if (hierarchyItemStyle == null) SetupStyles();

            EditorGUILayout.LabelField("Hierarchical Object Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Top controls
            EditorGUILayout.BeginHorizontal();

            // Search and basic options
            EditorGUILayout.BeginVertical(GUILayout.Width(250));
            searchFilter = EditorGUILayout.TextField("Search Filter:", searchFilter);
            includeInactiveObjects = EditorGUILayout.Toggle("Include Inactive Objects", includeInactiveObjects);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Scene"))
            {
                RefreshSceneObjects();
            }
            if (GUILayout.Button("Clear Selection"))
            {
                selectedObject = null;
                inspectionReport = "";
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Detail options
            EditorGUILayout.BeginVertical(GUILayout.Width(200));
            EditorGUILayout.LabelField("Inspection Options", EditorStyles.boldLabel);
            showComponentDetails = EditorGUILayout.Toggle("Show Component Details", showComponentDetails);
            showPrivateFields = EditorGUILayout.Toggle("Show Private Fields", showPrivateFields);
            showBuiltInComponents = EditorGUILayout.Toggle("Show Built-in Components", showBuiltInComponents);
            expandAllByDefault = EditorGUILayout.Toggle("Expand All by Default", expandAllByDefault);
            maxDepth = EditorGUILayout.IntSlider("Max Hierarchy Depth", maxDepth, 1, 10);
            EditorGUILayout.EndVertical();

            // Export options
            EditorGUILayout.BeginVertical();
            exportToFile = EditorGUILayout.Toggle("Export to File", exportToFile);
            if (exportToFile)
            {
                EditorGUILayout.BeginHorizontal();
                exportPath = EditorGUILayout.TextField("Path:", exportPath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string path = EditorUtility.SaveFilePanel("Save Hierarchical Report",
                        Application.dataPath, "HierarchicalInspection", "txt");
                    if (!string.IsNullOrEmpty(path))
                    {
                        exportPath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);

            // Main content area - split view
            EditorGUILayout.BeginHorizontal();

            // Left panel - Object selector
            EditorGUILayout.BeginVertical("box", GUILayout.Width(300), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Select Object to Inspect", EditorStyles.boldLabel);

            hierarchyScrollPosition = EditorGUILayout.BeginScrollView(hierarchyScrollPosition);
            DrawObjectSelector();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Right panel - Hierarchical inspection
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (selectedObject != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Hierarchical Analysis: {selectedObject.name}", EditorStyles.boldLabel);
                if (GUILayout.Button("Generate Full Report", GUILayout.Width(150)))
                {
                    GenerateHierarchicalReport();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition);

                if (!string.IsNullOrEmpty(inspectionReport))
                {
                    GUIStyle textStyle = new GUIStyle(EditorStyles.textArea);
                    textStyle.wordWrap = false;
                    textStyle.font = EditorStyles.miniFont;
                    textStyle.fontSize = 10;

                    inspectionReport = EditorGUILayout.TextArea(inspectionReport, textStyle,
                        GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                }
                else
                {
                    EditorGUILayout.HelpBox("Click 'Generate Full Report' to see the complete hierarchical breakdown", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.LabelField("Select an object to see its hierarchical structure", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void RefreshSceneObjects()
        {
            sceneObjects.Clear();

            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var rootObjects = currentScene.GetRootGameObjects();

            foreach (var rootObj in rootObjects.OrderBy(obj => obj.name))
            {
                CollectObjectsRecursive(rootObj, sceneObjects);
            }
        }

        private void CollectObjectsRecursive(GameObject obj, List<GameObject> collection)
        {
            if (!includeInactiveObjects && !obj.activeInHierarchy)
                return;

            collection.Add(obj);

            for (int i = 0; i < obj.transform.childCount; i++)
            {
                CollectObjectsRecursive(obj.transform.GetChild(i).gameObject, collection);
            }
        }

        private void DrawObjectSelector()
        {
            var filteredObjects = sceneObjects;

            if (!string.IsNullOrEmpty(searchFilter))
            {
                filteredObjects = sceneObjects.Where(obj =>
                    obj.name.ToLower().Contains(searchFilter.ToLower())).ToList();
            }

            foreach (var obj in filteredObjects)
            {
                DrawSelectorItem(obj);
            }
        }

        private void DrawSelectorItem(GameObject obj)
        {
            if (obj == null) return;

            EditorGUILayout.BeginHorizontal();

            // Calculate indentation based on hierarchy depth
            int depth = GetHierarchyDepth(obj);
            GUILayout.Space(depth * 15);

            // Object selection button
            GUIStyle itemStyle = obj == selectedObject ? selectedItemStyle : hierarchyItemStyle;

            string displayName = obj.name;
            if (!obj.activeInHierarchy)
                displayName += " (Inactive)";

            if (GUILayout.Button(displayName, itemStyle, GUILayout.ExpandWidth(true)))
            {
                selectedObject = obj;
                inspectionReport = ""; // Clear previous report
                Selection.activeGameObject = obj; // Also select in Unity's hierarchy
            }

            EditorGUILayout.EndHorizontal();
        }

        private int GetHierarchyDepth(GameObject obj)
        {
            int depth = 0;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }
            return depth;
        }

        private void GenerateHierarchicalReport()
        {
            if (selectedObject == null) return;

            var report = new StringBuilder();

            report.AppendLine($"=== HIERARCHICAL OBJECT INSPECTION ===");
            report.AppendLine($"Root Object: {selectedObject.name}");
            report.AppendLine($"Full Path: {GetFullPath(selectedObject)}");
            report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Max Depth: {maxDepth}");
            report.AppendLine();

            // Start recursive analysis
            AnalyzeObjectRecursively(selectedObject, report, 0);

            inspectionReport = report.ToString();

            // Export if requested
            if (exportToFile && !string.IsNullOrEmpty(exportPath))
            {
                try
                {
                    File.WriteAllText(exportPath, inspectionReport);
                    EditorUtility.DisplayDialog("Export Complete",
                        $"Hierarchical inspection exported to:\n{exportPath}", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Export Failed",
                        $"Failed to export report:\n{e.Message}", "OK");
                }
            }
        }

        private void AnalyzeObjectRecursively(GameObject obj, StringBuilder report, int depth)
        {
            if (depth >= maxDepth || obj == null) return;
            if (!includeInactiveObjects && !obj.activeInHierarchy) return;

            string indent = new string(' ', depth * 4);
            string objectPrefix = depth == 0 ? "■ " : "├── ";

            // Object header
            string activeStatus = obj.activeInHierarchy ? "" : " (INACTIVE)";
            report.AppendLine($"{indent}{objectPrefix}OBJECT: {obj.name}{activeStatus}");

            // Object details
            report.AppendLine($"{indent}    Path: {GetFullPath(obj)}");
            report.AppendLine($"{indent}    Layer: {LayerMask.LayerToName(obj.layer)} | Tag: {obj.tag}");

            // Transform info
            var transform = obj.transform;
            report.AppendLine($"{indent}    Position: {transform.position}");
            report.AppendLine($"{indent}    Rotation: {transform.rotation.eulerAngles}");
            report.AppendLine($"{indent}    Scale: {transform.localScale}");
            report.AppendLine();

            // Attached Components
            report.AppendLine($"{indent}    ┌── ATTACHED COMPONENTS:");
            var components = obj.GetComponents<Component>();

            if (components.Length == 0)
            {
                report.AppendLine($"{indent}    │   (No components)");
            }
            else
            {
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        report.AppendLine($"{indent}    │   ◦ Missing Script ⚠️");
                        continue;
                    }

                    if (!showBuiltInComponents && IsBuiltInComponent(component))
                        continue;

                    AnalyzeComponent(component, report, indent + "    │   ");
                }
            }
            report.AppendLine($"{indent}    └── END COMPONENTS");
            report.AppendLine();

            // Children analysis
            if (transform.childCount > 0)
            {
                report.AppendLine($"{indent}    ┌── CHILDREN ({transform.childCount}):");

                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i).gameObject;
                    if (!includeInactiveObjects && !child.activeInHierarchy)
                        continue;

                    report.AppendLine($"{indent}    │");

                    // Recursive call for each child
                    AnalyzeObjectRecursively(child, report, depth + 1);
                }

                report.AppendLine($"{indent}    └── END CHILDREN");
                report.AppendLine();
            }
            else
            {
                report.AppendLine($"{indent}    (No children)");
                report.AppendLine();
            }
        }

        private void AnalyzeComponent(Component component, StringBuilder report, string baseIndent)
        {
            var componentType = component.GetType();
            string componentName = componentType.Name;

            // Component header with enabled state
            string enabledStatus = "";
            if (component is MonoBehaviour mb)
                enabledStatus = mb.enabled ? " [Enabled]" : " [Disabled]";
            else if (component is Renderer rend)
                enabledStatus = rend.enabled ? " [Enabled]" : " [Disabled]";
            else if (component is Collider coll)
                enabledStatus = coll.enabled ? " [Enabled]" : " [Disabled]";

            report.AppendLine($"{baseIndent}◦ {componentName}{enabledStatus}");

            if (!showComponentDetails) return;

            // Component details
            report.AppendLine($"{baseIndent}  │ Type: {componentType.FullName}");
            report.AppendLine($"{baseIndent}  │ Assembly: {componentType.Assembly.GetName().Name}");

            // Special handling for common components
            AnalyzeSpecialComponentTypes(component, report, baseIndent + "  │ ");

            // Fields analysis
            if (showPrivateFields || HasPublicFields(componentType))
            {
                AnalyzeComponentFields(component, componentType, report, baseIndent + "  │ ");
            }

            report.AppendLine($"{baseIndent}  └──");
        }

        private void AnalyzeSpecialComponentTypes(Component component, StringBuilder report, string indent)
        {
            switch (component)
            {
                case RectTransform rectTransform:
                    report.AppendLine($"{indent}== RECT TRANSFORM SETTINGS ==");
                    report.AppendLine($"{indent}Anchored Position: {rectTransform.anchoredPosition}");
                    report.AppendLine($"{indent}Anchored Position 3D: {rectTransform.anchoredPosition3D}");
                    report.AppendLine($"{indent}Anchor Min: {rectTransform.anchorMin}");
                    report.AppendLine($"{indent}Anchor Max: {rectTransform.anchorMax}");
                    report.AppendLine($"{indent}Offset Min: {rectTransform.offsetMin}");
                    report.AppendLine($"{indent}Offset Max: {rectTransform.offsetMax}");
                    report.AppendLine($"{indent}Size Delta: {rectTransform.sizeDelta}");
                    report.AppendLine($"{indent}Pivot: {rectTransform.pivot}");
                    report.AppendLine($"{indent}Rect: {rectTransform.rect}");
                    break;

                case Canvas canvas:
                    report.AppendLine($"{indent}== CANVAS SETTINGS ==");
                    report.AppendLine($"{indent}Render Mode: {canvas.renderMode}");
                    report.AppendLine($"{indent}Sort Order: {canvas.sortingOrder}");
                    report.AppendLine($"{indent}Sorting Layer: {canvas.sortingLayerName}");
                    report.AppendLine($"{indent}Override Sorting: {canvas.overrideSorting}");
                    if (canvas.worldCamera != null)
                        report.AppendLine($"{indent}World Camera: {canvas.worldCamera.name}");
                    report.AppendLine($"{indent}Plane Distance: {canvas.planeDistance}");
                    report.AppendLine($"{indent}Additional Shader Channels: {canvas.additionalShaderChannels}");
                    break;

                case UnityEngine.UI.CanvasScaler canvasScaler:
                    report.AppendLine($"{indent}== CANVAS SCALER SETTINGS ==");
                    report.AppendLine($"{indent}UI Scale Mode: {canvasScaler.uiScaleMode}");
                    report.AppendLine($"{indent}Scale Factor: {canvasScaler.scaleFactor}");
                    report.AppendLine($"{indent}Reference Resolution: {canvasScaler.referenceResolution}");
                    report.AppendLine($"{indent}Screen Match Mode: {canvasScaler.screenMatchMode}");
                    report.AppendLine($"{indent}Match Width/Height: {canvasScaler.matchWidthOrHeight}");
                    report.AppendLine($"{indent}Physical Unit: {canvasScaler.physicalUnit}");
                    report.AppendLine($"{indent}Fallback Screen DPI: {canvasScaler.fallbackScreenDPI}");
                    report.AppendLine($"{indent}Default Sprite DPI: {canvasScaler.defaultSpriteDPI}");
                    report.AppendLine($"{indent}Dynamic Pixels Per Unit: {canvasScaler.dynamicPixelsPerUnit}");
                    break;

                case UnityEngine.UI.GraphicRaycaster graphicRaycaster:
                    report.AppendLine($"{indent}== GRAPHIC RAYCASTER SETTINGS ==");
                    report.AppendLine($"{indent}Ignore Reversed Graphics: {graphicRaycaster.ignoreReversedGraphics}");
                    report.AppendLine($"{indent}Blocking Objects: {graphicRaycaster.blockingObjects}");
                    report.AppendLine($"{indent}Blocking Mask: {graphicRaycaster.blockingMask.value}");
                    break;

                case UnityEngine.UI.Image image:
                    report.AppendLine($"{indent}== IMAGE SETTINGS ==");
                    if (image.sprite != null)
                        report.AppendLine($"{indent}Sprite: {image.sprite.name}");
                    report.AppendLine($"{indent}Color: {image.color}");
                    report.AppendLine($"{indent}Material: {(image.material != null ? image.material.name : "None")}");
                    report.AppendLine($"{indent}Image Type: {image.type}");
                    report.AppendLine($"{indent}Fill Method: {image.fillMethod}");
                    report.AppendLine($"{indent}Fill Amount: {image.fillAmount}");
                    report.AppendLine($"{indent}Fill Center: {image.fillCenter}");
                    report.AppendLine($"{indent}Preserve Aspect: {image.preserveAspect}");
                    report.AppendLine($"{indent}Use Sprite Mesh: {image.useSpriteMesh}");
                    report.AppendLine($"{indent}Pixels Per Unit Multiplier: {image.pixelsPerUnitMultiplier}");
                    break;

                case UnityEngine.UI.Text text:
                    report.AppendLine($"{indent}== TEXT SETTINGS ==");
                    report.AppendLine($"{indent}Text: \"{text.text}\"");
                    if (text.font != null)
                        report.AppendLine($"{indent}Font: {text.font.name}");
                    report.AppendLine($"{indent}Font Style: {text.fontStyle}");
                    report.AppendLine($"{indent}Font Size: {text.fontSize}");
                    report.AppendLine($"{indent}Line Spacing: {text.lineSpacing}");
                    report.AppendLine($"{indent}Rich Text: {text.supportRichText}");
                    report.AppendLine($"{indent}Alignment: {text.alignment}");
                    report.AppendLine($"{indent}Horizontal Overflow: {text.horizontalOverflow}");
                    report.AppendLine($"{indent}Vertical Overflow: {text.verticalOverflow}");
                    report.AppendLine($"{indent}Best Fit: {text.resizeTextForBestFit}");
                    report.AppendLine($"{indent}Color: {text.color}");
                    break;

                case UnityEngine.UI.Button button:
                    report.AppendLine($"{indent}== BUTTON SETTINGS ==");
                    report.AppendLine($"{indent}Interactable: {button.interactable}");
                    report.AppendLine($"{indent}Transition: {button.transition}");
                    if (button.targetGraphic != null)
                        report.AppendLine($"{indent}Target Graphic: {button.targetGraphic.name}");
                    report.AppendLine($"{indent}Navigation: {button.navigation.mode}");
                    // Color block details
                    var colors = button.colors;
                    report.AppendLine($"{indent}Normal Color: {colors.normalColor}");
                    report.AppendLine($"{indent}Highlighted Color: {colors.highlightedColor}");
                    report.AppendLine($"{indent}Pressed Color: {colors.pressedColor}");
                    report.AppendLine($"{indent}Selected Color: {colors.selectedColor}");
                    report.AppendLine($"{indent}Disabled Color: {colors.disabledColor}");
                    report.AppendLine($"{indent}Color Multiplier: {colors.colorMultiplier}");
                    report.AppendLine($"{indent}Fade Duration: {colors.fadeDuration}");
                    break;

                case MeshRenderer meshRenderer:
                    report.AppendLine($"{indent}== MESH RENDERER SETTINGS ==");
                    report.AppendLine($"{indent}Cast Shadows: {meshRenderer.shadowCastingMode}");
                    report.AppendLine($"{indent}Receive Shadows: {meshRenderer.receiveShadows}");
                    report.AppendLine($"{indent}Motion Vectors: {meshRenderer.motionVectorGenerationMode}");
                    report.AppendLine($"{indent}Light Probes: {meshRenderer.lightProbeUsage}");
                    report.AppendLine($"{indent}Reflection Probes: {meshRenderer.reflectionProbeUsage}");
                    report.AppendLine($"{indent}Sorting Layer: {meshRenderer.sortingLayerName}");
                    report.AppendLine($"{indent}Order in Layer: {meshRenderer.sortingOrder}");
                    if (meshRenderer.sharedMaterials.Length > 0)
                    {
                        report.AppendLine($"{indent}Materials ({meshRenderer.sharedMaterials.Length}):");
                        for (int i = 0; i < meshRenderer.sharedMaterials.Length; i++)
                        {
                            var mat = meshRenderer.sharedMaterials[i];
                            report.AppendLine($"{indent}  [{i}] {(mat != null ? mat.name : "None")}");
                        }
                    }
                    break;

                case MeshFilter meshFilter:
                    report.AppendLine($"{indent}== MESH FILTER SETTINGS ==");
                    if (meshFilter.sharedMesh != null)
                    {
                        report.AppendLine($"{indent}Mesh: {meshFilter.sharedMesh.name}");
                        report.AppendLine($"{indent}Vertices: {meshFilter.sharedMesh.vertexCount}");
                        report.AppendLine($"{indent}Triangles: {meshFilter.sharedMesh.triangles.Length / 3}");
                        report.AppendLine($"{indent}Sub Meshes: {meshFilter.sharedMesh.subMeshCount}");
                    }
                    else
                    {
                        report.AppendLine($"{indent}Mesh: None");
                    }
                    break;

                case BoxCollider boxCollider:
                    report.AppendLine($"{indent}== BOX COLLIDER SETTINGS ==");
                    report.AppendLine($"{indent}Is Trigger: {boxCollider.isTrigger}");
                    report.AppendLine($"{indent}Size: {boxCollider.size}");
                    report.AppendLine($"{indent}Center: {boxCollider.center}");
                    if (boxCollider.sharedMaterial != null)
                        report.AppendLine($"{indent}Physics Material: {boxCollider.sharedMaterial.name}");
                    break;

                case SphereCollider sphereCollider:
                    report.AppendLine($"{indent}== SPHERE COLLIDER SETTINGS ==");
                    report.AppendLine($"{indent}Is Trigger: {sphereCollider.isTrigger}");
                    report.AppendLine($"{indent}Radius: {sphereCollider.radius}");
                    report.AppendLine($"{indent}Center: {sphereCollider.center}");
                    if (sphereCollider.sharedMaterial != null)
                        report.AppendLine($"{indent}Physics Material: {sphereCollider.sharedMaterial.name}");
                    break;

                case CapsuleCollider capsuleCollider:
                    report.AppendLine($"{indent}== CAPSULE COLLIDER SETTINGS ==");
                    report.AppendLine($"{indent}Is Trigger: {capsuleCollider.isTrigger}");
                    report.AppendLine($"{indent}Radius: {capsuleCollider.radius}");
                    report.AppendLine($"{indent}Height: {capsuleCollider.height}");
                    report.AppendLine($"{indent}Direction: {capsuleCollider.direction}");
                    report.AppendLine($"{indent}Center: {capsuleCollider.center}");
                    if (capsuleCollider.sharedMaterial != null)
                        report.AppendLine($"{indent}Physics Material: {capsuleCollider.sharedMaterial.name}");
                    break;

                case MeshCollider meshCollider:
                    report.AppendLine($"{indent}== MESH COLLIDER SETTINGS ==");
                    report.AppendLine($"{indent}Is Trigger: {meshCollider.isTrigger}");
                    report.AppendLine($"{indent}Convex: {meshCollider.convex}");
                    report.AppendLine($"{indent}Cook Options: {meshCollider.cookingOptions}");
                    if (meshCollider.sharedMesh != null)
                        report.AppendLine($"{indent}Mesh: {meshCollider.sharedMesh.name}");
                    if (meshCollider.sharedMaterial != null)
                        report.AppendLine($"{indent}Physics Material: {meshCollider.sharedMaterial.name}");
                    break;

                case Rigidbody rigidbody:
                    report.AppendLine($"{indent}== RIGIDBODY SETTINGS ==");
                    report.AppendLine($"{indent}Mass: {rigidbody.mass}");
                    report.AppendLine($"{indent}Drag: {rigidbody.linearDamping}");
                    report.AppendLine($"{indent}Angular Drag: {rigidbody.angularDamping}");
                    report.AppendLine($"{indent}Use Gravity: {rigidbody.useGravity}");
                    report.AppendLine($"{indent}Is Kinematic: {rigidbody.isKinematic}");
                    report.AppendLine($"{indent}Interpolate: {rigidbody.interpolation}");
                    report.AppendLine($"{indent}Collision Detection: {rigidbody.collisionDetectionMode}");
                    report.AppendLine($"{indent}Freeze Position: {rigidbody.constraints}");
                    report.AppendLine($"{indent}Velocity: {rigidbody.linearVelocity}");
                    report.AppendLine($"{indent}Angular Velocity: {rigidbody.angularVelocity}");
                    break;

                case CharacterController characterController:
                    report.AppendLine($"{indent}== CHARACTER CONTROLLER SETTINGS ==");
                    report.AppendLine($"{indent}Slope Limit: {characterController.slopeLimit}°");
                    report.AppendLine($"{indent}Step Offset: {characterController.stepOffset}");
                    report.AppendLine($"{indent}Skin Width: {characterController.skinWidth}");
                    report.AppendLine($"{indent}Min Move Distance: {characterController.minMoveDistance}");
                    report.AppendLine($"{indent}Center: {characterController.center}");
                    report.AppendLine($"{indent}Radius: {characterController.radius}");
                    report.AppendLine($"{indent}Height: {characterController.height}");
                    report.AppendLine($"{indent}Is Grounded: {characterController.isGrounded}");
                    report.AppendLine($"{indent}Velocity: {characterController.velocity}");
                    break;

                case AudioSource audioSource:
                    report.AppendLine($"{indent}== AUDIO SOURCE SETTINGS ==");
                    if (audioSource.clip != null)
                        report.AppendLine($"{indent}Clip: {audioSource.clip.name}");
                    report.AppendLine($"{indent}Volume: {audioSource.volume}");
                    report.AppendLine($"{indent}Pitch: {audioSource.pitch}");
                    report.AppendLine($"{indent}Spatial Blend: {audioSource.spatialBlend}");
                    report.AppendLine($"{indent}Priority: {audioSource.priority}");
                    report.AppendLine($"{indent}Loop: {audioSource.loop}");
                    report.AppendLine($"{indent}Mute: {audioSource.mute}");
                    report.AppendLine($"{indent}Play On Awake: {audioSource.playOnAwake}");
                    report.AppendLine($"{indent}Min Distance: {audioSource.minDistance}");
                    report.AppendLine($"{indent}Max Distance: {audioSource.maxDistance}");
                    report.AppendLine($"{indent}Rolloff Mode: {audioSource.rolloffMode}");
                    break;

                case Camera camera:
                    report.AppendLine($"{indent}== CAMERA SETTINGS ==");
                    report.AppendLine($"{indent}Clear Flags: {camera.clearFlags}");
                    report.AppendLine($"{indent}Background Color: {camera.backgroundColor}");
                    report.AppendLine($"{indent}Culling Mask: {camera.cullingMask}");
                    report.AppendLine($"{indent}Projection: {camera.orthographic}");
                    if (camera.orthographic)
                    {
                        report.AppendLine($"{indent}Size: {camera.orthographicSize}");
                    }
                    else
                    {
                        report.AppendLine($"{indent}Field of View: {camera.fieldOfView}°");
                    }
                    report.AppendLine($"{indent}Near Clipping: {camera.nearClipPlane}");
                    report.AppendLine($"{indent}Far Clipping: {camera.farClipPlane}");
                    report.AppendLine($"{indent}Viewport Rect: {camera.rect}");
                    report.AppendLine($"{indent}Depth: {camera.depth}");
                    report.AppendLine($"{indent}Rendering Path: {camera.renderingPath}");
                    report.AppendLine($"{indent}Target Texture: {(camera.targetTexture != null ? camera.targetTexture.name : "None")}");
                    report.AppendLine($"{indent}Occlusion Culling: {camera.useOcclusionCulling}");
                    report.AppendLine($"{indent}HDR: {camera.allowHDR}");
                    report.AppendLine($"{indent}MSAA: {camera.allowMSAA}");
                    break;

                case Light light:
                    report.AppendLine($"{indent}== LIGHT SETTINGS ==");
                    report.AppendLine($"{indent}Type: {light.type}");
                    report.AppendLine($"{indent}Color: {light.color}");
                    report.AppendLine($"{indent}Mode: {light.lightmapBakeType}");
                    report.AppendLine($"{indent}Intensity: {light.intensity}");
                    report.AppendLine($"{indent}Indirect Multiplier: {light.bounceIntensity}");
                    if (light.type == LightType.Spot)
                    {
                        report.AppendLine($"{indent}Range: {light.range}");
                        report.AppendLine($"{indent}Spot Angle: {light.spotAngle}°");
                    }
                    else if (light.type == LightType.Point)
                    {
                        report.AppendLine($"{indent}Range: {light.range}");
                    }
                    report.AppendLine($"{indent}Shadow Type: {light.shadows}");
                    report.AppendLine($"{indent}Shadow Strength: {light.shadowStrength}");
                    report.AppendLine($"{indent}Shadow Resolution: {light.shadowResolution}");
                    report.AppendLine($"{indent}Shadow Bias: {light.shadowBias}");
                    report.AppendLine($"{indent}Shadow Normal Bias: {light.shadowNormalBias}");
                    report.AppendLine($"{indent}Shadow Near Plane: {light.shadowNearPlane}");
                    report.AppendLine($"{indent}Cookie: {(light.cookie != null ? light.cookie.name : "None")}");
                    report.AppendLine($"{indent}Culling Mask: {light.cullingMask}");
                    break;

                case Animator animator:
                    report.AppendLine($"{indent}== ANIMATOR SETTINGS ==");
                    if (animator.runtimeAnimatorController != null)
                        report.AppendLine($"{indent}Controller: {animator.runtimeAnimatorController.name}");
                    if (animator.avatar != null)
                        report.AppendLine($"{indent}Avatar: {animator.avatar.name}");
                    report.AppendLine($"{indent}Apply Root Motion: {animator.applyRootMotion}");
                    report.AppendLine($"{indent}Update Mode: {animator.updateMode}");
                    report.AppendLine($"{indent}Culling Mode: {animator.cullingMode}");
                    report.AppendLine($"{indent}Is Human: {(animator.avatar != null ? animator.avatar.isHuman : false)}");
                    report.AppendLine($"{indent}Has Root Motion: {animator.hasRootMotion}");
                    report.AppendLine($"{indent}Is Optimizable: {animator.isOptimizable}");
                    report.AppendLine($"{indent}Layer Count: {animator.layerCount}");
                    report.AppendLine($"{indent}Parameter Count: {animator.parameterCount}");
                    if (animator.parameterCount > 0)
                    {
                        report.AppendLine($"{indent}Parameters:");
                        foreach (var param in animator.parameters)
                        {
                            object value = "N/A";
                            switch (param.type)
                            {
                                case AnimatorControllerParameterType.Bool:
                                    value = animator.GetBool(param.name);
                                    break;
                                case AnimatorControllerParameterType.Float:
                                    value = animator.GetFloat(param.name);
                                    break;
                                case AnimatorControllerParameterType.Int:
                                    value = animator.GetInteger(param.name);
                                    break;
                                case AnimatorControllerParameterType.Trigger:
                                    value = "Trigger";
                                    break;
                            }
                            report.AppendLine($"{indent}  {param.type} {param.name} = {value}");
                        }
                    }
                    break;

                case Transform transform:
                    // Don't show transform details here as they're already shown in the object header
                    // But we can show if it has any special transform components
                    break;

                default:
                    // For any other components, try to get some basic info
                    if (component is Renderer renderer)
                    {
                        report.AppendLine($"{indent}== RENDERER SETTINGS ==");
                        if (renderer.sharedMaterial != null)
                            report.AppendLine($"{indent}Material: {renderer.sharedMaterial.name}");
                        report.AppendLine($"{indent}Cast Shadows: {renderer.shadowCastingMode}");
                        report.AppendLine($"{indent}Receive Shadows: {renderer.receiveShadows}");
                    }
                    break;
            }
        }

        private bool HasPublicFields(System.Type type)
        {
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance).Any();
        }

        private void AnalyzeComponentFields(Component component, System.Type componentType, StringBuilder report, string indent)
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.Instance;
            if (showPrivateFields)
                bindingFlags |= BindingFlags.NonPublic;

            var fields = componentType.GetFields(bindingFlags)
                .Where(f => !f.IsLiteral && !f.IsInitOnly) // Exclude constants
                .Where(f => showPrivateFields || f.IsPublic)
                .OrderBy(f => f.Name)
                .Take(10); // Limit to first 10 fields to avoid overwhelming output

            var fieldsList = fields.ToList();
            if (fieldsList.Any())
            {
                report.AppendLine($"{indent}Fields:");
                foreach (var field in fieldsList)
                {
                    try
                    {
                        object value = field.GetValue(component);
                        string valueStr = FormatFieldValue(value, field.FieldType);
                        string accessModifier = field.IsPublic ? "public" : "private";

                        // Check for SerializeField attribute
                        bool isSerializeField = field.GetCustomAttributes(typeof(SerializeField), false).Length > 0;
                        if (!field.IsPublic && isSerializeField)
                            accessModifier += " [SerializeField]";

                        report.AppendLine($"{indent}  {accessModifier} {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch (System.Exception e)
                    {
                        report.AppendLine($"{indent}  {field.FieldType.Name} {field.Name} = <Error: {e.Message}>");
                    }
                }

                // Show if there are more fields
                var totalFields = componentType.GetFields(bindingFlags).Where(f => !f.IsLiteral && !f.IsInitOnly).Count();
                if (totalFields > 10)
                {
                    report.AppendLine($"{indent}  ... and {totalFields - 10} more fields");
                }
            }
        }

        private string FormatFieldValue(object value, System.Type fieldType)
        {
            if (value == null)
                return "null";

            // Handle common Unity types specially
            if (value is UnityEngine.Object unityObj)
            {
                if (unityObj == null)
                    return "null (Missing Reference)";
                return $"\"{unityObj.name}\" ({unityObj.GetType().Name})";
            }

            if (value is string str)
                return $"\"{str}\"";

            if (value is bool || value.GetType().IsPrimitive)
                return value.ToString();

            if (value is Vector3 v3)
                return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";

            if (value is Vector2 v2)
                return $"({v2.x:F2}, {v2.y:F2})";

            if (value is Color color)
                return $"RGBA({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";

            // For arrays and lists, show count
            if (value is System.Collections.ICollection collection)
                return $"{fieldType.Name} [Count: {collection.Count}]";

            // For enums, show the enum value
            if (fieldType.IsEnum)
                return value.ToString();

            // For other complex types, just show the type
            return $"{fieldType.Name} instance";
        }

        private bool IsBuiltInComponent(Component component)
        {
            var builtInTypes = new System.Type[]
            {
                typeof(Transform),
                typeof(RectTransform),
                typeof(MeshRenderer),
                typeof(MeshFilter),
                typeof(BoxCollider),
                typeof(SphereCollider),
                typeof(CapsuleCollider),
                typeof(MeshCollider),
                typeof(Rigidbody),
                typeof(Camera),
                typeof(Light),
                typeof(AudioSource),
                typeof(Canvas),
                typeof(CanvasRenderer)
            };

            var componentType = component.GetType();
            return builtInTypes.Any(type => type.IsAssignableFrom(componentType));
        }

        private string GetFullPath(GameObject obj)
        {
            var path = new List<string>();
            Transform current = obj.transform;

            while (current != null)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", path);
        }
    }
}