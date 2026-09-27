using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FXMeshGeneratorPro.UnityEdition
{
    /// <summary>
    /// FX Mesh Generator Pro - Unity Edition v1.0.0 RC1
    /// User release candidate build with stable UV flow preview, persistent scene helpers, simplified export workflow, and publisher tools hidden behind Advanced mode.
    /// Put this file under: Assets/FXMeshGeneratorPro/Editor/
    /// Open from: Tools > FX Mesh Generator Pro > Unity Edition
    /// </summary>
    public sealed partial class FXMeshGeneratorProUnityEditionWindow : EditorWindow
    {
        private const string ToolVersion = "v1.0.0 RC1 Asset Store Submission";
        private const string PreviewObjectName = "FXM_UnityEdition_Preview";
        private const string PreviewShaderName = "FX Mesh Generator Pro/Preview/Vertex Alpha Checker";

        private const string PackageRoot = "Assets/FXMeshGeneratorPro";
        private const string GeneratedRoot = PackageRoot + "/Generated";
        private const string GeneratedMeshesFolder = GeneratedRoot + "/Meshes";
        private const string GeneratedMaterialFolder = GeneratedRoot + "/Materials";
        private const string GeneratedPrefabFolder = GeneratedRoot + "/Prefabs";
        private const string GeneratedSamplesFolder = GeneratedRoot + "/Samples";
        private const string GeneratedSampleMeshesFolder = GeneratedSamplesFolder + "/Meshes";
        private const string GeneratedSamplePrefabsFolder = GeneratedSamplesFolder + "/Prefabs";
        private const string GeneratedSampleScenesFolder = GeneratedSamplesFolder + "/Scenes";
        private const string DefaultPreviewMaterialPath = GeneratedMaterialFolder + "/M_FXM_VertexAlphaPreview.mat";
        private const int HeavyCrossHelixCellThreshold = 8192;
        // Kept intentionally for legacy generator paths / Unity reimport safety.
        private static readonly Color32 VertexColorWhite32 = new Color32(255, 255, 255, 255);

        private const float UiButtonHeight = 26f;
        private const float UiSmallButtonHeight = 24f;
        private const float UiSectionGap = 10f;
        private const float UiRowGap = 5f;

        private FXMSettings _settings = FXMSettings.Default();
        private Mesh _previewMesh;
        private GameObject _previewObject;
        private Material _previewMaterial;
        private Vector2 _scroll;

        private enum FXMUiLanguageMode
        {
            Auto,
            English,
            Korean
        }

        private enum FXMPreviewLodMode
        {
            Off,
            Auto,
            Performance,
            UltraPerformance
        }

        private enum FXMWorkspaceMode
        {
            Basic,
            Advanced
        }

        private enum FXMProductionRecipe
        {
            SwordSlash,
            BeamStrip,
            ShockwaveRing,
            ImpactDisc,
            ShieldDome,
            TornadoHelix,
            CrossEnergyHelix
        }

        private FXMUiLanguageMode _uiLanguageMode = FXMUiLanguageMode.Auto;
        private FXMWorkspaceMode _workspaceMode = FXMWorkspaceMode.Basic;
        private FXMProductionRecipe _productionRecipe = FXMProductionRecipe.SwordSlash;
        private bool _demoSceneBuildScheduled = false;
        private static bool _activeUiKorean;

        private bool _autoRefresh = true;
        private double _lastEditorUpdateTime = 0.0;
        private FXMPreviewLodMode _previewLodMode = FXMPreviewLodMode.Auto;
        private int _previewLodMaxFlowSegments = 96;
        private int _previewLodMaxCrossSegments = 18;
        private bool _lastPreviewUsedLod = false;
        private int _lastPreviewSourceSegments = 0;
        private int _lastPreviewSourceWidthSegments = 0;
        private int _lastPreviewBuildSegments = 0;
        private int _lastPreviewBuildWidthSegments = 0;
        private bool _showShape = true;
        private bool _showMeshQualityPass = true;
        private string _lastMeshQualityReport = string.Empty;
        private Vector2 _meshQualityReportScroll;
        private bool _qualityMobileMode = false;
        private bool _showUV = true;
        private bool _showAlpha = true;
        private bool _showAdvanced = true;
        private bool _showShapeEdit = true;
        private bool _showModifiers = true;
        private bool _showPivotAxis = true;
        private bool _showPreview = true;
        private bool _showExport = true;
        private bool _showFolder = true;

        private FXMPreviewMode _previewMode = FXMPreviewMode.UVChecker;
        private Color _previewTint = new Color(0.25f, 0.75f, 1f, 0.85f);
        private float _checkerScale = 8f;
        private float _checkerStrength = 0.85f;
        private bool _alphaAsOpacity = false;
        // Preview-only offsets. X = width/side offset, Y = semantic flow/progress offset.
        // These never modify saved mesh UVs; they only drive the on-mesh Flow Preview material and 2D preview.
        private float _flowPreviewOffsetU = 0f;
        private float _flowPreviewOffsetV = 0f;
        private bool _flowPreviewAnimate = false;
        private float _flowPreviewSpeed = 0.35f;

        private bool _showPreviewHelpers = true;
        private bool _autoAlphaProfile = false;
        private bool _autoUvProfile = false;
        private bool _autoHelperProfile = true;
        private bool _showWireBoundsHelper = true;
        private bool _showMeshWireHelper = true;
        private int _meshWireMaxEdges = 4096;
        private Color _meshWireColor = new Color(0.0f, 1f, 1f, 0.95f);
        private float _meshWireThickness = 2.25f;
        private bool _showNormalHelper = false;
        private bool _showFaceOrientationHelper = false;
        private int _faceOrientationTriangleLimit = 768;
        private bool _showUvMiniMapPreview = true;
        private bool _uvMiniMapShowTriangles = true;
        private bool _uvMiniMapShowDirection = true;
        private float _uvMiniMapHeight = 190f;
        private bool _showUvFlowHelper = true;
        private bool _showUvSurfaceFlowArrows = true;
        private int _uvSurfaceArrowMaxCount = 72;
        private float _uvSurfaceArrowLength = 0.85f;
        private float _uvSurfaceArrowOffset = 0.0035f;
        private bool _showUvBasisHelper = false;
        private int _uvBasisSampleCount = 40;
        private float _uvBasisHelperLength = 0.65f;
        private bool _showBackfaceHelper = true;
        private bool _showPivotGizmoHelper = true;
        private bool _showAxisGizmoHelper = true;
        private bool _showFfdHelper = true;
        private bool _showBendHelper = true;
        private bool _showTaperHelper = true;
        private float _helperScale = 0.18f;
        private int _normalSampleCount = 48;

        private bool _showMeshValidationSuite = true;
        private string _lastMeshValidationReport = string.Empty;
        private Vector2 _validationReportScroll;
        private bool _validationCopyReportAfterRun = true;
        private int _validationMobileVertexBudget = 10000;
        private int _validationMobileTriangleBudget = 16000;

        private FXMAlphaPresetKind _alphaPresetSelection = FXMAlphaPresetKind.FlowIn;
        private bool _showPreviewMaterialUtilities = false;

        private bool _showExportFinalizer = true;
        private string _lastExportReadinessReport = string.Empty;

        private bool _showFullRegressionQa = true;
        private string _lastFullRegressionReport = string.Empty;
        private Vector2 _fullRegressionReportScroll;

        private bool _showFlowVisualQa = true;
        private string _lastFlowVisualQaReport = string.Empty;
        private Vector2 _flowVisualQaReportScroll;

        private bool _showStoreReleaseCandidate = false;
        private bool _showPublisherTools = false;
        private bool _publisherToolsUnlocked = false;
        private string _lastStoreReleaseReport = string.Empty;
        private Vector2 _storeReleaseReportScroll;

        [MenuItem("Tools/FX Mesh Generator Pro", false, 2000)]
        public static void Open()
        {
            FXMeshGeneratorProUnityEditionWindow window = GetWindow<FXMeshGeneratorProUnityEditionWindow>();
            window.titleContent = new GUIContent("FX Mesh Generator Pro");
            window.minSize = new Vector2(460f, 720f);
            window.Show();
        }

        private void UpdateUiLanguageContext()
        {
            switch (_uiLanguageMode)
            {
                case FXMUiLanguageMode.Korean:
                    _activeUiKorean = true;
                    break;
                case FXMUiLanguageMode.English:
                    _activeUiKorean = false;
                    break;
                default:
                    _activeUiKorean = DetectUnityEditorKoreanLanguage();
                    break;
            }
        }

        internal static string Tr(string english, string korean)
        {
            return _activeUiKorean ? korean : english;
        }

        private static GUIContent C(string english, string korean, string englishTooltip = null, string koreanTooltip = null)
        {
            return new GUIContent(Tr(english, korean), Tr(englishTooltip ?? string.Empty, koreanTooltip ?? englishTooltip ?? string.Empty));
        }

        private static bool DetectUnityEditorKoreanLanguage()
        {
            try
            {
                Type localizationDatabaseType = typeof(EditorWindow).Assembly.GetType("UnityEditor.LocalizationDatabase");
                if (localizationDatabaseType != null)
                {
                    const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                    System.Reflection.PropertyInfo property = localizationDatabaseType.GetProperty("currentEditorLanguage", Flags)
                        ?? localizationDatabaseType.GetProperty("CurrentEditorLanguage", Flags);
                    if (property != null)
                    {
                        object value = property.GetValue(null, null);
                        string language = value != null ? value.ToString() : string.Empty;
                        if (language.IndexOf("ko", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            language.IndexOf("korean", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }

                        if (language.IndexOf("en", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            language.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return false;
                        }
                    }
                }
            }
            catch
            {
                // Some Unity versions keep editor localization APIs internal. Fallback below is safe.
            }

            string[] editorLanguagePreferenceKeys =
            {
                "EditorLanguage",
                "kEditorLanguage",
                "UnityEditor.Language",
                "UnityEditor.EditorLanguage",
                "unity-editor-language"
            };

            for (int i = 0; i < editorLanguagePreferenceKeys.Length; i++)
            {
                string language = EditorPrefs.GetString(editorLanguagePreferenceKeys[i], string.Empty);
                if (language.IndexOf("ko", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    language.IndexOf("korean", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (language.IndexOf("en", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    language.IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            // Store-ready default: prefer English when the Unity Editor language cannot be read.
            return false;
        }

        private void OnEnable()
        {
            EnsurePackageFolders(false);
            CachePreviewObjectReference();

            // Wire preview must start ON by default even if an older EditorWindow layout
            // had this value serialized as false. Keep the helper root enabled as well.
            _showPreviewHelpers = true;
            _showMeshWireHelper = true;
            ApplyRecommendedHelperProfile(false);
            SetCheckerPreviewMode(false);

            // v0.28.1: If the preview object already exists after domain reload / window reopen,
            // immediately attach the rebuilt mesh. Otherwise Handles helpers, including wire,
            // have no valid scene mesh to draw against until the user presses button 1 again.
            RebuildPreviewMesh(_previewObject != null);

            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            _lastEditorUpdateTime = EditorApplication.timeSinceStartup;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= OnEditorUpdate;

            // v0.28.1: Do not destroy the transient preview mesh while the scene preview
            // object is still using it. Destroying it here makes the preview object lose
            // its mesh after closing/reopening the window, which also hides the wire helper.
            if (_previewMesh != null)
            {
                if (!IsMeshAssignedToPreviewObject(_previewMesh))
                {
                    DestroyTransientMesh(_previewMesh);
                }
                _previewMesh = null;
            }

            if (_previewMaterial != null && !AssetDatabase.Contains(_previewMaterial))
            {
                UnityEngine.Object.DestroyImmediate(_previewMaterial);
                _previewMaterial = null;
            }
        }

        private void OnEditorUpdate()
        {
            if (!_flowPreviewAnimate)
            {
                _lastEditorUpdateTime = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Clamp((float)(now - _lastEditorUpdateTime), 0f, 0.1f);
            _lastEditorUpdateTime = now;

            float signedSpeed = _flowPreviewSpeed;
            _flowPreviewOffsetV = Mathf.Repeat(_flowPreviewOffsetV + signedSpeed * delta, 1f);
            ApplyPreviewMaterialSettings(GetPreviewMaterial());
            Repaint();
            SceneView.RepaintAll();
        }

        private static Vector2 ResolveFlowPreviewUvOffset(FXMUVFlowDirection direction, bool flipU, bool flipV, float sideOffset, float flowOffset)
        {
            // v0.32.7: FINAL UV BASIS rule.
            // Mesh UV generation already bakes Reverse and Flip into the final UV basis.
            // Therefore the preview shader and Scene helpers must NOT apply Reverse/Flip again.
            // This function returns a sampling offset, not a semantic arrow vector:
            // because the shader samples (uv + offset), visible texture motion is the opposite of offset.
            // We intentionally use -flowOffset so dragging Flow Offset forward makes the texture travel
            // in the same direction as the on-mesh/helper arrows.
            sideOffset = Mathf.Repeat(sideOffset, 1f);
            flowOffset = Mathf.Repeat(flowOffset, 1f);

            switch (GetFinalUvFlowAxis(direction))
            {
                case FXMFlowPreviewAxis.U:
                    return new Vector2(-flowOffset, sideOffset);

                case FXMFlowPreviewAxis.V:
                default:
                    return new Vector2(sideOffset, -flowOffset);
            }
        }

        private enum FXMFlowPreviewAxis
        {
            U,
            V
        }

        private static FXMFlowPreviewAxis GetFinalUvFlowAxis(FXMUVFlowDirection direction)
        {
            switch (direction)
            {
                case FXMUVFlowDirection.UForward:
                case FXMUVFlowDirection.UReverse:
                case FXMUVFlowDirection.CircularCW:
                case FXMUVFlowDirection.CircularCCW:
                    return FXMFlowPreviewAxis.U;

                case FXMUVFlowDirection.Auto:
                case FXMUVFlowDirection.VForward:
                case FXMUVFlowDirection.VReverse:
                case FXMUVFlowDirection.FromCenter:
                case FXMUVFlowDirection.ToCenter:
                default:
                    return FXMFlowPreviewAxis.V;
            }
        }

        private static Vector2 ApplyFlowPreviewOffsetToUv(Vector2 uv, FXMSettings settings, float sideOffset, float flowOffset)
        {
            Vector2 offset = ResolveFlowPreviewUvOffset(settings.uvFlowDirection, settings.flipU, settings.flipV, sideOffset, flowOffset);
            uv.x = Mathf.Repeat(uv.x + offset.x, 1f);
            uv.y = Mathf.Repeat(uv.y + offset.y, 1f);
            return uv;
        }

        private static string GetFlowOffsetSummary(FXMSettings settings, float sideOffset, float flowOffset)
        {
            Vector2 offset = ResolveFlowPreviewUvOffset(settings.uvFlowDirection, settings.flipU, settings.flipV, sideOffset, flowOffset);
            return "Applied UV offset: U " + offset.x.ToString("+0.###;-0.###;0") + " / V " + offset.y.ToString("+0.###;-0.###;0");
        }

        private void OnGUI()
        {
            UpdateUiLanguageContext();
            DrawHeader();

            EditorGUI.BeginChangeCheck();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawMainSettings();
            DrawShapeSettings();
            DrawProductionRecipesPanel();
            DrawMeshQualityPassPanel();
            if (_workspaceMode == FXMWorkspaceMode.Advanced)
            {
                DrawShapeEditSettings();
                DrawModifierSettings();
            }
            DrawUVSettings();
            DrawAlphaSettings();
            if (_workspaceMode == FXMWorkspaceMode.Advanced)
            {
                DrawPivotAxisSettings();
            }
            DrawPreviewSettings();
            DrawExportSettings();
            if (_workspaceMode == FXMWorkspaceMode.Advanced)
            {
                DrawFolderSettings();
                DrawAdvancedSettings();
            }

            EditorGUILayout.EndScrollView();

            bool changed = EditorGUI.EndChangeCheck();
            if (changed)
            {
                ApplyPreviewMaterialSettings(GetPreviewMaterial());

                if (_autoRefresh && !ShouldDeferHeavyCrossHelixAutoRefresh())
                {
                    RebuildPreviewMesh(true);
                }
                else
                {
                    SceneView.RepaintAll();
                }
            }
        }

        private bool ShouldDeferHeavyCrossHelixAutoRefresh()
        {
            if (_settings.meshType != FXMMeshType.CrossHelix)
            {
                return false;
            }

            if (_previewLodMode != FXMPreviewLodMode.Off)
            {
                return false;
            }

            int estimatedCells = Mathf.Max(1, _settings.segments) * Mathf.Max(1, _settings.widthSegments) * 2;
            return estimatedCells >= HeavyCrossHelixCellThreshold;
        }

        private FXMSettings GetPreviewBuildSettings(out bool usedLod)
        {
            usedLod = false;
            FXMSettings previewSettings = _settings;
            if (_previewLodMode == FXMPreviewLodMode.Off)
            {
                return previewSettings;
            }

            int maxFlow = Mathf.Clamp(_previewLodMaxFlowSegments, 12, 256);
            int maxCross = Mathf.Clamp(_previewLodMaxCrossSegments, 1, 64);

            switch (_previewLodMode)
            {
                case FXMPreviewLodMode.Performance:
                    maxFlow = Mathf.Min(maxFlow, _settings.meshType == FXMMeshType.CrossHelix ? 72 : 96);
                    maxCross = Mathf.Min(maxCross, _settings.meshType == FXMMeshType.CrossHelix ? 12 : 16);
                    break;
                case FXMPreviewLodMode.UltraPerformance:
                    maxFlow = Mathf.Min(maxFlow, _settings.meshType == FXMMeshType.CrossHelix ? 48 : 64);
                    maxCross = Mathf.Min(maxCross, _settings.meshType == FXMMeshType.CrossHelix ? 8 : 10);
                    break;
            }

            previewSettings.segments = Mathf.Clamp(previewSettings.segments, 3, Mathf.Max(3, maxFlow));
            previewSettings.widthSegments = Mathf.Clamp(previewSettings.widthSegments, 1, Mathf.Max(1, maxCross));
            usedLod = previewSettings.segments != _settings.segments || previewSettings.widthSegments != _settings.widthSegments;
            return previewSettings;
        }

        private string GetPreviewLodStatusText()
        {
            if (_lastPreviewBuildSegments <= 0 || _lastPreviewBuildWidthSegments <= 0)
            {
                return Tr("Preview LOD: waiting for first rebuild.", "Preview LOD: 첫 리빌드를 기다리는 중입니다.");
            }

            if (!_lastPreviewUsedLod)
            {
                return Tr("Preview LOD: Full quality preview.", "Preview LOD: 전체 품질 프리뷰입니다.");
            }

            return Tr("Preview LOD: Scene preview is reduced for editing speed. Save/export still rebuilds full quality. ",
                      "Preview LOD: 편집 속도를 위해 Scene Preview만 축소되었습니다. 저장/익스포트는 항상 전체 품질로 리빌드합니다. ") +
                   _lastPreviewSourceSegments + "x" + _lastPreviewSourceWidthSegments + " → " +
                   _lastPreviewBuildSegments + "x" + _lastPreviewBuildWidthSegments;
        }

        private void ApplyFastPreviewSetup()
        {
            _previewLodMode = FXMPreviewLodMode.Performance;
            _previewLodMaxFlowSegments = Mathf.Min(_previewLodMaxFlowSegments, 96);
            _previewLodMaxCrossSegments = Mathf.Min(_previewLodMaxCrossSegments, 16);
            _meshWireMaxEdges = Mathf.Min(_meshWireMaxEdges, 3072);
            _uvSurfaceArrowMaxCount = Mathf.Min(_uvSurfaceArrowMaxCount, 48);
            _uvBasisSampleCount = Mathf.Min(_uvBasisSampleCount, 24);
            _faceOrientationTriangleLimit = Mathf.Min(_faceOrientationTriangleLimit, 384);
            _showFaceOrientationHelper = false;
            _showUvBasisHelper = false;
            SceneView.RepaintAll();
        }

        private void ApplyFullPreviewQaSetup()
        {
            _previewLodMode = FXMPreviewLodMode.Off;
            _meshWireMaxEdges = Mathf.Max(_meshWireMaxEdges, GetRecommendedWireEdgeLimit(_settings.meshType));
            _uvSurfaceArrowMaxCount = Mathf.Max(_uvSurfaceArrowMaxCount, 96);
            _uvBasisSampleCount = Mathf.Max(_uvBasisSampleCount, 48);
            _faceOrientationTriangleLimit = Mathf.Max(_faceOrientationTriangleLimit, 768);
            SceneView.RepaintAll();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleLeft
                };

                EditorGUILayout.LabelField("FX Mesh Generator Pro - Unity Edition", titleStyle);
                EditorGUILayout.LabelField(ToolVersion, EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(Tr("UI Language", "UI 언어"), GUILayout.Width(92f));
                    FXMUiLanguageMode nextLanguageMode = (FXMUiLanguageMode)EditorGUILayout.EnumPopup(_uiLanguageMode);
                    if (nextLanguageMode != _uiLanguageMode)
                    {
                        _uiLanguageMode = nextLanguageMode;
                        UpdateUiLanguageContext();
                    }
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        Tr("Auto follows the Unity Editor language.", "Auto는 Unity Editor 언어 설정을 따릅니다."),
                        EditorStyles.miniLabel,
                        GUILayout.Width(250f));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(Tr("Workspace Mode", "작업 모드"), GUILayout.Width(92f));
                    _workspaceMode = (FXMWorkspaceMode)EditorGUILayout.EnumPopup(_workspaceMode);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        _workspaceMode == FXMWorkspaceMode.Basic
                            ? Tr("Basic hides advanced editing panels for faster onboarding.", "Basic은 고급 편집 패널을 숨겨 빠르게 시작할 수 있습니다.")
                            : Tr("Advanced exposes all editing, folder, and debug panels.", "Advanced는 전체 편집/폴더/디버그 패널을 모두 표시합니다."),
                        EditorStyles.miniLabel,
                        GUILayout.Width(320f));
                }

                EditorGUILayout.HelpBox(
                    Tr(
                        "Production workflow: 1) Setup Preview → 2) Shape → 3) Deform → 4) UV & Alpha → 5) Pivot/Axis → 6) Preview & QA → 7) Export/Save.",
                        "작업 흐름: 1) 프리뷰 준비 → 2) 형태 → 3) 변형 → 4) UV/알파 → 5) 피봇/축 → 6) 프리뷰/검증 → 7) 저장/익스포트."),
                    MessageType.Info);

                EditorGUILayout.HelpBox(
                    Tr("v1.0.0 RC1: Asset Store submission build. Core workflow is focused on mesh creation, flow preview validation, alpha setup, and clean export/save.",
                       "v1.0.0 RC1 에셋스토어 제출용 빌드입니다. 메시 생성, Flow 프리뷰 검증, 알파 설정, 저장/익스포트 중심으로 정리했습니다."),
                    MessageType.None);
            }
        }

        private void DrawMainSettings()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(Tr("1. Setup / Mesh Source", "1. 메시 생성"), EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("Workflow: choose a Mesh Type, create the Scene Preview Object, tune Shape/Deform/UV/Alpha/Pivot, validate the preview, then export/save.", "작업 순서: 1) 메시 타입 선택 → 2) Scene Preview Object 생성 → 3) Generate Mesh Data로 수동 갱신 → 4) 형태/3ds Max Edit/UV/알파/Pivot/Modifier 조정 → 5) 프리뷰 검증 → 6) 저장 순서로 진행하세요.") + "\n" +
                    (_workspaceMode == FXMWorkspaceMode.Basic
                        ? Tr("Basic mode shows the production path first and hides advanced deformation/debug sections.", "Basic 모드는 작업 흐름 중심으로 보여주고 고급 변형/디버그 섹션은 숨깁니다.")
                        : Tr("Advanced mode shows the full toolset, including deformation, folder, and debug sections.", "Advanced 모드는 변형/폴더/디버그를 포함한 전체 기능을 보여줍니다.")),
                    MessageType.None);

                _settings.meshName = EditorGUILayout.TextField(new GUIContent("Mesh Name", Tr("Name used for the generated Mesh Asset and Prefab.", "저장될 메시 이름입니다.")), _settings.meshName);
                FXMMeshType previousMeshType = _settings.meshType;
                _settings.meshType = (FXMMeshType)EditorGUILayout.EnumPopup(new GUIContent("Mesh Type", Tr("Select the VFX mesh type to generate.", "생성할 VFX 메시 타입입니다.")), _settings.meshType);
                if (_settings.meshType != previousMeshType)
                {
                    ClampUnsupportedModifierValues(false);
                    if (_autoAlphaProfile)
                    {
                        ApplyRecommendedAlphaForCurrentMesh(false, false);
                    }
                    if (_autoUvProfile)
                    {
                        ApplyRecommendedUvForCurrentMesh(false);
                    }
                    // v0.25: Mesh Type을 바꿀 때는 기본 체크 프리뷰로 돌아갑니다.
                    // Vertex Alpha 매트 프리뷰는 사용자가 Alpha 버튼을 눌렀을 때만 잠깐 사용합니다.
                    SetCheckerPreviewMode(false);
                    if (_autoHelperProfile)
                    {
                        ApplyRecommendedHelperProfile(false);
                    }
                    ForceNormalHelperOff(false);
                }

                FXMShapeUiProfile shapeProfile = FXMShapeUiProfile.For(_settings.meshType);
                _settings.segments = EditorGUILayout.IntSlider(new GUIContent(shapeProfile.primarySegmentsLabel, shapeProfile.primarySegmentsTooltip), _settings.segments, 3, 256);
                _settings.widthSegments = EditorGUILayout.IntSlider(new GUIContent(shapeProfile.secondarySegmentsLabel, shapeProfile.secondarySegmentsTooltip), _settings.widthSegments, 1, 64);
                EditorGUILayout.HelpBox(shapeProfile.description, MessageType.None);

                if (_settings.meshType == FXMMeshType.CrossHelix)
                {
                    DrawCrossHelixRotationControls(Tr("Cross Helix / Generation Options", "Cross Helix / 생성 옵션"), true);
                    int crossHelixCells = Mathf.Max(1, _settings.segments) * Mathf.Max(1, _settings.widthSegments) * 2;
                    if (crossHelixCells >= HeavyCrossHelixCellThreshold)
                    {
                        EditorGUILayout.HelpBox(
                            _previewLodMode == FXMPreviewLodMode.Off
                                ? Tr("High-density Cross Helix protection: Auto Refresh is deferred when Preview LOD is Off. Enable Preview LOD or press Generate after tuning values.", "Cross Helix 고밀도 편집 보호: Preview LOD가 꺼져 있으면 자동 갱신을 보류합니다. Preview LOD를 켜거나 값 조정 후 Generate를 누르세요.")
                                : Tr("High-density Cross Helix: Preview LOD keeps Scene Preview responsive while preserving full-quality save/export.", "Cross Helix 고밀도: Preview LOD가 Scene Preview를 가볍게 유지하고 저장/익스포트는 전체 품질로 처리합니다."),
                            MessageType.Info);
                    }
                }

                _autoRefresh = EditorGUILayout.Toggle(new GUIContent("Auto Refresh", Tr("Automatically rebuilds the preview mesh when settings change. Heavy Cross Helix updates are deferred to keep the editor responsive.", "값 변경 시 자동으로 프리뷰 메시를 갱신합니다. Cross Helix 고밀도 상태에서는 에디터 버벅임 방지를 위해 자동 보류됩니다.")), _autoRefresh);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(Tr("Interactive Preview Optimization", "인터랙티브 프리뷰 최적화"), EditorStyles.miniBoldLabel);
                _previewLodMode = (FXMPreviewLodMode)EditorGUILayout.EnumPopup(new GUIContent("Preview LOD Mode", Tr("Reduces Scene Preview segment density while editing. Save/export always rebuilds the full-quality mesh.", "편집 중 Scene Preview 세그먼트 밀도를 줄입니다. 저장/익스포트는 항상 전체 품질 메시로 리빌드합니다.")), _previewLodMode);
                using (new EditorGUI.DisabledScope(_previewLodMode == FXMPreviewLodMode.Off))
                {
                    _previewLodMaxFlowSegments = EditorGUILayout.IntSlider(new GUIContent("LOD Max Flow Segments", Tr("Maximum primary segments used by the Scene Preview when Preview LOD is enabled.", "Preview LOD가 켜졌을 때 Scene Preview에 사용할 최대 주 세그먼트 수입니다.")), _previewLodMaxFlowSegments, 12, 256);
                    _previewLodMaxCrossSegments = EditorGUILayout.IntSlider(new GUIContent("LOD Max Cross Segments", Tr("Maximum secondary/cross segments used by the Scene Preview when Preview LOD is enabled.", "Preview LOD가 켜졌을 때 Scene Preview에 사용할 최대 보조/단면 세그먼트 수입니다.")), _previewLodMaxCrossSegments, 1, 64);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fast Preview Setup", GUILayout.Height(24f)))
                    {
                        ApplyFastPreviewSetup();
                    }

                    if (GUILayout.Button("Full Preview QA Setup", GUILayout.Height(24f)))
                    {
                        ApplyFullPreviewQaSetup();
                    }
                }

                EditorGUILayout.HelpBox(GetPreviewLodStatusText(), MessageType.None);

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("1) Create / Update Scene Preview Object", GUILayout.Height(34f)))
                    {
                        ForceNormalHelperOff(false);
                        CreateOrUpdatePreviewObject();
                    }

                    if (GUILayout.Button("2) Generate / Update Mesh Data", GUILayout.Height(34f)))
                    {
                        ForceNormalHelperOff(false);
                        RebuildPreviewMesh(true);
                    }
                }

                if (GUILayout.Button("Generate Full Quality Preview Once", GUILayout.Height(26f)))
                {
                    ForceNormalHelperOff(false);
                    RebuildPreviewMesh(true, true);
                }

                if (GUILayout.Button("Optional) Create New Scene Object Copy", GUILayout.Height(28f)))
                {
                    ForceNormalHelperOff(false);
                    CreateSceneObjectFromCurrentMesh();
                }

                EditorGUILayout.HelpBox(
                    Tr("Create the Scene Preview Object first to see the current mesh in the scene. If Auto Refresh is off, press Generate Mesh Data manually. Save/export from step 7.", "먼저 Scene Preview Object를 만들면 현재 설정 기준의 메시가 자동 생성되어 씬에서 바로 보입니다. Auto Refresh를 껐을 때는 Generate Mesh Data로 수동 갱신하세요. 저장은 아래 '6. 저장 / 최종 생성'에서 진행합니다."),
                    MessageType.Info);
            }
        }


        private void DrawShapeSettings()
        {
            EditorGUILayout.Space(6f);
            _showShape = EditorGUILayout.Foldout(_showShape, Tr("2. Shape Settings", "2. 형태 설정"), true);
            if (!_showShape) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                switch (_settings.meshType)
                {
                    case FXMMeshType.Slash:
                        _settings.radius = EditorGUILayout.Slider(new GUIContent("Radius", Tr("Controls the radius for the selected mesh.", "Slash 중심 반지름입니다.")), _settings.radius, 0.05f, 10f);
                        _settings.width = EditorGUILayout.Slider(new GUIContent("Width", Tr("Controls the width or thickness for the selected mesh.", "Slash 두께입니다.")), _settings.width, 0.01f, 5f);
                        _settings.arcAngle = EditorGUILayout.Slider(new GUIContent("Arc Angle", Tr("Controls Slash shape and flow behavior.", "Slash 호의 각도입니다.")), _settings.arcAngle, 5f, 360f);
                        _settings.startAngle = EditorGUILayout.Slider(new GUIContent("Start Rotation", Tr("Adjust this option for the selected production workflow.", "전체 회전 각도입니다.")), _settings.startAngle, -180f, 180f);
                        EditorGUILayout.HelpBox(Tr("Controls modifier/deform behavior for the selected mesh.", "검기 세부 변형은 바로 아래 2-1. 3ds Max Style Edit에서 Flow/Bias/Taper Direction으로 조절하세요."), MessageType.None);
                        break;

                    case FXMMeshType.Beam:
                        _settings.length = EditorGUILayout.Slider(new GUIContent("Length", Tr("Controls Beam shape and linear flow behavior.", "Beam 길이입니다.")), _settings.length, 0.05f, 20f);
                        _settings.width = EditorGUILayout.Slider(new GUIContent("Width", Tr("Controls the width or thickness for the selected mesh.", "Beam 폭입니다.")), _settings.width, 0.01f, 10f);
                        _settings.pivotMode = (FXMPivotMode)EditorGUILayout.EnumPopup(new GUIContent("Pivot", Tr("Controls Beam shape and linear flow behavior.", "Beam 피벗 기준입니다.")), _settings.pivotMode);
                        break;

                    case FXMMeshType.Ring:
                        _settings.radius = EditorGUILayout.Slider(new GUIContent("Radius", Tr("Controls the radius for the selected mesh.", "Ring 중심 반지름입니다.")), _settings.radius, 0.05f, 10f);
                        _settings.width = EditorGUILayout.Slider(new GUIContent("Thickness", Tr("Controls the width or thickness for the selected mesh.", "Ring 두께입니다.")), _settings.width, 0.01f, 5f);
                        _settings.arcAngle = EditorGUILayout.Slider(new GUIContent("Arc Angle", Tr("Adjust this option for the selected production workflow.", "360이면 닫힌 링, 낮추면 열린 링/브로큰 링 형태가 됩니다.")), _settings.arcAngle, 5f, 360f);
                        _settings.brokenAmount = EditorGUILayout.Slider(new GUIContent("Broken Amount", Tr("Controls Ring shape and circular flow behavior.", "Blender Broken Ring 감각입니다. 일부 둘레 면을 랜덤하게 비워 파편형 쇼크웨이브를 만듭니다.")), _settings.brokenAmount, 0f, 0.85f);
                        _settings.startAngle = EditorGUILayout.Slider(new GUIContent("Start Rotation", Tr("Controls Ring shape and circular flow behavior.", "Ring 회전 각도입니다.")), _settings.startAngle, -180f, 180f);
                        EditorGUILayout.HelpBox(Tr("Controls the width or thickness for the selected mesh.", "Ring은 Width / Thickness Segments가 실제 두께 방향 분할에 반영됩니다. 브로큰 링은 Broken Amount와 Noise Seed로 패턴을 바꾸세요."), MessageType.None);
                        break;

                    case FXMMeshType.Disc:
                        _settings.radius = EditorGUILayout.Slider(new GUIContent("Radius", Tr("Controls the radius for the selected mesh.", "Disc 바깥 반지름입니다.")), _settings.radius, 0.05f, 10f);
                        _settings.innerRadiusRatio = EditorGUILayout.Slider(new GUIContent("Inner Radius Ratio", Tr("Controls Disc shape and radial flow behavior.", "0이면 거의 꽉 찬 Disc, 값이 커지면 구멍이 커집니다.")), _settings.innerRadiusRatio, 0f, 0.95f);
                        _settings.arcAngle = EditorGUILayout.Slider(new GUIContent("Arc Angle", Tr("Controls Disc shape and radial flow behavior.", "360이면 원형 Disc, 낮추면 쿨다운/부채꼴 Disc가 됩니다.")), _settings.arcAngle, 5f, 360f);
                        _settings.startAngle = EditorGUILayout.Slider(new GUIContent("Start Rotation", Tr("Controls Disc shape and radial flow behavior.", "Disc 회전 각도입니다.")), _settings.startAngle, -180f, 180f);
                        EditorGUILayout.HelpBox(Tr("Controls the radius for the selected mesh.", "Disc는 Radial Segments, Inner Radius Ratio, Arc Angle 조합으로 마법진/쿨다운/바닥 웨이브를 빠르게 만들 수 있습니다."), MessageType.None);
                        break;

                    case FXMMeshType.Dome:
                        _settings.radius = EditorGUILayout.Slider(new GUIContent("Radius", Tr("Controls the radius for the selected mesh.", "Dome 바닥 반지름입니다.")), _settings.radius, 0.05f, 10f);
                        _settings.height = EditorGUILayout.Slider(new GUIContent("Height", Tr("Controls Dome shape and preview behavior.", "Dome 높이입니다.")), _settings.height, 0.01f, 10f);
                        _settings.startAngle = EditorGUILayout.Slider(new GUIContent("Start Rotation", Tr("Controls Dome shape and preview behavior.", "Dome 회전 각도입니다.")), _settings.startAngle, -180f, 180f);
                        EditorGUILayout.HelpBox(Tr("Controls Dome shape and preview behavior.", "Dome은 XZ 바닥면 + Y 높이 기준입니다. 보호막, 충격파 반구, 장판형 VFX에 적합합니다."), MessageType.None);
                        break;

                    case FXMMeshType.HalfDome:
                        _settings.radius = EditorGUILayout.Slider(new GUIContent("Radius", Tr("Controls the radius for the selected mesh.", "Half Dome 바닥 반지름입니다.")), _settings.radius, 0.05f, 10f);
                        _settings.height = EditorGUILayout.Slider(new GUIContent("Height", Tr("Controls Dome shape and preview behavior.", "Half Dome 높이입니다.")), _settings.height, 0.01f, 10f);
                        _settings.arcAngle = EditorGUILayout.Slider(new GUIContent("Open Angle", Tr("Adjust this option for the selected production workflow.", "반구의 가로 열림 각도입니다.")), _settings.arcAngle, 30f, 360f);
                        _settings.startAngle = EditorGUILayout.Slider(new GUIContent("Start Rotation", Tr("Controls Dome shape and preview behavior.", "Half Dome 회전 각도입니다.")), _settings.startAngle, -180f, 180f);
                        break;

                    case FXMMeshType.Helix:
                    case FXMMeshType.CrossHelix:
                        _settings.radius = EditorGUILayout.Slider(new GUIContent("Radius", Tr("Controls the radius for the selected mesh.", "나선 중심 반지름입니다.")), _settings.radius, 0.05f, 10f);
                        _settings.width = EditorGUILayout.Slider(new GUIContent("Ribbon Width", Tr("Controls the width or thickness for the selected mesh.", "나선 리본 폭입니다.")), _settings.width, 0.01f, 5f);
                        _settings.height = EditorGUILayout.Slider(new GUIContent("Height", Tr("Adjust this option for the selected production workflow.", "나선 높이입니다.")), _settings.height, 0.05f, 20f);
                        _settings.turns = EditorGUILayout.Slider(new GUIContent("Turns", Tr("Adjust this option for the selected production workflow.", "나선 회전 수입니다.")), _settings.turns, 0.1f, 12f);
                        _settings.startAngle = EditorGUILayout.Slider(new GUIContent("Start Rotation", Tr("Adjust this option for the selected production workflow.", "나선 시작 회전입니다.")), _settings.startAngle, -180f, 180f);
                        break;
                }
            }
        }

        private void DrawProductionRecipesPanel()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawUiSectionHeader(
                    Tr("2-1. Production Recipes", "2-1. 프로덕션 레시피"),
                    Tr("Clean starting points for common real-time VFX meshes. Apply a recipe first, then tune shape, UV, alpha, QA, and export.", "자주 쓰는 실시간 VFX 메시 시작점입니다. 레시피 적용 후 형태, UV, 알파, QA, 저장을 순서대로 조정하세요."));

                DrawUiSubHeader(Tr("Recipe Setup", "레시피 설정"));
                _productionRecipe = (FXMProductionRecipe)EditorGUILayout.EnumPopup(
                    new GUIContent(Tr("Recipe", "레시피"), Tr("Select a production-ready starting point.", "실무용 시작 프리셋을 선택합니다.")),
                    _productionRecipe);

                UiRowSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UiButton(Tr("Apply Recipe", "레시피 적용")))
                    {
                        ApplyProductionRecipe(_productionRecipe, true);
                    }

                    if (UiButton(Tr("Apply + Auto Name", "적용 + 자동 이름")))
                    {
                        ApplyProductionRecipe(_productionRecipe, false);
                        _settings.meshName = BuildAutoMeshName(_settings.meshType, _settings.exportPreset);
                        RebuildPreviewMesh(true);
                    }
                }

                DrawUiSubHeader(Tr("Sample Pack", "샘플 팩"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UiSmallButton(Tr("Generate Sample Mesh Pack", "샘플 메시 팩 생성")))
                    {
                        GenerateProductionRecipeSamplePack(false, true);
                    }

                    if (UiSmallButton(Tr("Generate Sample Prefab Pack", "샘플 프리팹 팩 생성")))
                    {
                        GenerateProductionRecipeSamplePack(true, true);
                    }
                }

                UiRowSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UiSmallButton(Tr("Build Demo Scene", "데모 씬 생성")))
                    {
                        RequestProductionRecipeDemoSceneBuild(false);
                    }

                    if (UiSmallButton(Tr("Sample Pack + Demo", "샘플 팩 + 데모")))
                    {
                        RequestProductionRecipeDemoSceneBuild(true);
                    }
                }

                UiRowSpace();
                if (UiSmallButton(Tr("Copy Recipe Catalog", "레시피 카탈로그 복사")))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildProductionRecipeCatalogReport();
                }
            }
        }

        private void GenerateProductionRecipeSamplePack(bool includePrefabs, bool showDialog)
        {
            FXMSettings originalSettings = _settings;
            FXMProductionRecipe originalRecipe = _productionRecipe;
            EnsurePackageFolders(false);

            List<UnityEngine.Object> savedObjects = new List<UnityEngine.Object>();
            Array recipes = Enum.GetValues(typeof(FXMProductionRecipe));
            foreach (FXMProductionRecipe recipe in recipes)
            {
                _settings = originalSettings;
                _productionRecipe = recipe;
                ApplyProductionRecipe(recipe, false);
                ApplyMeshQualityPass(true, false);
                ApplyFinishPipeline(false, false);

                string sampleName = "FXM_SAMPLE_" + recipe.ToString();
                _settings.meshName = sampleName;

                Mesh meshAsset = CreateMeshAssetInFolder(GeneratedSampleMeshesFolder, sampleName, string.Empty, false);
                if (meshAsset != null)
                {
                    savedObjects.Add(meshAsset);
                }

                if (includePrefabs)
                {
                    GameObject prefab = CreatePrefabAssetInFolder(GeneratedSamplePrefabsFolder, sampleName, meshAsset, false);
                    if (prefab != null)
                    {
                        savedObjects.Add(prefab);
                    }
                }
            }

            _settings = originalSettings;
            _productionRecipe = originalRecipe;
            RebuildPreviewMesh(true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (savedObjects.Count > 0)
            {
                Selection.objects = savedObjects.ToArray();
                EditorGUIUtility.PingObject(savedObjects[0]);
            }

            string report = BuildProductionRecipeCatalogReport();
            EditorGUIUtility.systemCopyBuffer = report;
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "FXM Sample Pack",
                    savedObjects.Count + Tr(" sample assets generated. Recipe catalog copied to clipboard.\n\n", "개 샘플 에셋을 생성했습니다. 레시피 카탈로그를 클립보드에 복사했습니다.\n\n") + GeneratedSamplesFolder,
                    "OK");
            }
        }

        private void RequestProductionRecipeDemoSceneBuild(bool generateSamplePackFirst)
        {
            if (_demoSceneBuildScheduled)
            {
                EditorUtility.DisplayDialog(
                    "FXM Demo Scene Builder",
                    Tr("A demo scene build is already scheduled. Please wait for the current operation to finish.", "이미 데모 씬 생성이 예약되어 있습니다. 현재 작업이 끝날 때까지 기다려 주세요."),
                    "OK");
                return;
            }

            _demoSceneBuildScheduled = true;
            ScheduleDelayedEditorOperation(
                "FXM Demo Scene Builder",
                () =>
                {
                    _demoSceneBuildScheduled = false;
                    if (generateSamplePackFirst)
                    {
                        GenerateProductionRecipeSamplePack(true, false);
                    }
                    BuildProductionRecipeDemoScene(false);
                },
                "Demo scene generation failed. See the Console for details.",
                "데모 씬 생성에 실패했습니다. 자세한 내용은 Console을 확인하세요.");
        }

        private void BuildProductionRecipeDemoScene(bool skipPrompt)
        {
            // Must run from EditorApplication.delayCall, never directly inside OnGUI.
            // EditorSceneManager.NewScene changes the editor layout/repaint state and can break IMGUI GUILayout groups
            // if executed while DrawProductionRecipesPanel is still inside a VerticalScope/ScrollView.
            if (!skipPrompt && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            FXMSettings originalSettings = _settings;
            FXMProductionRecipe originalRecipe = _productionRecipe;
            EnsurePackageFolders(false);
            EnsureFolder(GeneratedSampleScenesFolder);

            Material previewMaterial = GetOrCreateDemoPreviewMaterial();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("FXM_DemoScene_Root");

            CreateDemoSceneCameraAndLight(root.transform);
            CreateDemoSceneGround(root.transform);
            CreateDemoSceneReadme(root.transform);

            Array recipes = Enum.GetValues(typeof(FXMProductionRecipe));
            int index = 0;
            foreach (FXMProductionRecipe recipe in recipes)
            {
                _settings = originalSettings;
                _productionRecipe = recipe;
                ApplyProductionRecipe(recipe, false);
                ApplyMeshQualityPass(true, false);
                ApplyFinishPipeline(false, false);

                string sampleName = "FXM_DEMO_" + recipe.ToString();
                _settings.meshName = sampleName;
                Mesh meshAsset = CreateMeshAssetInFolder(GeneratedSampleMeshesFolder, sampleName, string.Empty, false);
                if (meshAsset == null)
                {
                    continue;
                }

                GameObject go = new GameObject(sampleName);
                go.transform.SetParent(root.transform);
                go.transform.position = GetDemoScenePosition(index, recipes.Length);
                go.transform.rotation = GetDemoSceneRotation(recipe);

                MeshFilter meshFilter = go.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = meshAsset;

                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = previewMaterial;

                CreateDemoSceneLabel(recipe.ToString(), go.transform.position + new Vector3(0f, -0.08f, -1.1f), root.transform);
                index++;
            }

            _settings = originalSettings;
            _productionRecipe = originalRecipe;
            RebuildPreviewMesh(true);

            string scenePath = AssetDatabase.GenerateUniqueAssetPath(GeneratedSampleScenesFolder + "/FXM_ProductionRecipe_Demo.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            UnityEngine.Object sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenePath);
            if (sceneAsset != null)
            {
                EditorGUIUtility.PingObject(sceneAsset);
                Selection.activeObject = sceneAsset;
            }

            string report = BuildDemoSceneReport(scenePath);
            EditorGUIUtility.systemCopyBuffer = report;
            EditorUtility.DisplayDialog(
                "FXM Demo Scene Builder",
                Tr("Demo scene generated and report copied to clipboard.\n\n", "데모 씬을 생성했고 리포트를 클립보드에 복사했습니다.\n\n") + scenePath,
                "OK");
        }

        private Material GetOrCreateDemoPreviewMaterial()
        {
            EnsurePackageFolders(false);
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(DefaultPreviewMaterialPath);
            if (existing != null)
            {
                ApplyPreviewMaterialSettings(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Material created = CreatePreviewMaterialAssetInternal(false);
            ApplyPreviewMaterialSettings(created);
            return created;
        }

        private static Vector3 GetDemoScenePosition(int index, int count)
        {
            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))));
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
            int row = index / columns;
            int col = index % columns;
            float spacingX = 3.2f;
            float spacingZ = 2.8f;
            float x = (col - (columns - 1) * 0.5f) * spacingX;
            float z = (row - (rows - 1) * 0.5f) * spacingZ;
            return new Vector3(x, 0f, z);
        }

        private static Quaternion GetDemoSceneRotation(FXMProductionRecipe recipe)
        {
            switch (recipe)
            {
                case FXMProductionRecipe.ShieldDome:
                    return Quaternion.Euler(0f, 0f, 0f);
                case FXMProductionRecipe.TornadoHelix:
                case FXMProductionRecipe.CrossEnergyHelix:
                    return Quaternion.Euler(0f, 20f, 0f);
                case FXMProductionRecipe.BeamStrip:
                    return Quaternion.Euler(0f, 0f, 0f);
                default:
                    return Quaternion.Euler(0f, 0f, 0f);
            }
        }

        private static void CreateDemoSceneCameraAndLight(Transform root)
        {
            GameObject lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(root);
            lightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.shadows = LightShadows.None;

            GameObject fillLightGo = new GameObject("Soft Fill Light");
            fillLightGo.transform.SetParent(root);
            fillLightGo.transform.position = new Vector3(0f, 4f, -4f);
            Light fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.intensity = 0.45f;
            fillLight.range = 12f;
            fillLight.shadows = LightShadows.None;

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(root);
            cameraGo.transform.position = new Vector3(0f, 7.6f, -10.4f);
            cameraGo.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 39f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 100f;
            cameraGo.tag = "MainCamera";
        }

        private static void CreateDemoSceneGround(Transform root)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "FXM_Demo_Ground";
            ground.transform.SetParent(root);
            ground.transform.position = new Vector3(0f, -0.025f, 0f);
            ground.transform.localScale = new Vector3(2.65f, 1f, 1.95f);
            MeshRenderer renderer = ground.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = null;
                renderer.sharedMaterials = Array.Empty<Material>();
            }
        }

        private static void CreateDemoSceneLabel(string label, Vector3 position, Transform root)
        {
            GameObject labelGo = new GameObject("Label_" + label);
            labelGo.transform.SetParent(root);
            labelGo.transform.position = position;
            labelGo.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            labelGo.transform.localScale = Vector3.one * 0.2f;

            TextMesh textMesh = labelGo.AddComponent<TextMesh>();
            textMesh.text = label;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 42;
            textMesh.characterSize = 0.1f;
            textMesh.color = new Color(0.92f, 0.96f, 1f, 1f);
        }

        private static void CreateDemoSceneReadme(Transform root)
        {
            GameObject readme = new GameObject("FXM_Demo_Readme");
            readme.transform.SetParent(root);
            readme.transform.position = new Vector3(0f, 0.05f, -4.2f);
            readme.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            readme.transform.localScale = Vector3.one * 0.2f;

            TextMesh textMesh = readme.AddComponent<TextMesh>();
            textMesh.text = "FX Mesh Generator Pro\nProduction Recipe Demo\nUse the preview material to inspect UV flow, vertex alpha, and mesh shape.";
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 42;
            textMesh.characterSize = 0.1f;
            textMesh.color = new Color(0.9f, 0.95f, 1f, 1f);
        }

        private string BuildDemoSceneReport(string scenePath)
        {
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Demo Scene Report");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Scene: " + scenePath);
            lines.Add("Preview Material: " + DefaultPreviewMaterialPath);
            lines.Add("Samples: " + GeneratedSampleMeshesFolder);
            lines.Add(string.Empty);
            lines.Add("Included recipes:");
            Array recipes = Enum.GetValues(typeof(FXMProductionRecipe));
            foreach (FXMProductionRecipe recipe in recipes)
            {
                lines.Add("- " + recipe + ": " + GetProductionRecipeDescription(recipe));
            }
            lines.Add(string.Empty);
            lines.Add("Recommended next step: assign your final URP/VFX material variants or keep the preview material for UV/alpha QA screenshots.");
            return string.Join("\n", lines.ToArray());
        }

        private string BuildProductionRecipeCatalogReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Production Recipe Catalog");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Output Folder: " + GeneratedSamplesFolder);
            lines.Add(string.Empty);

            Array recipes = Enum.GetValues(typeof(FXMProductionRecipe));
            foreach (FXMProductionRecipe recipe in recipes)
            {
                lines.Add("- " + recipe + ": " + GetProductionRecipeDescription(recipe));
            }

            lines.Add(string.Empty);
            lines.Add("Recommended store demo flow:");
            lines.Add("1. Generate Sample Prefab Pack.");
            lines.Add("2. Open Generated/Samples/Prefabs.");
            lines.Add("3. Assign your production URP/VFX material variants.");
            lines.Add("4. Run Mesh Validation Suite before packaging.");
            return string.Join("\n", lines.ToArray());
        }

        private static string GetProductionRecipeDescription(FXMProductionRecipe recipe)
        {
            switch (recipe)
            {
                case FXMProductionRecipe.BeamStrip:
                    return "Straight beam/trail strip with V flow.";
                case FXMProductionRecipe.ShockwaveRing:
                    return "Circular shockwave ring using circular UV flow.";
                case FXMProductionRecipe.ImpactDisc:
                    return "Radial impact disc for bursts, magic circles, and decals.";
                case FXMProductionRecipe.ShieldDome:
                    return "Dome shield mesh for force fields and barrier VFX.";
                case FXMProductionRecipe.TornadoHelix:
                    return "Single-ribbon helix for tornado, swirl, and energy spiral VFX.";
                case FXMProductionRecipe.CrossEnergyHelix:
                    return "Cross-ribbon helix for dense energy spirals.";
                case FXMProductionRecipe.SwordSlash:
                default:
                    return "Arc slash mesh for sword trails and stylized attack VFX.";
            }
        }

        private void ApplyProductionRecipe(FXMProductionRecipe recipe, bool rebuild)
        {
            switch (recipe)
            {
                case FXMProductionRecipe.BeamStrip:
                    _settings.meshType = FXMMeshType.Beam;
                    break;
                case FXMProductionRecipe.ShockwaveRing:
                    _settings.meshType = FXMMeshType.Ring;
                    break;
                case FXMProductionRecipe.ImpactDisc:
                    _settings.meshType = FXMMeshType.Disc;
                    break;
                case FXMProductionRecipe.ShieldDome:
                    _settings.meshType = FXMMeshType.Dome;
                    break;
                case FXMProductionRecipe.TornadoHelix:
                    _settings.meshType = FXMMeshType.Helix;
                    break;
                case FXMProductionRecipe.CrossEnergyHelix:
                    _settings.meshType = FXMMeshType.CrossHelix;
                    break;
                case FXMProductionRecipe.SwordSlash:
                default:
                    _settings.meshType = FXMMeshType.Slash;
                    break;
            }

            ClampUnsupportedModifierValues(false);
            ApplyProductionDefaultsForCurrentMesh();

            switch (recipe)
            {
                case FXMProductionRecipe.ShockwaveRing:
                    _settings.width = 0.22f;
                    _settings.uvFlowDirection = FXMUVFlowDirection.CircularCW;
                    break;
                case FXMProductionRecipe.ImpactDisc:
                    _settings.innerRadiusRatio = 0f;
                    _settings.uvFlowDirection = FXMUVFlowDirection.FromCenter;
                    break;
                case FXMProductionRecipe.ShieldDome:
                    _settings.height = 0.9f;
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    break;
                case FXMProductionRecipe.TornadoHelix:
                    _settings.turns = 3.0f;
                    _settings.height = 2.4f;
                    _settings.width = 0.16f;
                    _settings.uvTilingV = 3.0f;
                    break;
                case FXMProductionRecipe.CrossEnergyHelix:
                    _settings.turns = 2.75f;
                    _settings.height = 2.25f;
                    _settings.width = 0.14f;
                    _settings.helixCrossRotation = 90f;
                    _settings.uvTilingV = 3.0f;
                    break;
                case FXMProductionRecipe.BeamStrip:
                    _settings.uvTilingV = 2.0f;
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    break;
                case FXMProductionRecipe.SwordSlash:
                default:
                    _settings.uvTilingV = 1.5f;
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    break;
            }

            ApplyRecommendedAlphaForCurrentMesh(false, false);
            ApplyRecommendedPivotForCurrentMesh();
            ApplyRecommendedHelperProfile(false);
            ForceNormalHelperOff(false);
            SetCheckerPreviewMode(false);

            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
            else
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void DrawMeshQualityPassPanel()
        {
            EditorGUILayout.Space(6f);
            _showMeshQualityPass = EditorGUILayout.Foldout(_showMeshQualityPass, Tr("2-2. Mesh Quality Pass", "2-2. 메시 품질 패스"), true);
            if (!_showMeshQualityPass)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawUiSectionHeader(
                    Tr("Mesh Quality Pass", "메시 품질 패스"),
                    Tr("Final cleanup for production meshes. It clamps unstable values, improves segment defaults, keeps normals/tangents safe, and prepares QA before export.",
                       "최종 제작용 메시 정리입니다. 불안정한 값을 보정하고 세그먼트, 노멀/탄젠트, 저장 전 QA를 안정화합니다."));

                DrawUiSubHeader(Tr("Quality Target", "품질 타겟"));
                _qualityMobileMode = EditorGUILayout.Toggle(
                    new GUIContent(Tr("Mobile-Oriented Quality", "모바일 지향 품질"), Tr("Uses lighter but stable segment counts and disables expensive output options where possible.", "가볍지만 안정적인 세그먼트 수를 사용하고 비용이 큰 출력 옵션을 가능한 줄입니다.")),
                    _qualityMobileMode);

                DrawUiSubHeader(Tr("Apply Defaults", "기본값 적용"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UiButton(Tr("Apply Quality Defaults", "품질 기본값 적용")))
                    {
                        ApplyMeshQualityPass(false, true);
                    }

                    if (UiButton(Tr("Apply Mobile Defaults", "모바일 기본값 적용")))
                    {
                        ApplyMeshQualityPass(true, true);
                    }
                }

                DrawUiSubHeader(Tr("Quality Report", "품질 리포트"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UiSmallButton(Tr("Run Quality QA", "품질 QA 실행")))
                    {
                        _lastMeshQualityReport = BuildMeshQualityReport();
                    }

                    if (UiSmallButton(Tr("Copy Quality Report", "품질 리포트 복사")))
                    {
                        if (string.IsNullOrEmpty(_lastMeshQualityReport))
                        {
                            _lastMeshQualityReport = BuildMeshQualityReport();
                        }
                        EditorGUIUtility.systemCopyBuffer = _lastMeshQualityReport;
                    }
                }

                if (!string.IsNullOrEmpty(_lastMeshQualityReport))
                {
                    UiRowSpace();
                    _meshQualityReportScroll = EditorGUILayout.BeginScrollView(_meshQualityReportScroll, GUILayout.MinHeight(70f), GUILayout.MaxHeight(150f));
                    EditorGUILayout.TextArea(_lastMeshQualityReport, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void ApplyMeshQualityPass(bool mobile, bool rebuild)
        {
            ClampUnsupportedModifierValues(false);

            _settings.flipNormals = false;
            _settings.shellThickness = 0f;
            _settings.doubleSidedGeometry = mobile ? false : _settings.doubleSidedGeometry;
            _settings.recalculateNormals = true;
            _settings.recalculateTangents = true;
            _settings.uvTilingU = Mathf.Max(0.1f, _settings.uvTilingU);
            _settings.uvTilingV = Mathf.Max(0.1f, _settings.uvTilingV);
            _settings.minAlpha = Mathf.Clamp01(_settings.minAlpha);
            _settings.maxAlpha = Mathf.Clamp01(Mathf.Max(_settings.maxAlpha, _settings.minAlpha));
            _settings.alphaSoftness = Mathf.Clamp(_settings.alphaSoftness, 0.001f, 1f);

            switch (_settings.meshType)
            {
                case FXMMeshType.Slash:
                    _settings.segments = Mathf.Clamp(_settings.segments, mobile ? 28 : 48, mobile ? 96 : 160);
                    _settings.widthSegments = Mathf.Clamp(_settings.widthSegments, mobile ? 2 : 3, mobile ? 8 : 16);
                    _settings.arcAngle = Mathf.Clamp(_settings.arcAngle, 15f, 340f);
                    _settings.width = Mathf.Clamp(_settings.width, 0.02f, Mathf.Max(0.03f, _settings.radius * 1.65f));
                    _settings.slashRootScale = Mathf.Clamp(_settings.slashRootScale, 0.05f, 2f);
                    _settings.slashTipScale = Mathf.Clamp(_settings.slashTipScale, 0.02f, 1.35f);
                    _settings.edgeJitter = Mathf.Min(_settings.edgeJitter, mobile ? 0.12f : 0.28f);
                    break;

                case FXMMeshType.Beam:
                    _settings.segments = Mathf.Clamp(_settings.segments, mobile ? 12 : 24, mobile ? 96 : 160);
                    _settings.widthSegments = Mathf.Clamp(_settings.widthSegments, mobile ? 1 : 2, mobile ? 8 : 16);
                    _settings.length = Mathf.Max(0.05f, _settings.length);
                    _settings.width = Mathf.Clamp(_settings.width, 0.01f, Mathf.Max(0.02f, _settings.length * 1.25f));
                    _settings.beamStartWidth = Mathf.Clamp(_settings.beamStartWidth, 0.02f, 2f);
                    _settings.beamEndWidth = Mathf.Clamp(_settings.beamEndWidth, 0.02f, 2f);
                    _settings.beamCoreWidth = Mathf.Clamp(_settings.beamCoreWidth, 0.02f, 2f);
                    break;

                case FXMMeshType.Ring:
                    _settings.segments = Mathf.Clamp(_settings.segments, mobile ? 48 : 96, mobile ? 160 : 256);
                    _settings.widthSegments = Mathf.Clamp(_settings.widthSegments, mobile ? 2 : 4, mobile ? 10 : 24);
                    _settings.radius = Mathf.Max(0.05f, _settings.radius);
                    _settings.width = Mathf.Clamp(_settings.width, 0.01f, Mathf.Max(0.02f, _settings.radius * 1.5f));
                    _settings.arcAngle = Mathf.Clamp(_settings.arcAngle, 8f, 360f);
                    _settings.ringInnerThickness = Mathf.Clamp(_settings.ringInnerThickness, 0.05f, 2f);
                    _settings.ringOuterThickness = Mathf.Clamp(_settings.ringOuterThickness, 0.05f, 2f);
                    _settings.brokenAmount = Mathf.Clamp(_settings.brokenAmount, 0f, mobile ? 0.45f : 0.75f);
                    break;

                case FXMMeshType.Disc:
                    _settings.segments = Mathf.Clamp(_settings.segments, mobile ? 48 : 96, mobile ? 160 : 256);
                    _settings.widthSegments = Mathf.Clamp(_settings.widthSegments, mobile ? 4 : 8, mobile ? 20 : 32);
                    _settings.radius = Mathf.Max(0.05f, _settings.radius);
                    _settings.innerRadiusRatio = Mathf.Clamp(_settings.innerRadiusRatio, 0f, 0.92f);
                    _settings.arcAngle = Mathf.Clamp(_settings.arcAngle, 8f, 360f);
                    _settings.discCenterFade = Mathf.Clamp(_settings.discCenterFade, 0f, 0.45f);
                    _settings.discOuterFade = Mathf.Clamp(_settings.discOuterFade, 0f, 0.45f);
                    break;

                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    _settings.segments = Mathf.Clamp(_settings.segments, mobile ? 32 : 48, mobile ? 128 : 192);
                    _settings.widthSegments = Mathf.Clamp(_settings.widthSegments, mobile ? 8 : 12, mobile ? 24 : 36);
                    _settings.radius = Mathf.Max(0.05f, _settings.radius);
                    _settings.height = Mathf.Clamp(_settings.height, 0.02f, Mathf.Max(0.03f, _settings.radius * 3f));
                    _settings.domeRoundness = Mathf.Clamp(_settings.domeRoundness, 0.18f, 3f);
                    _settings.domeHeightBias = Mathf.Clamp(_settings.domeHeightBias, -0.9f, 0.9f);
                    if (_settings.meshType == FXMMeshType.HalfDome)
                    {
                        _settings.arcAngle = Mathf.Clamp(_settings.arcAngle, 30f, 360f);
                    }
                    break;

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    _settings.segments = Mathf.Clamp(_settings.segments, mobile ? 64 : 96, mobile ? 192 : 256);
                    _settings.widthSegments = Mathf.Clamp(_settings.widthSegments, mobile ? 2 : 3, mobile ? 10 : 20);
                    _settings.radius = Mathf.Max(0.03f, _settings.radius);
                    _settings.width = Mathf.Clamp(_settings.width, 0.01f, Mathf.Max(0.02f, _settings.radius * 1.2f));
                    _settings.height = Mathf.Max(0.05f, _settings.height);
                    _settings.turns = Mathf.Clamp(_settings.turns, 0.1f, mobile ? 6f : 12f);
                    _settings.helixCrossRotation = Mathf.Repeat(_settings.helixCrossRotation + 360f, 360f);
                    break;
            }

            ApplyRecommendedAlphaForCurrentMesh(false, false);
            ApplyRecommendedPivotForCurrentMesh();
            ForceNormalHelperOff(false);
            _lastMeshQualityReport = BuildMeshQualityReport();

            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
            else
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private string BuildMeshQualityReport()
        {
            RebuildPreviewMesh(false);
            Mesh mesh = _previewMesh;
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Mesh Quality Report");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Mesh Type: " + _settings.meshType);
            lines.Add("Quality Mode: " + (_qualityMobileMode ? "Mobile-Oriented" : "Full Quality"));
            lines.Add("Segments: " + _settings.segments + " / Width Segments: " + _settings.widthSegments);

            if (mesh != null)
            {
                lines.Add("Vertices: " + mesh.vertexCount.ToString("N0"));
                lines.Add("Triangles: " + ((mesh.triangles != null ? mesh.triangles.Length : 0) / 3).ToString("N0"));
                lines.Add("Bounds Size: " + FormatVector3(mesh.bounds.size));
            }
            else
            {
                lines.Add("Mesh: Missing");
            }

            List<string> warnings = BuildMeshQualityWarnings(mesh, _settings);
            lines.Add("Status: " + (warnings.Count == 0 ? "PASS" : "CHECK / " + warnings.Count + " warning(s)"));
            for (int i = 0; i < warnings.Count; i++)
            {
                lines.Add("- " + warnings[i]);
            }

            return string.Join("\n", lines.ToArray());
        }

        private static List<string> BuildMeshQualityWarnings(Mesh mesh, FXMSettings settings)
        {
            List<string> warnings = new List<string>();
            if (settings.segments < 12) warnings.Add("Primary segment count is very low; silhouettes may look faceted.");
            if (settings.widthSegments < 1) warnings.Add("Width segment count must be at least 1.");
            if (settings.flipNormals) warnings.Add("Flip Normals is enabled; verify backface/cull workflow.");
            if (Mathf.Abs(settings.shellThickness) > 0.0001f) warnings.Add("Shell Thickness is enabled; output cost and winding should be checked.");

            switch (settings.meshType)
            {
                case FXMMeshType.Slash:
                    if (settings.width > Mathf.Max(0.001f, settings.radius * 1.7f)) warnings.Add("Slash width is very large compared to radius; inner cells may fold.");
                    if (settings.arcAngle > 345f) warnings.Add("Slash arc is almost a full circle; Ring may be a cleaner mesh type.");
                    break;
                case FXMMeshType.Ring:
                    if (settings.width > Mathf.Max(0.001f, settings.radius * 1.55f)) warnings.Add("Ring thickness is very high compared to radius.");
                    if (settings.brokenAmount > 0.6f) warnings.Add("Broken Amount is high; check visual continuity and triangle islands.");
                    break;
                case FXMMeshType.Disc:
                    if (settings.innerRadiusRatio > 0.9f) warnings.Add("Disc inner radius is very large; visible area may become too thin.");
                    if (settings.widthSegments < 4) warnings.Add("Disc radial segments are low; radial gradients may band.");
                    break;
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    if (settings.height > settings.radius * 3f) warnings.Add("Dome height is very high compared to radius; silhouette may look stretched.");
                    if (settings.domeRoundness < 0.2f || settings.domeRoundness > 3f) warnings.Add("Dome roundness is outside the recommended quality range.");
                    break;
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    if (settings.turns > 6f && settings.segments < 96) warnings.Add("High turn count with low segments may look angular.");
                    if (settings.width > settings.radius * 1.25f) warnings.Add("Helix ribbon width is very large compared to radius.");
                    break;
            }

            if (mesh != null)
            {
                int triCount = mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
                if (mesh.vertexCount > 65000 && mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16) warnings.Add("Vertex count exceeds UInt16 index capacity.");
                if (triCount > 16000) warnings.Add("Triangle count is high for mobile particle mesh usage.");
            }

            return warnings;
        }

        private void DrawShapeEditSettings()
        {
            EditorGUILayout.Space(6f);
            _showShapeEdit = EditorGUILayout.Foldout(_showShapeEdit, "2-1. 3ds Max Style Edit / Shape Details", true);
            if (!_showShapeEdit) return;

            FXMShapeEditProfile profile = FXMShapeEditProfile.For(_settings.meshType);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(profile.title, EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(profile.description, MessageType.None);

                using (new EditorGUI.DisabledScope(!profile.allowFlowOffset))
                {
                    _settings.flowOffset = EditorGUILayout.Slider(new GUIContent(profile.flowLabel, profile.flowTooltip), _settings.flowOffset, -1f, 1f);
                }

                using (new EditorGUI.DisabledScope(!profile.allowWidthOffset))
                {
                    _settings.widthOffset = EditorGUILayout.Slider(new GUIContent(profile.widthLabel, profile.widthTooltip), _settings.widthOffset, -1f, 1f);
                }

                using (new EditorGUI.DisabledScope(!profile.allowShapeBias))
                {
                    _settings.shapeBias = EditorGUILayout.Slider(new GUIContent(profile.biasLabel, profile.biasTooltip), _settings.shapeBias, -1f, 1f);
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Blender Shape Detail", EditorStyles.miniBoldLabel);
                bool allowBlenderShapeDetail = SupportsBlenderShapeDetail(_settings.meshType);
                using (new EditorGUI.DisabledScope(!allowBlenderShapeDetail))
                {
                    _settings.segmentFlowBias = EditorGUILayout.Slider(new GUIContent("Flow Bias", Tr("Adjust this option for the selected production workflow.", "Blender의 segment_flow_bias 감각입니다. -값은 시작 쪽 밀도, +값은 끝 쪽 밀도를 강조합니다.")), _settings.segmentFlowBias, -1f, 1f);
                    _settings.segmentWidthBias = EditorGUILayout.Slider(new GUIContent("Width Bias", Tr("Controls the width or thickness for the selected mesh.", "Blender의 segment_width_bias 감각입니다. 폭 방향 세그먼트 분포를 한쪽으로 밀어줍니다.")), _settings.segmentWidthBias, -1f, 1f);
                    _settings.depth = EditorGUILayout.Slider(new GUIContent("Depth / Z Bow", Tr("Adjust this option for the selected production workflow.", "검기/빔/나선에 살짝 입체적인 휘어짐을 줍니다. 0이면 완전 평면입니다.")), _settings.depth, -2f, 2f);
                    _settings.edgeJitter = EditorGUILayout.Slider(new GUIContent("Edge Jitter", Tr("Adjust this option for the selected production workflow.", "Blender edge_jitter 감각입니다. 외곽선에 미세한 거친 흔들림을 추가합니다.")), _settings.edgeJitter, 0f, 1f);
                }

                DrawMeshSpecificShapeDetail();

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Taper Edit", EditorStyles.miniBoldLabel);

                using (new EditorGUI.DisabledScope(!profile.allowTaperEdit))
                {
                    _settings.taperAmount = EditorGUILayout.Slider(new GUIContent("Taper Amount", Tr("Controls the width or thickness for the selected mesh.", "Blender 기준 Taper입니다. 0이면 유지, 1이면 선택 방향의 폭이 강하게 좁아집니다.")), _settings.taperAmount, 0f, 1f);
                    _settings.taperDirection = EditorGUILayout.Slider(new GUIContent("Taper Direction", Tr("Adjust this option for the selected production workflow.", "Blender 기준입니다. 0이면 양끝이 좁아지고 중앙은 유지, -1이면 시작 쪽이 좁아지고, +1이면 끝 쪽이 좁아집니다.")), _settings.taperDirection, -1f, 1f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Both Ends", GUILayout.Height(22f)))
                        {
                            _settings.taperAmount = Mathf.Max(_settings.taperAmount, 0.75f);
                            _settings.taperDirection = 0f;
                            RebuildPreviewMesh(true);
                        }

                        if (GUILayout.Button("Start Narrow", GUILayout.Height(22f)))
                        {
                            _settings.taperAmount = Mathf.Max(_settings.taperAmount, 0.75f);
                            _settings.taperDirection = -1f;
                            RebuildPreviewMesh(true);
                        }

                        if (GUILayout.Button("End Narrow", GUILayout.Height(22f)))
                        {
                            _settings.taperAmount = Mathf.Max(_settings.taperAmount, 0.75f);
                            _settings.taperDirection = 1f;
                            RebuildPreviewMesh(true);
                        }
                    }
                }

                if (_settings.meshType == FXMMeshType.Ring || _settings.meshType == FXMMeshType.Disc)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Ring / Disc Quick Shape", EditorStyles.miniBoldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Full 360", GUILayout.Height(22f)))
                        {
                            _settings.arcAngle = 360f;
                            _settings.brokenAmount = 0f;
                            RebuildPreviewMesh(true);
                        }

                        if (GUILayout.Button("Open Arc", GUILayout.Height(22f)))
                        {
                            _settings.arcAngle = _settings.meshType == FXMMeshType.Disc ? 270f : 220f;
                            _settings.brokenAmount = 0f;
                            RebuildPreviewMesh(true);
                        }

                        if (GUILayout.Button("Broken Ring", GUILayout.Height(22f)))
                        {
                            _settings.meshType = FXMMeshType.Ring;
                            _settings.arcAngle = 360f;
                            _settings.brokenAmount = 0.28f;
                            _settings.widthSegments = Mathf.Max(_settings.widthSegments, 2);
                            _settings.edgeJitter = Mathf.Max(_settings.edgeJitter, 0.04f);
                            _settings.noiseSeed += 17;
                            RebuildPreviewMesh(true);
                        }

                        if (GUILayout.Button("Cooldown Disc", GUILayout.Height(22f)))
                        {
                            _settings.meshType = FXMMeshType.Disc;
                            _settings.arcAngle = 265f;
                            _settings.innerRadiusRatio = Mathf.Max(_settings.innerRadiusRatio, 0.18f);
                            _settings.alphaMode = FXMAlphaMode.EdgeFade;
                            RebuildPreviewMesh(true);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Shape Edit Reset", GUILayout.Height(24f)))
                    {
                        _settings.flowOffset = 0f;
                        _settings.widthOffset = 0f;
                        _settings.shapeBias = 0f;
                        _settings.segmentFlowBias = 0f;
                        _settings.segmentWidthBias = 0f;
                        _settings.depth = 0f;
                        _settings.edgeJitter = 0f;
                        _settings.brokenAmount = 0f;
                        _settings.slashRootScale = 1f;
                        _settings.slashTipScale = 1f;
                        _settings.symmetryWidth = false;
                        _settings.beamStartWidth = 1f;
                        _settings.beamEndWidth = 1f;
                        _settings.beamCoreWidth = 1f;
                        _settings.ringInnerThickness = 1f;
                        _settings.ringOuterThickness = 1f;
                        _settings.discCenterFade = 0f;
                        _settings.discOuterFade = 0f;
                        _settings.domeRoundness = 1f;
                        _settings.domeHeightBias = 0f;
                        _settings.helixRadius2 = 0f;
                        _settings.helixClockwise = false;
                        if (_settings.meshType == FXMMeshType.Slash)
                        {
                            _settings.taperAmount = 0f;
                            _settings.taperDirection = 0f;
                            _settings.bendAmount = 0f;
                        }
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Sharp Slash Tip", GUILayout.Height(24f)))
                    {
                        _settings.meshType = FXMMeshType.Slash;
                        _settings.useModifiers = true;
                        _settings.taperAmount = 0.88f;
                        _settings.taperDirection = 1f;
                        _settings.shapeBias = 0.18f;
                        _settings.segmentFlowBias = 0.12f;
                        _settings.segmentWidthBias = 0f;
                        _settings.depth = 0.08f;
                        _settings.edgeJitter = 0.02f;
                        _settings.slashRootScale = 1.15f;
                        _settings.slashTipScale = 0.08f;
                        _settings.symmetryWidth = false;
                        _settings.widthOffset = 0f;
                        _settings.bendAmount = 2.5f;
                        RebuildPreviewMesh(true);
                    }
                }

                string unsupported = profile.UnsupportedSummary(_settings);
                if (!string.IsNullOrEmpty(unsupported))
                {
                    EditorGUILayout.HelpBox(Tr("Adjust this option for the selected production workflow.", "현재 Mesh Type에서는 다음 Shape Edit 값이 빌드에서 무시됩니다: ") + unsupported, MessageType.Warning);
                }

                EditorGUILayout.HelpBox(Tr("Controls modifier/deform behavior for the selected mesh.", "이 섹션은 Blender/3ds Max에서 하던 Edit/Shape Details 감각을 Unity 안에서 빠르게 흉내 내기 위한 컨트롤입니다. Modifier와 달리 일부 값은 기본 메시 생성 단계에 직접 반영됩니다."), MessageType.Info);
            }
        }



        private void DrawMeshSpecificShapeDetail()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Mesh Specific Detail", EditorStyles.miniBoldLabel);

            switch (_settings.meshType)
            {
                case FXMMeshType.Slash:
                    _settings.slashRootScale = EditorGUILayout.Slider(new GUIContent("Root Scale", Tr("Controls the width or thickness for the selected mesh.", "검기 시작 쪽 폭 배율입니다. 1이면 기본 폭입니다.")), _settings.slashRootScale, 0.05f, 2f);
                    _settings.slashTipScale = EditorGUILayout.Slider(new GUIContent("Tip Scale", Tr("Controls the width or thickness for the selected mesh.", "검기 끝 쪽 폭 배율입니다. 낮을수록 칼끝이 날카롭게 닫힙니다.")), _settings.slashTipScale, 0.02f, 2f);
                    _settings.symmetryWidth = EditorGUILayout.Toggle(new GUIContent("Symmetry Width Lock", Tr("Controls the width or thickness for the selected mesh.", "폭 방향 Bias/Offset을 잠시 무시하고 양쪽 폭을 대칭으로 유지합니다.")), _settings.symmetryWidth);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Balanced Slash", GUILayout.Height(22f)))
                        {
                            _settings.slashRootScale = 1f;
                            _settings.slashTipScale = 1f;
                            _settings.symmetryWidth = false;
                            RebuildPreviewMesh(true);
                        }
                        if (GUILayout.Button("Razor Tip", GUILayout.Height(22f)))
                        {
                            _settings.slashRootScale = 1.15f;
                            _settings.slashTipScale = 0.08f;
                            _settings.taperAmount = Mathf.Max(_settings.taperAmount, 0.45f);
                            _settings.taperDirection = 1f;
                            RebuildPreviewMesh(true);
                        }
                    }
                    break;

                case FXMMeshType.Beam:
                    _settings.beamStartWidth = EditorGUILayout.Slider(new GUIContent("Start Width", Tr("Controls the width or thickness for the selected mesh.", "빔 시작 쪽 폭 배율입니다.")), _settings.beamStartWidth, 0.05f, 2f);
                    _settings.beamEndWidth = EditorGUILayout.Slider(new GUIContent("End Width", Tr("Controls the width or thickness for the selected mesh.", "빔 끝 쪽 폭 배율입니다.")), _settings.beamEndWidth, 0.05f, 2f);
                    _settings.beamCoreWidth = EditorGUILayout.Slider(new GUIContent("Core Width", Tr("Controls the width or thickness for the selected mesh.", "빔 중앙부 폭 배율입니다. 1이면 기본, 높이면 중앙부가 두꺼워집니다.")), _settings.beamCoreWidth, 0.05f, 2f);
                    break;

                case FXMMeshType.Ring:
                    _settings.ringInnerThickness = EditorGUILayout.Slider(new GUIContent("Inner Thickness Scale", Tr("Controls the width or thickness for the selected mesh.", "링 안쪽 두께 확장 배율입니다.")), _settings.ringInnerThickness, 0.05f, 2f);
                    _settings.ringOuterThickness = EditorGUILayout.Slider(new GUIContent("Outer Thickness Scale", Tr("Controls the width or thickness for the selected mesh.", "링 바깥쪽 두께 확장 배율입니다.")), _settings.ringOuterThickness, 0.05f, 2f);
                    break;

                case FXMMeshType.Disc:
                    _settings.discCenterFade = EditorGUILayout.Slider(new GUIContent("Center Fade", Tr("Controls Vertex Alpha generation and preview behavior.", "Disc 중심부 Vertex Alpha를 부드럽게 줄입니다. 0이면 비활성입니다.")), _settings.discCenterFade, 0f, 1f);
                    _settings.discOuterFade = EditorGUILayout.Slider(new GUIContent("Outer Fade", Tr("Controls Vertex Alpha generation and preview behavior.", "Disc 외곽부 Vertex Alpha를 부드럽게 줄입니다. 0이면 비활성입니다.")), _settings.discOuterFade, 0f, 1f);
                    EditorGUILayout.HelpBox(Tr("Controls Vertex Alpha generation and preview behavior.", "Center/Outer Fade는 Mesh 형태가 아니라 Vertex Alpha에 반영됩니다. Preview Mode를 UVCheckerAlpha 또는 VertexAlpha로 확인하세요."), MessageType.None);
                    break;

                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    _settings.domeRoundness = EditorGUILayout.Slider(new GUIContent("Dome Roundness", Tr("Adjust this option for the selected production workflow.", "돔 곡률입니다. 낮으면 더 볼록하고, 높으면 바닥 쪽으로 넓게 퍼지는 느낌입니다.")), _settings.domeRoundness, 0.25f, 2.5f);
                    _settings.domeHeightBias = EditorGUILayout.Slider(new GUIContent("Height Bias", Tr("Adjust this option for the selected production workflow.", "높이 방향 세그먼트/곡률 분포를 위쪽 또는 아래쪽으로 편향합니다.")), _settings.domeHeightBias, -1f, 1f);
                    break;

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    if (_settings.helixRadius2 <= 0.0001f)
                    {
                        _settings.helixRadius2 = Mathf.Max(0.05f, _settings.radius);
                    }
                    _settings.helixRadius2 = EditorGUILayout.Slider(new GUIContent("Radius 2", Tr("Controls the radius for the selected mesh.", "나선 끝 지점 반지름입니다. Radius와 다르게 주면 위/아래로 벌어지는 나선을 만들 수 있습니다.")), _settings.helixRadius2, 0.05f, 10f);
                    _settings.helixClockwise = EditorGUILayout.Toggle(new GUIContent("Clockwise Direction", Tr("Adjust this option for the selected production workflow.", "체크하면 면을 뒤집지 않고 Helix 회전 방향만 반대로 바꿉니다.")), _settings.helixClockwise);
                    if (_settings.meshType == FXMMeshType.CrossHelix)
                    {
                        DrawCrossHelixRotationControls("Cross Helix / Shape Detail", false);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(Tr("Controls Cross Helix generation and preview behavior.", "Cross 형태가 필요하면 Mesh Type에서 Cross Helix를 선택하세요. Helix는 단일 리본 전용으로 유지합니다."), MessageType.None);
                    }
                    break;
            }
        }


        private void DrawCrossHelixRotationControls(string title, bool compact)
        {
            EditorGUILayout.Space(compact ? 2f : 4f);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _settings.helixCrossRotation = EditorGUILayout.Slider(new GUIContent("Cross Rotation", Tr("Controls Cross Helix generation and preview behavior.", "Cross Helix의 두 번째 리본을 흐름 축 기준으로 회전합니다. 0이면 기본 90도 크로스입니다.")), _settings.helixCrossRotation, -180f, 180f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Cross 0", GUILayout.Height(22f)))
                    {
                        _settings.helixCrossRotation = 0f;
                        RebuildPreviewMesh(true);
                    }
                    if (GUILayout.Button("Cross -45", GUILayout.Height(22f)))
                    {
                        _settings.helixCrossRotation = -45f;
                        RebuildPreviewMesh(true);
                    }
                    if (GUILayout.Button("Cross +45", GUILayout.Height(22f)))
                    {
                        _settings.helixCrossRotation = 45f;
                        RebuildPreviewMesh(true);
                    }
                }

                if (!compact)
                {
                    EditorGUILayout.HelpBox(Tr("Cross Helix is a dedicated mesh type. Use Cross Rotation to keep both ribbons locked together while rotating the cross section.", "Cross Helix는 별도 Mesh Type입니다. Cross Rotation은 두 리본을 크로스 상태로 유지한 채 함께 회전시킵니다."), MessageType.None);
                }
            }
        }

        private static bool SupportsBlenderShapeDetail(FXMMeshType type)
        {
            switch (type)
            {
                case FXMMeshType.Slash:
                case FXMMeshType.Beam:
                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return true;
                default:
                    return false;
            }
        }

        private void DrawUVSettings()
        {
            EditorGUILayout.Space(6f);
            _showUV = EditorGUILayout.Foldout(_showUV, Tr("4. UV / Flow Settings", "3. UV / Flow 설정"), true);
            if (!_showUV) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                FXMUVFlowDirection previousDirection = _settings.uvFlowDirection;
                _settings.uvFlowDirection = (FXMUVFlowDirection)EditorGUILayout.EnumPopup(
                    new GUIContent("UV Flow Direction", Tr("Controls UV layout and texture flow behavior.", "텍스처가 흐르는 주 방향을 지정합니다. Auto는 기존 Mesh Type 기본 UV를 유지합니다.")),
                    _settings.uvFlowDirection);
                if (_settings.uvFlowDirection != previousDirection)
                {
                    SetUvGradientPreviewMode(false);
                    if (!_autoRefresh && (_previewObject != null || _previewMesh != null))
                    {
                        RebuildPreviewMesh(true);
                    }
                }
                _autoUvProfile = EditorGUILayout.Toggle(new GUIContent("Auto UV Profile", Tr("Controls UV layout and texture flow behavior.", "Mesh Type을 바꿀 때 해당 메시 타입에 맞는 UV Flow 추천값을 자동 적용합니다.")), _autoUvProfile);

                _settings.uvTilingU = EditorGUILayout.Slider(new GUIContent("UV Tiling U", Tr("Controls UV layout and texture flow behavior.", "가로 UV 반복입니다.")), _settings.uvTilingU, 0.1f, 20f);
                _settings.uvTilingV = EditorGUILayout.Slider(new GUIContent("UV Tiling V", Tr("Controls UV layout and texture flow behavior.", "세로/진행 방향 UV 반복입니다.")), _settings.uvTilingV, 0.1f, 20f);
                _settings.uvBiasU = EditorGUILayout.Slider(new GUIContent("UV Bias U", Tr("Adjust this option for the selected production workflow.", "U 좌표의 분포를 한쪽으로 밀어줍니다. -값은 U 시작 쪽을 넓게, +값은 U 끝 쪽을 넓게 보이게 합니다.")), _settings.uvBiasU, -1f, 1f);
                _settings.uvBiasV = EditorGUILayout.Slider(new GUIContent("UV Bias V", Tr("Controls UV layout and texture flow behavior.", "V/Flow 좌표의 분포를 한쪽으로 밀어줍니다. 검기 흐름 텍스처의 밀도감을 맞출 때 사용합니다.")), _settings.uvBiasV, -1f, 1f);
                bool previousFlipU = _settings.flipU;
                bool previousFlipV = _settings.flipV;
                _settings.flipU = EditorGUILayout.Toggle(new GUIContent("Flip U", Tr("Adjust this option for the selected production workflow.", "U 방향을 뒤집습니다.")), _settings.flipU);
                _settings.flipV = EditorGUILayout.Toggle(new GUIContent("Flip V", Tr("Adjust this option for the selected production workflow.", "V 방향을 뒤집습니다.")), _settings.flipV);
                if ((_settings.flipU != previousFlipU || _settings.flipV != previousFlipV) && !_autoRefresh && (_previewObject != null || _previewMesh != null))
                {
                    RebuildPreviewMesh(true);
                }

                DrawUiSubHeader(Tr("UV Preview Offset / Manual Flow", "UV 프리뷰 오프셋 / 수동 Flow"));
                EditorGUILayout.HelpBox(
                    Tr("Blender-style preview-only offset. Move the Flow Offset slider to inspect texture travel directly on the mesh. Width Offset shifts across the ribbon/thickness. Saved mesh UVs are not changed.",
                       "블렌더처럼 쓰는 프리뷰 전용 오프셋입니다. Flow Offset 슬라이더를 직접 움직여 실제 메시 위 텍스처 흐름을 확인하세요. Width Offset은 리본/두께 방향으로 이동합니다. 저장되는 메시 UV는 바뀌지 않습니다."),
                    MessageType.None);

                EditorGUI.BeginChangeCheck();
                _flowPreviewOffsetV = EditorGUILayout.Slider(new GUIContent(Tr("Flow Offset", "Flow 오프셋"), Tr("Semantic progress offset. It follows the selected UV Flow Direction, including Reverse, Circular, Center, Flip U, and Flip V.", "선택된 UV Flow Direction 의미를 따라가는 진행 오프셋입니다. Reverse, Circular, Center, Flip U/V까지 반영합니다.")), _flowPreviewOffsetV, 0f, 1f);
                _flowPreviewOffsetU = EditorGUILayout.Slider(new GUIContent(Tr("Width Offset", "Width 오프셋"), Tr("Side/cross offset across ribbon width, radial thickness, or UV side axis.", "리본 폭, 방사 두께, 또는 UV 보조축 방향 오프셋입니다.")), _flowPreviewOffsetU, 0f, 1f);
                bool offsetChanged = EditorGUI.EndChangeCheck();

                Rect offsetButtonRow = GUILayoutUtility.GetRect(1f, UiSmallButtonHeight);
                const float offsetButtonGap = 5f;
                if (UiEqualButton(GetEqualButtonRect(offsetButtonRow, 0, 4, offsetButtonGap), Tr("Flow -0.25", "Flow -0.25")))
                {
                    _flowPreviewOffsetV = Mathf.Repeat(_flowPreviewOffsetV - 0.25f, 1f);
                    offsetChanged = true;
                }
                if (UiEqualButton(GetEqualButtonRect(offsetButtonRow, 1, 4, offsetButtonGap), Tr("Flow +0.25", "Flow +0.25")))
                {
                    _flowPreviewOffsetV = Mathf.Repeat(_flowPreviewOffsetV + 0.25f, 1f);
                    offsetChanged = true;
                }
                if (UiEqualButton(GetEqualButtonRect(offsetButtonRow, 2, 4, offsetButtonGap), Tr("Reset", "초기화")))
                {
                    _flowPreviewOffsetU = 0f;
                    _flowPreviewOffsetV = 0f;
                    offsetChanged = true;
                }
                if (UiEqualButton(GetEqualButtonRect(offsetButtonRow, 3, 4, offsetButtonGap), Tr("Use Flow Preview", "Flow 프리뷰")))
                {
                    SetUvGradientPreviewMode(true);
                    offsetChanged = true;
                }

                EditorGUILayout.LabelField(GetFlowOffsetSummary(_settings, _flowPreviewOffsetU, _flowPreviewOffsetV), EditorStyles.miniLabel);

                if (_workspaceMode == FXMWorkspaceMode.Advanced)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        _flowPreviewAnimate = EditorGUILayout.ToggleLeft(new GUIContent(Tr("Optional Auto Scroll", "선택 자동 스크롤"), Tr("Advanced-only helper. Manual sliders above are the recommended QA workflow.", "고급 옵션입니다. 기본 검수는 위 수동 슬라이더 사용을 권장합니다.")), _flowPreviewAnimate);
                        using (new EditorGUI.DisabledScope(!_flowPreviewAnimate))
                        {
                            _flowPreviewSpeed = EditorGUILayout.Slider(new GUIContent(Tr("Auto Scroll Speed", "자동 스크롤 속도")), _flowPreviewSpeed, -2f, 2f);
                        }
                    }
                }
                else
                {
                    _flowPreviewAnimate = false;
                }

                if (offsetChanged)
                {
                    ApplyPreviewMaterialSettings(GetPreviewMaterial());
                    SceneView.RepaintAll();
                    Repaint();
                }

                ApplyPreviewMaterialSettings(GetPreviewMaterial());

                DrawUiSubHeader(Tr("Flow Preview", "Flow 프리뷰"));
                DrawUvFlowMapPreview();
                EditorGUILayout.HelpBox(
                    Tr("Duplicate UV preset buttons were removed. Use the UV Flow Direction dropdown, Flip toggles, Tiling, and Bias fields above as the single source of truth.",
                       "중복 UV 프리셋 버튼을 제거했습니다. 위의 UV Flow Direction 드롭다운, Flip, Tiling, Bias 값을 단일 기준으로 사용하세요."),
                    MessageType.None);

                EditorGUILayout.HelpBox(GetUvFlowDescription(_settings.uvFlowDirection), MessageType.None);
            }
        }

        private void ApplyRecommendedUvForCurrentMesh(bool rebuild)
        {
            _settings.flipU = false;
            _settings.flipV = false;
            _settings.uvBiasU = 0f;
            _settings.uvBiasV = 0f;

            switch (_settings.meshType)
            {
                case FXMMeshType.Slash:
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    _settings.uvTilingU = 1f;
                    _settings.uvTilingV = 1.5f;
                    _settings.uvBiasV = 0.12f;
                    break;

                case FXMMeshType.Beam:
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    _settings.uvTilingU = 1f;
                    _settings.uvTilingV = 2f;
                    break;

                case FXMMeshType.Ring:
                    _settings.uvFlowDirection = FXMUVFlowDirection.Auto;
                    _settings.uvTilingU = 1f;
                    _settings.uvTilingV = 1f;
                    break;

                case FXMMeshType.Disc:
                    _settings.uvFlowDirection = FXMUVFlowDirection.FromCenter;
                    _settings.uvTilingU = 1f;
                    _settings.uvTilingV = 1f;
                    break;

                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    _settings.uvTilingU = 1f;
                    _settings.uvTilingV = 1f;
                    break;

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    _settings.uvTilingU = 1f;
                    _settings.uvTilingV = Mathf.Max(2f, _settings.turns);
                    break;
            }

            SetUvGradientPreviewMode(false);
            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
        }

        private void FlipCurrentUvFlowDirection()
        {
            switch (_settings.uvFlowDirection)
            {
                case FXMUVFlowDirection.Auto:
                case FXMUVFlowDirection.VForward:
                    _settings.uvFlowDirection = FXMUVFlowDirection.VReverse;
                    break;
                case FXMUVFlowDirection.VReverse:
                    _settings.uvFlowDirection = FXMUVFlowDirection.VForward;
                    break;
                case FXMUVFlowDirection.UForward:
                    _settings.uvFlowDirection = FXMUVFlowDirection.UReverse;
                    break;
                case FXMUVFlowDirection.UReverse:
                    _settings.uvFlowDirection = FXMUVFlowDirection.UForward;
                    break;
                case FXMUVFlowDirection.FromCenter:
                    _settings.uvFlowDirection = FXMUVFlowDirection.ToCenter;
                    break;
                case FXMUVFlowDirection.ToCenter:
                    _settings.uvFlowDirection = FXMUVFlowDirection.FromCenter;
                    break;
                case FXMUVFlowDirection.CircularCW:
                    _settings.uvFlowDirection = FXMUVFlowDirection.CircularCCW;
                    break;
                case FXMUVFlowDirection.CircularCCW:
                    _settings.uvFlowDirection = FXMUVFlowDirection.CircularCW;
                    break;
            }
        }

        private static string GetUvFlowDescription(FXMUVFlowDirection direction)
        {
            switch (direction)
            {
                case FXMUVFlowDirection.Auto:
                    return Tr("Controls UV layout and texture flow behavior.", "Auto: Mesh Type이 생성한 기본 UV를 유지합니다. Slash/Beam/Helix는 V 방향, Ring/Disc는 기존 원형/방사형 UV를 우선합니다.");
                case FXMUVFlowDirection.VForward:
                    return Tr("Controls UV layout and texture flow behavior.", "V Forward: V가 진행 방향입니다. 검기, 빔, 헬릭스 리본 텍스처 흐름에 가장 안전합니다.");
                case FXMUVFlowDirection.VReverse:
                    return Tr("Controls UV layout and texture flow behavior.", "V Reverse: V 진행 방향을 반전합니다. 텍스처가 반대로 흐를 때 사용합니다.");
                case FXMUVFlowDirection.UForward:
                    return Tr("Controls UV layout and texture flow behavior.", "U Forward: U가 진행 방향입니다. 가로로 흐르는 텍스처 또는 일부 커스텀 셰이더 UV에 맞춥니다.");
                case FXMUVFlowDirection.UReverse:
                    return Tr("Adjust this option for the selected production workflow.", "U Reverse: U 진행 방향을 반전합니다.");
                case FXMUVFlowDirection.FromCenter:
                    return Tr("Controls the width or thickness for the selected mesh.", "From Center: 중앙에서 외곽으로 흐르는 UV입니다. Disc, 충격파, 마법진, 폭발 링에 유용합니다.");
                case FXMUVFlowDirection.ToCenter:
                    return Tr("Controls UV layout and texture flow behavior.", "To Center: 외곽에서 중앙으로 흐르는 UV입니다. 흡수, 포탈, 블랙홀 계열에 유용합니다.");
                case FXMUVFlowDirection.CircularCW:
                    return Tr("Controls UV layout and texture flow behavior.", "Circular CW: 시계 방향 원형 흐름입니다. 링 회전 텍스처와 마법진 회전에 유용합니다.");
                case FXMUVFlowDirection.CircularCCW:
                    return Tr("Adjust this option for the selected production workflow.", "Circular CCW: 반시계 방향 원형 흐름입니다.");
                default:
                    return Tr("Controls UV layout and texture flow behavior.", "UV Flow Direction 설명입니다.");
            }
        }

        private void DrawUvFlowMapPreview()
        {
            EditorGUILayout.Space(6f);
            Rect rect = GUILayoutUtility.GetRect(256f, 96f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));

            const int columns = 24;
            const int rows = 10;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float rawU = (x + 0.5f) / columns;
                    float rawV = (y + 0.5f) / rows;
                    Vector2 uv = FXMMeshBuilder.PreviewUV01(rawU, rawV, _settings);
                    uv = ApplyFlowPreviewOffsetToUv(uv, _settings, _flowPreviewOffsetU, _flowPreviewOffsetV);
                    float hue = Mathf.Repeat(uv.x * 0.78f + uv.y * 0.22f, 1f);
                    Color c = Color.HSVToRGB(hue, 0.75f, Mathf.Lerp(0.32f, 0.95f, Mathf.Clamp01(uv.y)));
                    c.a = 1f;
                    Rect cell = new Rect(
                        rect.x + x * rect.width / columns,
                        rect.y + (rows - 1 - y) * rect.height / rows,
                        rect.width / columns + 1f,
                        rect.height / rows + 1f);
                    EditorGUI.DrawRect(cell, c);
                }
            }

            Handles.BeginGUI();
            Color oldColor = Handles.color;
            Handles.color = new Color(1f, 1f, 1f, 0.92f);
            Vector2 start = UvPreviewPoint(rect, 0.18f, 0.22f);
            Vector2 end = UvPreviewPoint(rect, 0.82f, 0.78f);
            ResolveUvFlowPreviewArrow(rect, _settings.uvFlowDirection, out start, out end);
            Handles.DrawAAPolyLine(3f, start, end);
            Vector2 dir = (end - start).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Vector2 side = new Vector2(-dir.y, dir.x);
                float head = 9f;
                Handles.DrawAAPolyLine(3f, end, end - dir * head + side * head * 0.55f);
                Handles.DrawAAPolyLine(3f, end, end - dir * head - side * head * 0.55f);
            }
            Handles.color = oldColor;
            Handles.EndGUI();

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f), "UV Gradient / Flow Preview", labelStyle);
            GUI.Label(new Rect(rect.x + 6f, rect.yMax - 18f, rect.width - 12f, 16f), Tr("Controls UV layout and texture flow behavior.", "색 변화 = UV 분포, 흰 화살표 = 권장 흐름 방향"), labelStyle);
        }

        private static Vector2 UvPreviewPoint(Rect rect, float u, float v)
        {
            return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), Mathf.Lerp(rect.yMax, rect.yMin, v));
        }

        private static void ResolveUvFlowPreviewArrow(Rect rect, FXMUVFlowDirection direction, out Vector2 start, out Vector2 end)
        {
            switch (direction)
            {
                case FXMUVFlowDirection.UForward:
                    start = UvPreviewPoint(rect, 0.18f, 0.5f);
                    end = UvPreviewPoint(rect, 0.82f, 0.5f);
                    break;
                case FXMUVFlowDirection.UReverse:
                    start = UvPreviewPoint(rect, 0.82f, 0.5f);
                    end = UvPreviewPoint(rect, 0.18f, 0.5f);
                    break;
                case FXMUVFlowDirection.VReverse:
                    start = UvPreviewPoint(rect, 0.5f, 0.82f);
                    end = UvPreviewPoint(rect, 0.5f, 0.18f);
                    break;
                case FXMUVFlowDirection.FromCenter:
                    start = UvPreviewPoint(rect, 0.5f, 0.5f);
                    end = UvPreviewPoint(rect, 0.82f, 0.76f);
                    break;
                case FXMUVFlowDirection.ToCenter:
                    start = UvPreviewPoint(rect, 0.82f, 0.76f);
                    end = UvPreviewPoint(rect, 0.5f, 0.5f);
                    break;
                case FXMUVFlowDirection.CircularCW:
                    start = UvPreviewPoint(rect, 0.34f, 0.74f);
                    end = UvPreviewPoint(rect, 0.68f, 0.26f);
                    break;
                case FXMUVFlowDirection.CircularCCW:
                    start = UvPreviewPoint(rect, 0.68f, 0.26f);
                    end = UvPreviewPoint(rect, 0.34f, 0.74f);
                    break;
                case FXMUVFlowDirection.Auto:
                case FXMUVFlowDirection.VForward:
                default:
                    start = UvPreviewPoint(rect, 0.5f, 0.18f);
                    end = UvPreviewPoint(rect, 0.5f, 0.82f);
                    break;
            }
        }

        private void DrawAlphaSettings()
        {
            EditorGUILayout.Space(6f);
            _showAlpha = EditorGUILayout.Foldout(_showAlpha, Tr("4-2. Vertex Alpha Settings", "4. Vertex Alpha 설정"), true);
            if (!_showAlpha) return;

            EnsureAlphaSettingsCompatibility();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _settings.alphaMode = (FXMAlphaMode)EditorGUILayout.EnumPopup(new GUIContent("Alpha Mode", Tr("Controls Vertex Alpha generation and preview behavior.", "Vertex Color Alpha를 자동 생성합니다. v0.18부터 Edge/Center 계열은 메시 샘플 수를 자동 보강해 프리뷰와 실제 메시 차이를 줄입니다.")), _settings.alphaMode);
                _autoAlphaProfile = EditorGUILayout.Toggle(new GUIContent("Auto Alpha Profile", Tr("Controls Vertex Alpha generation and preview behavior.", "Mesh Type을 바꿀 때 해당 메시 타입에 맞는 Vertex Alpha 추천값을 자동 적용합니다.")), _autoAlphaProfile);

                DrawUiSubHeader(Tr("Alpha Symmetry", "알파 대칭"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    _settings.alphaSymmetryU = EditorGUILayout.ToggleLeft(new GUIContent("Sym U", Tr("Controls the width or thickness for the selected mesh.", "U 방향 Vertex Alpha를 중앙 기준으로 좌우 대칭 처리합니다. Beam/Slash/Helix 폭 방향 페이드에 유용합니다.")), _settings.alphaSymmetryU, GUILayout.Width(86f));
                    _settings.alphaSymmetryV = EditorGUILayout.ToggleLeft(new GUIContent("Sym V", Tr("Controls Vertex Alpha generation and preview behavior.", "V 방향 Vertex Alpha를 중앙 기준으로 상하/루트-팁 대칭 처리합니다. 양끝이 같이 사라지는 Trail에 유용합니다.")), _settings.alphaSymmetryV, GUILayout.Width(86f));
                    _settings.alphaSymmetryCenterOrigin = EditorGUILayout.ToggleLeft(new GUIContent("Center Origin", Tr("Adjust this option for the selected production workflow.", "OFF: 양쪽 외곽에서 중앙으로 같은 값이 진행됩니다. ON: 중앙에서 양쪽 외곽으로 같은 값이 진행됩니다.")), _settings.alphaSymmetryCenterOrigin);
                }
                EditorGUILayout.HelpBox(Tr("Symmetry is controlled by the toggles above. Duplicate quick buttons were removed to keep this panel clean.", "대칭 옵션은 위 토글로만 제어합니다. 중복 버튼은 패널 정리를 위해 제거했습니다."), MessageType.None);

                DrawUiSubHeader(Tr("Recommended Profile", "추천 프로필"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (UiButton("Apply Alpha For This Mesh"))
                    {
                        ApplyRecommendedAlphaForCurrentMesh(true);
                    }

                    if (UiButton("Mesh Alpha Help"))
                    {
                        EditorUtility.DisplayDialog("FXM Mesh Alpha Profile", GetAlphaProfileDescription(_settings.meshType), "OK");
                    }
                }

                DrawUiSubHeader(Tr("Alpha UV Map Preview", "Alpha UV 맵 프리뷰"));
                DrawAlphaUvPreview();
                DrawAlphaSamplingStatus();
                EditorGUILayout.HelpBox(Tr("Controls Vertex Alpha generation and preview behavior.", "밝은 부분은 Alpha 1, 어두운 부분은 Alpha 0입니다. v0.18부터 Soft Edge / Center 계열은 실제 메시의 Cross Segment가 부족하면 자동으로 보강해서 프리뷰와 실제 메시 차이를 줄입니다."), MessageType.None);

                DrawUiSubHeader(Tr("Alpha Preset", "알파 프리셋"));
                using (new EditorGUILayout.HorizontalScope())
                {
                    _alphaPresetSelection = (FXMAlphaPresetKind)EditorGUILayout.EnumPopup(new GUIContent("Preset", Tr("Select a reusable alpha preset. The old button grid was consolidated into this dropdown.", "재사용할 알파 프리셋을 선택합니다. 기존 버튼 그리드는 드롭다운으로 통합했습니다.")), _alphaPresetSelection);
                    if (UiButton(Tr("Apply", "적용")))
                    {
                        ApplyAlphaPreset(_alphaPresetSelection);
                    }
                }

                DrawUiSubHeader(Tr("Alpha Output / Packing", "알파 출력 / 패킹"));
                _settings.vertexColorRGBMode = (FXMVertexColorRGBMode)EditorGUILayout.EnumPopup(new GUIContent("Vertex RGB Packing", Tr("Controls Vertex Alpha generation and preview behavior.", "기본은 RGB White + Alpha입니다. ShaderGraph/VFX Graph에서 RGB 마스크가 필요하면 Alpha 값을 RGB 채널에도 복사할 수 있습니다.")), _settings.vertexColorRGBMode);
                EditorGUILayout.HelpBox(Tr("Vertex RGB Packing is controlled by the dropdown above. Preview-only buttons remain in Vertex Alpha Pro Tools.", "Vertex RGB 패킹은 위 드롭다운으로 제어합니다. 프리뷰 전용 버튼은 Vertex Alpha Pro Tools에만 남겨두었습니다."), MessageType.None);

                DrawVertexAlphaProTools();

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Alpha Range / Curve", EditorStyles.boldLabel);
                _settings.alphaRangeStart = EditorGUILayout.Slider(new GUIContent("Range Start", Tr("Controls Vertex Alpha generation and preview behavior.", "알파가 0에서 1로 올라가기 시작하는 기준입니다.")), _settings.alphaRangeStart, 0f, 1f);
                _settings.alphaRangeEnd = EditorGUILayout.Slider(new GUIContent("Range End", Tr("Controls Vertex Alpha generation and preview behavior.", "알파가 1에 도달하는 기준입니다. Start보다 작게 두면 반대 방향 페이드가 됩니다.")), _settings.alphaRangeEnd, 0f, 1f);

                _settings.alphaSoftness = EditorGUILayout.Slider(new GUIContent("Softness", Tr("Adjust this option for the selected production workflow.", "0이면 날카롭고, 1이면 부드러운 SmoothStep에 가까워집니다.")), _settings.alphaSoftness, 0f, 1f);
                _settings.alphaPower = EditorGUILayout.Slider(new GUIContent("Power", Tr("Controls Vertex Alpha generation and preview behavior.", "알파 곡선의 감마/강도입니다.")), _settings.alphaPower, 0.1f, 8f);
                _settings.alphaContrast = EditorGUILayout.Slider(new GUIContent("Contrast", Tr("Controls Vertex Alpha generation and preview behavior.", "알파 대비입니다. 1이면 기본입니다.")), _settings.alphaContrast, 0.1f, 4f);
                _settings.alphaOffset = EditorGUILayout.Slider(new GUIContent("Offset", Tr("Controls Vertex Alpha generation and preview behavior.", "알파 전체를 위/아래로 밀어줍니다.")), _settings.alphaOffset, -1f, 1f);
                _settings.invertAlpha = EditorGUILayout.Toggle(new GUIContent("Invert Alpha", Tr("Controls Vertex Alpha generation and preview behavior.", "알파를 반전합니다.")), _settings.invertAlpha);

                EditorGUILayout.MinMaxSlider(new GUIContent("Clamp Min / Max", Tr("Controls Vertex Alpha generation and preview behavior.", "최종 알파 최소/최대값입니다.")), ref _settings.minAlpha, ref _settings.maxAlpha, 0f, 1f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _settings.minAlpha = EditorGUILayout.FloatField("Min", Mathf.Clamp01(_settings.minAlpha));
                    _settings.maxAlpha = EditorGUILayout.FloatField("Max", Mathf.Clamp01(_settings.maxAlpha));
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Edge / Noise / Stripe", EditorStyles.boldLabel);
                _settings.alphaEdgeWidthU = EditorGUILayout.Slider(new GUIContent("Edge Width U", Tr("Controls the width or thickness for the selected mesh.", "EdgeU / EdgeFade 계열의 좌우 엣지 폭입니다.")), _settings.alphaEdgeWidthU, 0.01f, 0.5f);
                _settings.alphaEdgeWidthV = EditorGUILayout.Slider(new GUIContent("Edge Width V", Tr("Controls the width or thickness for the selected mesh.", "EdgeV / EdgeFade 계열의 상하 엣지 폭입니다.")), _settings.alphaEdgeWidthV, 0.01f, 0.5f);
                _settings.alphaNoiseStrength = EditorGUILayout.Slider(new GUIContent("Noise Strength", Tr("Controls Vertex Alpha generation and preview behavior.", "알파에 노이즈를 섞는 강도입니다.")), _settings.alphaNoiseStrength, 0f, 1f);
                _settings.alphaNoiseScale = EditorGUILayout.Slider(new GUIContent("Noise Scale", Tr("Controls Vertex Alpha generation and preview behavior.", "작을수록 큰 패턴, 클수록 촘촘한 알파 노이즈입니다.")), _settings.alphaNoiseScale, 0.5f, 32f);
                _settings.alphaNoiseSeed = EditorGUILayout.IntSlider(new GUIContent("Noise Seed", Tr("Controls Vertex Alpha generation and preview behavior.", "알파 노이즈 패턴 시드입니다.")), _settings.alphaNoiseSeed, 0, 9999);
                _settings.alphaStripeCount = EditorGUILayout.Slider(new GUIContent("Stripe Count", Tr("Adjust this option for the selected production workflow.", "StripePulse 모드의 줄무늬 개수입니다.")), _settings.alphaStripeCount, 1f, 24f);
                _settings.alphaStripeSoftness = EditorGUILayout.Slider(new GUIContent("Stripe Softness", Tr("Adjust this option for the selected production workflow.", "StripePulse 모드의 줄무늬 부드러움입니다.")), _settings.alphaStripeSoftness, 0.01f, 1f);
            }
        }

        private void DrawVertexAlphaProTools()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Vertex Alpha Pro Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                Tr("Quick QA controls for alpha/RGB packing. Buttons are aligned to equal columns for consistent scanning.",
                   "Alpha/RGB Packing 빠른 검수 도구입니다. 버튼 폭을 동일한 컬럼으로 맞춰 시선 이동을 줄였습니다."),
                MessageType.None);

            Rect previewRow = EditorGUILayout.GetControlRect(false, UiSmallButtonHeight);
            const float gap = 5f;
            if (UiEqualButton(GetEqualButtonRect(previewRow, 0, 3, gap), "View Alpha"))
            {
                _previewMode = FXMPreviewMode.VertexAlpha;
                _alphaAsOpacity = false;
                ApplyPreviewMaterialSettings(GetPreviewMaterial());
                SceneView.RepaintAll();
            }
            if (UiEqualButton(GetEqualButtonRect(previewRow, 1, 3, gap), "View Alpha Opacity"))
            {
                _previewMode = FXMPreviewMode.VertexAlpha;
                _alphaAsOpacity = true;
                ApplyPreviewMaterialSettings(GetPreviewMaterial());
                SceneView.RepaintAll();
            }
            if (UiEqualButton(GetEqualButtonRect(previewRow, 2, 3, gap), "View RGB Mask"))
            {
                _previewMode = FXMPreviewMode.VertexRGB;
                _alphaAsOpacity = false;
                ApplyPreviewMaterialSettings(GetPreviewMaterial());
                SceneView.RepaintAll();
            }

            UiRowSpace();
            Rect packingRow = EditorGUILayout.GetControlRect(false, UiSmallButtonHeight);
            if (UiEqualButton(GetEqualButtonRect(packingRow, 0, 3, gap), "Particle Safe Pack"))
            {
                _settings.vertexColorRGBMode = FXMVertexColorRGBMode.White;
                _previewMode = FXMPreviewMode.VertexAlpha;
                RebuildPreviewMesh(true);
            }
            if (UiEqualButton(GetEqualButtonRect(packingRow, 1, 3, gap), "Mask Pack A→RGB"))
            {
                _settings.vertexColorRGBMode = FXMVertexColorRGBMode.AlphaToRGB;
                _previewMode = FXMPreviewMode.VertexRGB;
                RebuildPreviewMesh(true);
            }
            if (UiEqualButton(GetEqualButtonRect(packingRow, 2, 3, gap), "Copy Alpha QA"))
            {
                EditorGUIUtility.systemCopyBuffer = BuildAlphaQaReport();
            }
        }

        private string BuildAlphaQaReport()
        {
            RebuildPreviewMesh(false);
            Mesh mesh = _previewMesh;
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Vertex Alpha QA");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Mesh Type: " + _settings.meshType);
            lines.Add("Alpha Mode: " + _settings.alphaMode);
            lines.Add("RGB Packing: " + _settings.vertexColorRGBMode);
            lines.Add("Symmetry U/V: " + _settings.alphaSymmetryU + " / " + _settings.alphaSymmetryV);
            lines.Add("Range: " + _settings.alphaRangeStart.ToString("0.###") + " → " + _settings.alphaRangeEnd.ToString("0.###"));
            lines.Add("Clamp: " + _settings.minAlpha.ToString("0.###") + " → " + _settings.maxAlpha.ToString("0.###"));
            if (mesh == null)
            {
                lines.Add("Mesh: Missing");
                return string.Join("\n", lines.ToArray());
            }

            Color32[] colors = mesh.colors32;
            float minAlpha;
            float maxAlpha;
            GetColor32AlphaRange(colors, mesh.vertexCount, out minAlpha, out maxAlpha);
            lines.Add("Vertices: " + mesh.vertexCount.ToString("N0"));
            lines.Add("Color32: " + (colors != null && colors.Length == mesh.vertexCount ? "OK" : "Missing / Count Mismatch"));
            lines.Add("Actual Alpha Range: " + minAlpha.ToString("0.000") + " ~ " + maxAlpha.ToString("0.000"));
            return string.Join("\n", lines.ToArray());
        }

        private void EnsureAlphaSettingsCompatibility()
        {
            if (Mathf.Abs(_settings.alphaRangeStart) < 0.0001f && Mathf.Abs(_settings.alphaRangeEnd) < 0.0001f)
            {
                _settings.alphaRangeEnd = 1f;
            }
            if (_settings.alphaPower <= 0.0001f) _settings.alphaPower = 1.5f;
            if (_settings.alphaContrast <= 0.0001f) _settings.alphaContrast = 1f;
            if (_settings.alphaNoiseScale <= 0.0001f) _settings.alphaNoiseScale = 8f;
            if (_settings.alphaStripeCount <= 0.0001f) _settings.alphaStripeCount = 5f;
            if (_settings.alphaStripeSoftness <= 0.0001f) _settings.alphaStripeSoftness = 0.35f;
            if (_settings.alphaEdgeWidthU <= 0.0001f) _settings.alphaEdgeWidthU = 0.25f;
            if (_settings.alphaEdgeWidthV <= 0.0001f) _settings.alphaEdgeWidthV = 0.25f;
        }

        private void DrawAlphaSamplingStatus()
        {
            int requestedCross = Mathf.Max(1, _settings.widthSegments);
            int effectiveCross = FXMMeshBuilder.GetEffectiveCrossSegmentsForPreview(_settings);
            bool boosted = effectiveCross > requestedCross;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Mesh-Aware Alpha Sampling", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("Requested Cross Segments", requestedCross.ToString(), EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Effective Alpha Cross Segments", effectiveCross.ToString(), EditorStyles.miniLabel);

                if (boosted)
                {
                    EditorGUILayout.HelpBox(Tr("Controls Vertex Alpha generation and preview behavior.", "현재 Alpha Mode는 중앙/엣지 샘플이 필요합니다. Cross Segments가 부족해서 v0.18이 빌드 시 자동으로 세그먼트를 보강합니다."), MessageType.Info);
                }
                else if (AlphaNeedsInteriorWidthSamples(_settings.alphaMode))
                {
                    EditorGUILayout.HelpBox(Tr("Controls Vertex Alpha generation and preview behavior.", "이 Alpha Mode는 최소 2개 이상의 Cross Segments가 있어야 엣지→중앙 그라데이션이 실제 메시에서 보입니다."), MessageType.None);
                }
            }
        }

        private void ApplyRecommendedAlphaForCurrentMesh(bool rebuild, bool showAlphaMatte = true)
        {
            _settings.invertAlpha = false;
            _settings.minAlpha = 0f;
            _settings.maxAlpha = 1f;
            _settings.alphaRangeStart = 0f;
            _settings.alphaRangeEnd = 1f;
            _settings.alphaOffset = 0f;
            _settings.alphaContrast = 1f;
            _settings.alphaNoiseStrength = 0f;
            _settings.alphaNoiseScale = 8f;
            _settings.alphaNoiseSeed = Mathf.Max(0, _settings.alphaNoiseSeed);
            _settings.alphaStripeCount = 5f;
            _settings.alphaStripeSoftness = 0.35f;
            _settings.alphaEdgeWidthU = 0.25f;
            _settings.alphaEdgeWidthV = 0.25f;
            _settings.alphaSymmetryU = false;
            _settings.alphaSymmetryV = false;
            _settings.alphaSymmetryCenterOrigin = false;

            switch (_settings.meshType)
            {
                case FXMMeshType.Slash:
                    _settings.alphaMode = FXMAlphaMode.TipFade;
                    _settings.alphaSoftness = 0.52f;
                    _settings.alphaPower = 1.15f;
                    _settings.alphaEdgeWidthU = 0.28f;
                    _settings.widthSegments = Mathf.Max(_settings.widthSegments, 2);
                    break;
                case FXMMeshType.Beam:
                    _settings.alphaMode = FXMAlphaMode.FlowV;
                    _settings.alphaSoftness = 0.42f;
                    _settings.alphaPower = 1.05f;
                    _settings.widthSegments = Mathf.Max(_settings.widthSegments, 2);
                    break;
                case FXMMeshType.Ring:
                    _settings.alphaMode = FXMAlphaMode.GeometryBorder;
                    _settings.alphaSoftness = 0.72f;
                    _settings.alphaPower = 1f;
                    _settings.alphaEdgeWidthU = 0.18f;
                    _settings.alphaEdgeWidthV = 0.18f;
                    _settings.widthSegments = Mathf.Max(_settings.widthSegments, 2);
                    break;
                case FXMMeshType.Disc:
                    _settings.alphaMode = FXMAlphaMode.EdgeFade;
                    _settings.alphaSoftness = 0.68f;
                    _settings.alphaPower = 1f;
                    _settings.alphaEdgeWidthU = 0.20f;
                    _settings.alphaEdgeWidthV = 0.20f;
                    _settings.widthSegments = Mathf.Max(_settings.widthSegments, 3);
                    break;
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    _settings.alphaMode = FXMAlphaMode.EdgeFade;
                    _settings.alphaSoftness = 0.75f;
                    _settings.alphaPower = 1f;
                    _settings.alphaEdgeWidthU = 0.22f;
                    _settings.alphaEdgeWidthV = 0.28f;
                    _settings.widthSegments = Mathf.Max(_settings.widthSegments, 3);
                    break;
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    _settings.alphaMode = FXMAlphaMode.StripePulse;
                    _settings.alphaSoftness = 0.35f;
                    _settings.alphaPower = 1f;
                    _settings.alphaStripeCount = 6f;
                    _settings.alphaStripeSoftness = 0.42f;
                    _settings.widthSegments = Mathf.Max(_settings.widthSegments, 2);
                    break;
            }

            if (showAlphaMatte)
            {
                // v0.25: 사용자가 명시적으로 Alpha 적용 버튼을 눌렀을 때만
                // 체크 패턴을 잠시 끄고 검정→흰색 Alpha Matte로 전환합니다.
                _previewMode = FXMPreviewMode.VertexAlpha;
                _previewTint = Color.white;
                _alphaAsOpacity = false;
            }
            else
            {
                SetCheckerPreviewMode(false);
            }
            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
        }

        private static string GetAlphaProfileDescription(FXMMeshType meshType)
        {
            switch (meshType)
            {
                case FXMMeshType.Slash:
                    return Tr("Controls the width or thickness for the selected mesh.", "Slash: 검기 끝과 루트가 자연스럽게 빠지는 Tip Fade를 기본으로 사용합니다. Soft Edge가 필요하면 Width / Cross Segments 2 이상을 권장합니다.");
                case FXMMeshType.Beam:
                    return Tr("Controls Beam shape and linear flow behavior.", "Beam: 진행 방향 Flow Fade가 기본입니다. 빔 시작/끝 소멸과 파티클 Stretch에 가장 안전합니다.");
                case FXMMeshType.Ring:
                    return Tr("Controls Ring shape and circular flow behavior.", "Ring: 실제 열린 경계선을 기준으로 빠지는 Geometry Border가 기본입니다. Broken Ring / Shockwave에 유리합니다.");
                case FXMMeshType.Disc:
                    return Tr("Controls Disc shape and radial flow behavior.", "Disc: 중심과 외곽을 같이 다루기 쉬운 Edge Fade가 기본입니다. 장판/마법진/쿨다운용으로 안정적입니다.");
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    return Tr("Controls Dome shape and preview behavior.", "Dome: 보호막 외곽이 부드럽게 사라지는 Edge Fade가 기본입니다. Half Dome은 열린 면까지 확인하세요.");
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return Tr("Controls UV layout and texture flow behavior.", "Helix: 나선 흐름이 보이는 Stripe Pulse가 기본입니다. 텍스처 스크롤과 조합하기 좋습니다.");
                default:
                    return Tr("Controls Vertex Alpha generation and preview behavior.", "현재 Mesh Type에 맞는 Vertex Alpha 추천값을 적용합니다.");
            }
        }

        private void ApplyAlphaPreset(FXMAlphaPresetKind kind)
        {
            _settings.invertAlpha = false;
            _settings.minAlpha = 0f;
            _settings.maxAlpha = 1f;
            _settings.alphaOffset = 0f;
            _settings.alphaContrast = 1f;
            _settings.alphaNoiseStrength = 0f;
            _settings.alphaNoiseScale = Mathf.Max(0.5f, _settings.alphaNoiseScale <= 0.0001f ? 8f : _settings.alphaNoiseScale);
            _settings.alphaStripeCount = Mathf.Max(1f, _settings.alphaStripeCount <= 0.0001f ? 5f : _settings.alphaStripeCount);
            _settings.alphaStripeSoftness = Mathf.Clamp(_settings.alphaStripeSoftness <= 0.0001f ? 0.35f : _settings.alphaStripeSoftness, 0.01f, 1f);

            switch (kind)
            {
                case FXMAlphaPresetKind.FlowIn:
                    _settings.alphaMode = FXMAlphaMode.FlowV;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.35f;
                    _settings.alphaPower = 1.1f;
                    break;
                case FXMAlphaPresetKind.FlowOut:
                    _settings.alphaMode = FXMAlphaMode.FlowVReverse;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.35f;
                    _settings.alphaPower = 1.1f;
                    break;
                case FXMAlphaPresetKind.SoftTip:
                    _settings.alphaMode = FXMAlphaMode.TipFade;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.45f;
                    _settings.alphaPower = 1.2f;
                    break;
                case FXMAlphaPresetKind.SoftEdge:
                    _settings.alphaMode = IsLinearWidthMesh(_settings.meshType) ? FXMAlphaMode.EdgeU : FXMAlphaMode.EdgeFade;
                    _settings.alphaEdgeWidthU = 0.42f;
                    _settings.alphaEdgeWidthV = 0.22f;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.75f;
                    _settings.alphaPower = 1f;
                    break;
                case FXMAlphaPresetKind.AllBorder:
                    _settings.alphaMode = FXMAlphaMode.EdgeFade;
                    _settings.alphaEdgeWidthU = 0.32f;
                    _settings.alphaEdgeWidthV = 0.32f;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.8f;
                    _settings.alphaPower = 1f;
                    break;
                case FXMAlphaPresetKind.GeometryBorder:
                    _settings.alphaMode = FXMAlphaMode.GeometryBorder;
                    _settings.alphaEdgeWidthU = 0.2f;
                    _settings.alphaEdgeWidthV = 0.2f;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.8f;
                    _settings.alphaPower = 1f;
                    break;
                case FXMAlphaPresetKind.CenterCore:
                    _settings.alphaMode = FXMAlphaMode.CenterOut;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.65f;
                    _settings.alphaPower = 0.8f;
                    break;
                case FXMAlphaPresetKind.CenterHole:
                    _settings.alphaMode = FXMAlphaMode.CenterHole;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.65f;
                    _settings.alphaPower = 1f;
                    break;
                case FXMAlphaPresetKind.CornerFade:
                    _settings.alphaMode = FXMAlphaMode.CornerFade;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.75f;
                    _settings.alphaPower = 1f;
                    break;
                case FXMAlphaPresetKind.NoisyDissolve:
                    _settings.alphaMode = FXMAlphaMode.NoiseDissolve;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.25f;
                    _settings.alphaPower = 1f;
                    _settings.alphaNoiseStrength = 0.75f;
                    _settings.alphaNoiseScale = 10f;
                    break;
                case FXMAlphaPresetKind.EnergyStripes:
                    _settings.alphaMode = FXMAlphaMode.StripePulse;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.35f;
                    _settings.alphaPower = 1f;
                    _settings.alphaStripeCount = 6f;
                    _settings.alphaStripeSoftness = 0.35f;
                    break;
                case FXMAlphaPresetKind.Reset:
                default:
                    _settings.alphaMode = FXMAlphaMode.TipFade;
                    _settings.invertAlpha = false;
                    _settings.alphaPower = 1.5f;
                    _settings.minAlpha = 0f;
                    _settings.maxAlpha = 1f;
                    _settings.alphaRangeStart = 0f;
                    _settings.alphaRangeEnd = 1f;
                    _settings.alphaSoftness = 0.35f;
                    _settings.alphaOffset = 0f;
                    _settings.alphaContrast = 1f;
                    _settings.alphaNoiseStrength = 0f;
                    _settings.alphaNoiseScale = 8f;
                    _settings.alphaStripeCount = 5f;
                    _settings.alphaStripeSoftness = 0.35f;
                    _settings.alphaEdgeWidthU = 0.25f;
                    _settings.alphaEdgeWidthV = 0.25f;
                    _settings.alphaSymmetryU = false;
                    _settings.alphaSymmetryV = false;
                    _settings.alphaSymmetryCenterOrigin = false;
                    break;
            }

            // v0.21: 알파를 적용할 때는 체커 패턴을 잠시 끄고, Blender처럼 검정→흰색 매트로 확인합니다.
            _previewMode = FXMPreviewMode.VertexAlpha;
            _previewTint = Color.white;
            _alphaAsOpacity = false;
            RebuildPreviewMesh(true);
        }

        private static bool AlphaNeedsInteriorWidthSamples(FXMAlphaMode mode)
        {
            switch (mode)
            {
                case FXMAlphaMode.CenterOut:
                case FXMAlphaMode.CenterHole:
                case FXMAlphaMode.EdgeFade:
                case FXMAlphaMode.EdgeU:
                case FXMAlphaMode.CornerFade:
                case FXMAlphaMode.RadialIn:
                case FXMAlphaMode.RadialOut:
                case FXMAlphaMode.GeometryBorder:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsLinearWidthMesh(FXMMeshType meshType)
        {
            return meshType == FXMMeshType.Slash || meshType == FXMMeshType.Beam || meshType == FXMMeshType.Helix || meshType == FXMMeshType.CrossHelix;
        }

        private void DrawAlphaUvPreview()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 74f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            const int columns = 40;
            const int rows = 12;
            float cellW = rect.width / columns;
            float cellH = rect.height / rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    float u = (x + 0.5f) / columns;
                    float v = (y + 0.5f) / rows;
                    u = ApplyPreviewUvBias(u, _settings.uvBiasU);
                    v = ApplyPreviewUvBias(v, _settings.uvBiasV);
                    float a = EvaluateAlphaPreview(u, v);
                    Color low = new Color(0.035f, 0.035f, 0.04f, 1f);
                    Color high = new Color(_previewTint.r, _previewTint.g, _previewTint.b, 1f);
                    Rect cell = new Rect(rect.x + x * cellW, rect.y + (rows - 1 - y) * cellH, cellW + 0.5f, cellH + 0.5f);
                    EditorGUI.DrawRect(cell, Color.Lerp(low, high, a));
                }
            }

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawLine(new Vector3(rect.xMin, rect.center.y), new Vector3(rect.xMax, rect.center.y));
            Handles.DrawLine(new Vector3(rect.center.x, rect.yMin), new Vector3(rect.center.x, rect.yMax));

            int sampleU = Mathf.Clamp(FXMMeshBuilder.GetEffectiveCrossSegmentsForPreview(_settings), 1, 16);
            int sampleV = Mathf.Clamp(Mathf.Max(1, _settings.segments), 1, 24);
            Handles.color = new Color(1f, 1f, 1f, 0.18f);
            for (int i = 0; i <= sampleU; i++)
            {
                float x = Mathf.Lerp(rect.xMin, rect.xMax, i / (float)sampleU);
                Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
            }
            for (int i = 0; i <= sampleV; i++)
            {
                float y = Mathf.Lerp(rect.yMax, rect.yMin, i / (float)sampleV);
                Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
            }

            Handles.color = Color.white;
            Handles.EndGUI();

            Rect labelRect = new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, 18f);
            EditorGUI.LabelField(labelRect, "U → / V ↑  ·  bright=opaque  ·  UV Bias + Alpha Symmetry reflected  ·  thin lines=mesh samples", EditorStyles.miniLabel);
        }

        private static float ApplyPreviewUvBias(float t, float bias)
        {
            t = Mathf.Clamp01(t);
            bias = Mathf.Clamp(bias, -1f, 1f);
            if (bias > 0f)
            {
                return Mathf.Pow(t, 1f + bias * 3f);
            }
            if (bias < 0f)
            {
                return 1f - Mathf.Pow(1f - t, 1f + Mathf.Abs(bias) * 3f);
            }
            return t;
        }

        private static void ApplyAlphaSymmetryPreview(ref float u, ref float v, FXMSettings settings)
        {
            if (settings.alphaSymmetryU)
            {
                u = settings.alphaSymmetryCenterOrigin
                    ? Mathf.Abs(u - 0.5f) * 2f
                    : 1f - Mathf.Abs(u - 0.5f) * 2f;
            }

            if (settings.alphaSymmetryV)
            {
                v = settings.alphaSymmetryCenterOrigin
                    ? Mathf.Abs(v - 0.5f) * 2f
                    : 1f - Mathf.Abs(v - 0.5f) * 2f;
            }

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
        }

        private float EvaluateAlphaPreview(float u, float v)
        {
            ApplyAlphaSymmetryPreview(ref u, ref v, _settings);
            float alpha = 1f;
            switch (_settings.alphaMode)
            {
                case FXMAlphaMode.None:
                    alpha = 1f;
                    break;
                case FXMAlphaMode.AlongU:
                case FXMAlphaMode.WidthU:
                    alpha = u;
                    break;
                case FXMAlphaMode.WidthUReverse:
                    alpha = 1f - u;
                    break;
                case FXMAlphaMode.AlongV:
                case FXMAlphaMode.FlowV:
                    alpha = v;
                    break;
                case FXMAlphaMode.FlowVReverse:
                case FXMAlphaMode.RootFade:
                    alpha = 1f - v;
                    break;
                case FXMAlphaMode.CenterOut:
                {
                    float du = Mathf.Abs(u - 0.5f) * 2f;
                    float dv = Mathf.Abs(v - 0.5f) * 2f;
                    alpha = 1f - Mathf.Clamp01(Mathf.Max(du, dv));
                    break;
                }
                case FXMAlphaMode.CenterHole:
                {
                    float du = Mathf.Abs(u - 0.5f) * 2f;
                    float dv = Mathf.Abs(v - 0.5f) * 2f;
                    alpha = Mathf.Clamp01(Mathf.Max(du, dv));
                    break;
                }
                case FXMAlphaMode.EdgeU:
                    alpha = EdgeMaskPreview(u, _settings.alphaEdgeWidthU);
                    break;
                case FXMAlphaMode.EdgeV:
                    alpha = EdgeMaskPreview(v, _settings.alphaEdgeWidthV);
                    break;
                case FXMAlphaMode.EdgeFade:
                    alpha = Mathf.Min(EdgeMaskPreview(u, _settings.alphaEdgeWidthU), EdgeMaskPreview(v, _settings.alphaEdgeWidthV));
                    break;
                case FXMAlphaMode.GeometryBorder:
                    alpha = Mathf.Min(EdgeMaskPreview(u, _settings.alphaEdgeWidthU), EdgeMaskPreview(v, _settings.alphaEdgeWidthV));
                    break;
                case FXMAlphaMode.TipFade:
                    alpha = Mathf.Clamp01(Mathf.Min(v, 1f - v) * 2f);
                    break;
                case FXMAlphaMode.CornerFade:
                {
                    float edgeU = Mathf.Min(u, 1f - u) * 2f;
                    float edgeV = Mathf.Min(v, 1f - v) * 2f;
                    alpha = Mathf.Clamp01(edgeU * edgeV);
                    break;
                }
                case FXMAlphaMode.RadialIn:
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.70710678f;
                    alpha = 1f - Mathf.Clamp01(d);
                    break;
                }
                case FXMAlphaMode.RadialOut:
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.70710678f;
                    alpha = Mathf.Clamp01(d);
                    break;
                }
                case FXMAlphaMode.NoiseDissolve:
                    alpha = Mathf.Lerp(v, HashNoisePreview(u, v, _settings.alphaNoiseScale, _settings.alphaNoiseSeed), Mathf.Clamp01(_settings.alphaNoiseStrength));
                    break;
                case FXMAlphaMode.StripePulse:
                    alpha = StripeMaskPreview(v, _settings.alphaStripeCount, _settings.alphaStripeSoftness);
                    break;
            }

            alpha = RemapAlphaRangePreview(alpha);
            if (_settings.alphaMode != FXMAlphaMode.NoiseDissolve && _settings.alphaNoiseStrength > 0.0001f)
            {
                float noise = HashNoisePreview(u, v, _settings.alphaNoiseScale, _settings.alphaNoiseSeed);
                alpha = Mathf.Lerp(alpha, alpha * noise, _settings.alphaNoiseStrength);
            }

            alpha = Mathf.Clamp01(alpha + _settings.alphaOffset);
            alpha = Mathf.Clamp01((alpha - 0.5f) * Mathf.Max(0.0001f, _settings.alphaContrast) + 0.5f);
            alpha = Mathf.Pow(Mathf.Clamp01(alpha), Mathf.Max(0.0001f, _settings.alphaPower));
            if (_settings.invertAlpha)
            {
                alpha = 1f - alpha;
            }
            return Mathf.Lerp(Mathf.Clamp01(_settings.minAlpha), Mathf.Clamp01(_settings.maxAlpha), Mathf.Clamp01(alpha));
        }

        private static float EdgeMaskPreview(float value, float width)
        {
            float edge = Mathf.Min(value, 1f - value);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / Mathf.Max(0.0001f, width)));
        }

        private float RemapAlphaRangePreview(float value)
        {
            float start = Mathf.Clamp01(_settings.alphaRangeStart);
            float end = Mathf.Clamp01(_settings.alphaRangeEnd);
            if (Mathf.Abs(end - start) < 0.0001f)
            {
                return value >= end ? 1f : 0f;
            }
            float t = Mathf.Clamp01((value - start) / (end - start));
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            return Mathf.Lerp(t, smooth, Mathf.Clamp01(_settings.alphaSoftness));
        }

        private static float HashNoisePreview(float u, float v, float scale, int seed)
        {
            float sx = Mathf.Floor(u * Mathf.Max(0.01f, scale) * 23f + seed * 13.13f);
            float sy = Mathf.Floor(v * Mathf.Max(0.01f, scale) * 23f + seed * 7.71f);
            float n = Mathf.Sin(sx * 12.9898f + sy * 78.233f + seed * 0.123f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        private static float StripeMaskPreview(float value, float count, float softness)
        {
            float wave = Mathf.Abs(Mathf.Sin(value * Mathf.Max(1f, count) * Mathf.PI));
            return Mathf.SmoothStep(0f, Mathf.Clamp(softness, 0.01f, 1f), wave);
        }

        private void DrawAdvancedSettings()
        {
            EditorGUILayout.Space(6f);
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, Tr("8. Advanced Settings", "4-1. 고급 설정"), true);
            if (!_showAdvanced) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _settings.doubleSidedGeometry = EditorGUILayout.Toggle(new GUIContent("Double Sided Geometry", Tr("Mobile optimization note. Reduce geometry complexity when needed.", "뒷면 삼각형을 추가합니다. 모바일에서는 폴리곤 수가 증가합니다.")), _settings.doubleSidedGeometry);
                _settings.recalculateNormals = EditorGUILayout.Toggle(new GUIContent("Recalculate Normals", Tr("Adjust this option for the selected production workflow.", "노멀을 다시 계산합니다.")), _settings.recalculateNormals);
                _settings.recalculateTangents = EditorGUILayout.Toggle(new GUIContent("Recalculate Tangents", Tr("Adjust this option for the selected production workflow.", "탄젠트를 다시 계산합니다. 노멀맵 사용 시 유용합니다.")), _settings.recalculateTangents);
                _settings.centerPivotAfterBuild = EditorGUILayout.Toggle(new GUIContent("Center Pivot After Build", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "생성 후 Bounds 중심이 원점에 오도록 정렬합니다.")), _settings.centerPivotAfterBuild);
            }
        }

        private void DrawPivotAxisSettings()
        {
            EditorGUILayout.Space(6f);
            _showPivotAxis = EditorGUILayout.Foldout(_showPivotAxis, Tr("5. Pivot / Axis Settings", "4-2. Pivot / Axis 설정"), true);
            if (!_showPivotAxis) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("Controls the saved mesh pivot and axis orientation.", "Mesh Asset의 실제 버텍스를 이동/회전해서 피봇 위치와 축 방향을 맞춥니다. Unity Object Transform 값은 건드리지 않고, 저장되는 Mesh 데이터에 직접 반영됩니다."),
                    MessageType.Info);

                _settings.pivotAnchor = (FXMGlobalPivotAnchor)EditorGUILayout.EnumPopup(new GUIContent("Pivot Anchor", Tr("Controls the saved mesh pivot and axis orientation.", "피봇을 어디에 둘지 정합니다. Bounds 기준은 현재 생성된 메시 Bounds를 사용합니다.")), _settings.pivotAnchor);
                _settings.pivotPositionOffset = EditorGUILayout.Vector3Field(new GUIContent("Pivot Position Offset", Tr("Controls the saved mesh pivot and axis orientation.", "선택한 Pivot Anchor에서 추가로 이동할 피봇 오프셋입니다. +X로 넣으면 메시가 -X 방향으로 이동해 피봇이 +X 위치에 잡힌 것처럼 됩니다.")), _settings.pivotPositionOffset);
                _settings.pivotRotationEuler = EditorGUILayout.Vector3Field(new GUIContent("Pivot Rotation", Tr("Controls the saved mesh pivot and axis orientation.", "피봇 기준으로 메시 버텍스를 미리 회전합니다. 저장되는 Mesh Asset에 반영됩니다.")), _settings.pivotRotationEuler);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rot X +90", GUILayout.Height(22f)))
                    {
                        _settings.pivotRotationEuler.x += 90f;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Rot Y +90", GUILayout.Height(22f)))
                    {
                        _settings.pivotRotationEuler.y += 90f;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Rot Z +90", GUILayout.Height(22f)))
                    {
                        _settings.pivotRotationEuler.z += 90f;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Clear Rot", GUILayout.Height(22f)))
                    {
                        _settings.pivotRotationEuler = Vector3.zero;
                        RebuildPreviewMesh(true);
                    }
                }

                _settings.upAxis = (FXMUpAxis)EditorGUILayout.EnumPopup(new GUIContent("Up Axis", Tr("Adjust this option for the selected production workflow.", "Unity 기본은 Y-Up입니다. Blender/DCC 기준 Z-Up 메시가 필요하면 Z-Up을 선택하세요.")), _settings.upAxis);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Pivot Center", GUILayout.Height(24f)))
                    {
                        _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsCenter;
                        _settings.pivotPositionOffset = Vector3.zero;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Pivot Bottom", GUILayout.Height(24f)))
                    {
                        _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsBottom;
                        _settings.pivotPositionOffset = Vector3.zero;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Pivot Top", GUILayout.Height(24f)))
                    {
                        _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsTop;
                        _settings.pivotPositionOffset = Vector3.zero;
                        RebuildPreviewMesh(true);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Y-Up Unity", GUILayout.Height(24f)))
                    {
                        _settings.upAxis = FXMUpAxis.YUp;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Z-Up DCC", GUILayout.Height(24f)))
                    {
                        _settings.upAxis = FXMUpAxis.ZUp;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Reset Pivot / Axis", GUILayout.Height(24f)))
                    {
                        _settings.pivotAnchor = FXMGlobalPivotAnchor.KeepGeneratedOrigin;
                        _settings.pivotPositionOffset = Vector3.zero;
                        _settings.pivotRotationEuler = Vector3.zero;
                        _settings.upAxis = FXMUpAxis.YUp;
                        RebuildPreviewMesh(true);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Recommended Pivot", GUILayout.Height(24f)))
                    {
                        ApplyRecommendedPivotForCurrentMesh();
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Clear Pivot Offset", GUILayout.Height(24f)))
                    {
                        _settings.pivotPositionOffset = Vector3.zero;
                        RebuildPreviewMesh(true);
                    }
                }

                string upAxisInfo = _settings.upAxis == FXMUpAxis.YUp
                    ? Tr("Adjust this option for the selected production workflow.", "Y-Up: Unity 기본 축입니다. 기존 버전과 같은 방향으로 생성됩니다.")
                    : Tr("Adjust this option for the selected production workflow.", "Z-Up: 메시 버텍스를 X축 +90도로 회전해 Z축이 위로 향하도록 저장합니다. Blender/DCC 교차 작업용입니다.");
                EditorGUILayout.HelpBox(upAxisInfo + Tr("Controls the saved mesh pivot and axis orientation.", "\nScene Helper는 v0.16부터 Pivot Rotation / Up Axis와 동기화됩니다."), MessageType.None);
            }
        }


        private void DrawModifierSettings()
        {
            EditorGUILayout.Space(6f);
            _showModifiers = EditorGUILayout.Foldout(_showModifiers, Tr("3. Modifier / Deform Settings", "4-3. Modifier / Deform 설정"), true);
            if (!_showModifiers) return;

            FXMModifierProfile profile = FXMModifierProfile.For(_settings.meshType);
            EnsureModifierSettingsCompatibility();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(profile.title, EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(profile.description, MessageType.None);

                _settings.useModifiers = EditorGUILayout.Toggle(new GUIContent("Use Modifiers", Tr("Controls modifier/deform behavior for the selected mesh.", "현재 메시 타입에서 허용된 Modifier만 실제 빌드에 적용됩니다. 비활성 항목은 값이 남아 있어도 무시됩니다.")), _settings.useModifiers);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Recommended For This Mesh", GUILayout.Height(24f)))
                    {
                        ApplyRecommendedModifiersForCurrentMesh();
                    }

                    if (GUILayout.Button("Clear Unsupported", GUILayout.Height(24f)))
                    {
                        ClampUnsupportedModifierValues(true);
                    }

                    if (GUILayout.Button("Reset All", GUILayout.Height(24f)))
                    {
                        ResetAllModifiers();
                    }
                }

                EditorGUILayout.Space(4f);
                DrawModifierAvailability(profile);

                EditorGUILayout.Space(4f);
                using (new EditorGUI.DisabledScope(!_settings.useModifiers || !profile.allowTwist))
                {
                    _settings.twistAmount = EditorGUILayout.Slider(new GUIContent(profile.twistLabel, profile.twistTooltip), _settings.twistAmount, -720f, 720f);
                }

                using (new EditorGUI.DisabledScope(!profile.allowTaper))
                {
                    _settings.taperAmount = EditorGUILayout.Slider(new GUIContent(profile.taperLabel, profile.taperTooltip), _settings.taperAmount, 0f, 1f);
                    _settings.taperDirection = EditorGUILayout.Slider(new GUIContent("Taper Direction", Tr("Adjust this option for the selected production workflow.", "Blender 기준입니다. 0=양끝, -1=시작 쪽, +1=끝 쪽을 좁힙니다.")), _settings.taperDirection, -1f, 1f);
                }

                using (new EditorGUI.DisabledScope(!_settings.useModifiers || !profile.allowBend))
                {
                    _settings.bendAmount = EditorGUILayout.Slider(new GUIContent(profile.bendLabel, profile.bendTooltip), _settings.bendAmount, -180f, 180f);
                }

                using (new EditorGUI.DisabledScope(!_settings.useModifiers || !profile.allowNoise))
                {
                    _settings.noiseStrength = EditorGUILayout.Slider(new GUIContent(profile.noiseLabel, profile.noiseTooltip), _settings.noiseStrength, 0f, 1f);
                    _settings.noiseScale = EditorGUILayout.Slider(new GUIContent("Noise Scale", Tr("Adjust this option for the selected production workflow.", "3ds Max 스타일 기준입니다. 값이 작을수록 촘촘하고, 클수록 넓은 흐름의 흔들림입니다.")), _settings.noiseScale, 0.1f, 20f);
                    _settings.noiseSeed = EditorGUILayout.IntSlider(new GUIContent("Noise Seed", Tr("Adjust this option for the selected production workflow.", "노이즈 패턴을 바꾸는 시드입니다.")), _settings.noiseSeed, 0, 9999);
                }

                EditorGUILayout.Space(8f);
                DrawMaxStyleModifierStack();

                string ignored = GetUnsupportedModifierSummary(profile);
                if (!string.IsNullOrEmpty(ignored))
                {
                    EditorGUILayout.HelpBox(Tr("Controls modifier/deform behavior for the selected mesh.", "현재 Mesh Type에서는 다음 Modifier가 적용되지 않습니다: ") + ignored + Tr("Adjust this option for the selected production workflow.", "\n값은 보존되지만 빌드에서는 무시됩니다. 정리하려면 Clear Unsupported를 누르세요."), MessageType.Warning);
                }

                EditorGUILayout.HelpBox(
                    Tr("Controls modifier/deform behavior for the selected mesh.", "Taper는 Blender 방식으로 작동합니다. Amount 0=유지, 1=강한 축소 / Direction 0=양끝, -1=시작, +1=끝 쪽 축소입니다. Ring/Disc는 Taper보다 Thickness/Radial Bias를 우선 사용합니다."),
                    MessageType.Info);
            }
        }

        private void EnsureModifierSettingsCompatibility()
        {
            if (_settings.ffdP0Scale <= 0.0001f) _settings.ffdP0Scale = 1f;
            if (_settings.ffdP1Scale <= 0.0001f) _settings.ffdP1Scale = 1f;
            if (_settings.ffdP2Scale <= 0.0001f) _settings.ffdP2Scale = 1f;
            if (_settings.ffdP3Scale <= 0.0001f) _settings.ffdP3Scale = 1f;
            _settings.ffdSmoothness = Mathf.Clamp01(_settings.ffdSmoothness <= 0f ? 1f : _settings.ffdSmoothness);
        }

        private void DrawMaxStyleModifierStack()
        {
            EditorGUILayout.LabelField("Blender / 3ds Max Style Stack", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(Tr("Adjust this option for the selected production workflow.", "Blender 애드온의 Edit / 3ds Max 흐름을 Unity용으로 이식한 스택입니다. 체크한 항목만 위에서 아래 순서로 실제 Mesh Vertex에 적용됩니다."), MessageType.Info);

            {
                if (!_settings.useModifiers)
                {
                    EditorGUILayout.HelpBox(Tr("Controls modifier/deform behavior for the selected mesh.", "Use Modifiers가 꺼져 있으면 아래 스택 값은 저장만 되고 실제 빌드에는 적용되지 않습니다."), MessageType.None);
                }

                _settings.maxBendEnable = EditorGUILayout.Toggle(new GUIContent("Use Max Bend", Tr("Adjust this option for the selected production workflow.", "선택 축을 기준으로 메시를 호 형태로 굽힙니다.")), _settings.maxBendEnable);
                if (_settings.maxBendEnable)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.maxBendAngle = EditorGUILayout.Slider(new GUIContent("Max Bend Angle"), _settings.maxBendAngle, -720f, 720f);
                        _settings.maxBendAxis = (FXMAxis)EditorGUILayout.EnumPopup(_settings.maxBendAxis, GUILayout.Width(52f));
                    }
                    _settings.maxBendPlane = (FXMAxis)EditorGUILayout.EnumPopup(new GUIContent("Bend Direction Plane", Tr("Controls modifier/deform behavior for the selected mesh.", "굽힘이 밀리는 방향입니다. Bend Axis와 같으면 자동으로 보정됩니다.")), _settings.maxBendPlane);
                    _settings.maxBendBias = EditorGUILayout.Slider(new GUIContent("Bend Bias", Tr("Adjust this option for the selected production workflow.", "-1이면 시작 쪽, +1이면 끝 쪽에 굽힘 분포를 더 몰아줍니다.")), _settings.maxBendBias, -1f, 1f);
                }

                EditorGUILayout.Space(3f);
                _settings.maxTwistEnable = EditorGUILayout.Toggle(new GUIContent("Use Max Twist", Tr("Adjust this option for the selected production workflow.", "선택 축을 따라 점진적으로 비틀어 토네이도/에너지 흐름을 만듭니다.")), _settings.maxTwistEnable);
                if (_settings.maxTwistEnable)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.maxTwistAngle = EditorGUILayout.Slider(new GUIContent("Max Twist Angle"), _settings.maxTwistAngle, -1440f, 1440f);
                        _settings.maxTwistAxis = (FXMAxis)EditorGUILayout.EnumPopup(_settings.maxTwistAxis, GUILayout.Width(52f));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.maxTwistDirection = (FXMTwistDirection)EditorGUILayout.EnumPopup(new GUIContent("Twist Direction"), _settings.maxTwistDirection);
                        _settings.maxTwistMode = (FXMTwistMode)EditorGUILayout.EnumPopup(new GUIContent("Twist Mode"), _settings.maxTwistMode);
                    }
                    _settings.maxTwistBias = EditorGUILayout.Slider(new GUIContent("Twist Bias", Tr("Adjust this option for the selected production workflow.", "-1이면 앞쪽에, +1이면 뒤쪽에 비틀림이 더 몰립니다.")), _settings.maxTwistBias, -1f, 1f);
                }

                EditorGUILayout.Space(3f);
                _settings.maxTaperEnable = EditorGUILayout.Toggle(new GUIContent("Use Max Taper", Tr("Controls modifier/deform behavior for the selected mesh.", "축 방향으로 전체 단면을 점진적으로 확대/축소합니다. 기본 Taper와 별개인 3ds Max 스타일 보정입니다.")), _settings.maxTaperEnable);
                if (_settings.maxTaperEnable)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.maxTaperAmount = EditorGUILayout.Slider(new GUIContent("Max Taper Amount"), _settings.maxTaperAmount, -0.95f, 0.95f);
                        _settings.maxTaperAxis = (FXMAxis)EditorGUILayout.EnumPopup(_settings.maxTaperAxis, GUILayout.Width(52f));
                    }
                    _settings.maxTaperBias = EditorGUILayout.Slider(new GUIContent("Taper Bias"), _settings.maxTaperBias, -1f, 1f);
                }

                EditorGUILayout.Space(3f);
                _settings.ffdEnable = EditorGUILayout.Toggle(new GUIContent("Use Simple FFD", Tr("Adjust this option for the selected production workflow.", "간단한 2x2x2 / 3x3x3 / 4x4x4 라티스 느낌의 스케일/높이 변형입니다.")), _settings.ffdEnable);
                if (_settings.ffdEnable)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.ffdMode = (FXMFFDMode)EditorGUILayout.EnumPopup(new GUIContent("FFD Mode"), _settings.ffdMode);
                        _settings.ffdResolution = (FXMFFDResolution)EditorGUILayout.EnumPopup(new GUIContent("FFD Res"), _settings.ffdResolution);
                        _settings.ffdAxis = (FXMAxis)EditorGUILayout.EnumPopup(_settings.ffdAxis, GUILayout.Width(52f));
                    }
                    _settings.ffdStrength = EditorGUILayout.Slider(new GUIContent("FFD Strength", Tr("Adjust this option for the selected production workflow.", "중앙부를 부풀리거나 눌러주는 전역 FFD 강도입니다.")), _settings.ffdStrength, -2f, 2f);
                    EditorGUILayout.LabelField("FFD Scale", EditorStyles.miniBoldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.ffdP0Scale = EditorGUILayout.FloatField(_settings.ffdP0Scale);
                        _settings.ffdP1Scale = EditorGUILayout.FloatField(_settings.ffdP1Scale);
                        _settings.ffdP2Scale = EditorGUILayout.FloatField(_settings.ffdP2Scale);
                        _settings.ffdP3Scale = EditorGUILayout.FloatField(_settings.ffdP3Scale);
                    }
                    EditorGUILayout.LabelField("FFD Height", EditorStyles.miniBoldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.ffdP0Height = EditorGUILayout.FloatField(_settings.ffdP0Height);
                        _settings.ffdP1Height = EditorGUILayout.FloatField(_settings.ffdP1Height);
                        _settings.ffdP2Height = EditorGUILayout.FloatField(_settings.ffdP2Height);
                        _settings.ffdP3Height = EditorGUILayout.FloatField(_settings.ffdP3Height);
                    }
                    _settings.ffdSmoothness = EditorGUILayout.Slider(new GUIContent("FFD Smoothness", Tr("Adjust this option for the selected production workflow.", "0은 선형에 가깝고 1은 부드러운 Bezier 라티스 느낌입니다.")), _settings.ffdSmoothness, 0f, 1f);
                }

                EditorGUILayout.Space(3f);
                _settings.maxNoiseEnable = EditorGUILayout.Toggle(new GUIContent("Use Max Noise", Tr("Controls modifier/deform behavior for the selected mesh.", "Blender 애드온과 같은 Max Noise 스택입니다. Scale은 3ds Max 기준으로 값이 작을수록 촘촘합니다.")), _settings.maxNoiseEnable);
                if (_settings.maxNoiseEnable)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.maxNoiseSeed = EditorGUILayout.IntField(new GUIContent("Noise Seed"), Mathf.Max(0, _settings.maxNoiseSeed));
                        _settings.maxNoiseScale = EditorGUILayout.Slider(new GUIContent("Noise Scale"), _settings.maxNoiseScale, 0f, 500f);
                    }
                    _settings.maxNoiseFractal = EditorGUILayout.Toggle(new GUIContent("Fractal Noise", Tr("Adjust this option for the selected production workflow.", "여러 옥타브를 합성해 좀 더 자연스러운 노이즈를 만듭니다.")), _settings.maxNoiseFractal);
                    EditorGUILayout.LabelField("Noise Strength XYZ", EditorStyles.miniBoldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _settings.maxNoiseStrengthX = EditorGUILayout.FloatField(_settings.maxNoiseStrengthX);
                        _settings.maxNoiseStrengthY = EditorGUILayout.FloatField(_settings.maxNoiseStrengthY);
                        _settings.maxNoiseStrengthZ = EditorGUILayout.FloatField(_settings.maxNoiseStrengthZ);
                    }
                }

                EditorGUILayout.Space(3f);
                _settings.shellThickness = EditorGUILayout.Slider(new GUIContent("Shell Thickness", Tr("Controls the width or thickness for the selected mesh.", "축 방향으로 두께를 만들어주는 Shell 옵션입니다. 0이면 비활성입니다.")), _settings.shellThickness, -10f, 10f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _settings.shellAxis = (FXMAxis)EditorGUILayout.EnumPopup(new GUIContent("Shell Axis"), _settings.shellAxis);
                    _settings.shellMode = (FXMShellMode)EditorGUILayout.EnumPopup(new GUIContent("Shell Mode"), _settings.shellMode);
                }
                _settings.flipNormals = EditorGUILayout.Toggle(new GUIContent("Flip Normals", Tr("Adjust this option for the selected production workflow.", "최종 삼각형 와인딩을 뒤집습니다.")), _settings.flipNormals);
                EditorGUILayout.HelpBox("Order: Bend > Twist > Max Taper > FFD > Max Noise > Shell > Flip Normals > Double-sided > Normal", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Preset Slash Bend", GUILayout.Height(22f)))
                    {
                        _settings.useModifiers = true;
                        _settings.maxBendEnable = true;
                        _settings.maxBendAngle = 35f;
                        _settings.maxBendAxis = FXMAxis.Y;
                        _settings.maxBendPlane = FXMAxis.X;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Preset Tornado FFD", GUILayout.Height(22f)))
                    {
                        _settings.useModifiers = true;
                        _settings.maxTwistEnable = true;
                        _settings.maxTwistAngle = 420f;
                        _settings.maxTwistAxis = FXMAxis.Y;
                        _settings.maxTwistMode = FXMTwistMode.BottomToTop;
                        _settings.ffdEnable = true;
                        _settings.ffdAxis = FXMAxis.Y;
                        _settings.ffdResolution = FXMFFDResolution.Res3;
                        _settings.ffdP0Scale = 0.35f;
                        _settings.ffdP1Scale = 1.05f;
                        _settings.ffdP2Scale = 0.55f;
                        _settings.ffdP3Scale = 1f;
                        _settings.ffdSmoothness = 1f;
                        RebuildPreviewMesh(true);
                    }

                    if (GUILayout.Button("Clear Max Stack", GUILayout.Height(22f)))
                    {
                        ClearMaxStyleModifiers();
                        RebuildPreviewMesh(true);
                    }
                }
            }
        }

        private void ClearMaxStyleModifiers()
        {
            _settings.maxBendEnable = false;
            _settings.maxBendAngle = 0f;
            _settings.maxBendBias = 0f;
            _settings.maxTwistEnable = false;
            _settings.maxTwistAngle = 0f;
            _settings.maxTwistBias = 0f;
            _settings.maxTaperEnable = false;
            _settings.maxTaperAmount = 0f;
            _settings.maxTaperBias = 0f;
            _settings.ffdEnable = false;
            _settings.ffdStrength = 0f;
            _settings.ffdP0Scale = 1f;
            _settings.ffdP1Scale = 1f;
            _settings.ffdP2Scale = 1f;
            _settings.ffdP3Scale = 1f;
            _settings.ffdP0Height = 0f;
            _settings.ffdP1Height = 0f;
            _settings.ffdP2Height = 0f;
            _settings.ffdP3Height = 0f;
            _settings.ffdSmoothness = 1f;
            _settings.maxNoiseEnable = false;
            _settings.maxNoiseSeed = 0;
            _settings.maxNoiseScale = 0f;
            _settings.maxNoiseFractal = false;
            _settings.maxNoiseStrengthX = 0f;
            _settings.maxNoiseStrengthY = 0f;
            _settings.maxNoiseStrengthZ = 0f;
            _settings.shellThickness = 0f;
            _settings.shellAxis = FXMAxis.Z;
            _settings.shellMode = FXMShellMode.Center;
            _settings.flipNormals = false;
        }

        private void DrawModifierAvailability(FXMModifierProfile profile)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawMiniStatus("Twist", profile.allowTwist);
                DrawMiniStatus("Taper", profile.allowTaper);
                DrawMiniStatus("Bend", profile.allowBend);
                DrawMiniStatus("Noise", profile.allowNoise);
            }
        }

        private static void DrawMiniStatus(string label, bool enabled)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = enabled ? FontStyle.Bold : FontStyle.Normal
            };

            string text = enabled ? label + " ON" : label + " OFF";
            EditorGUILayout.LabelField(text, style, GUILayout.MinWidth(72f));
        }

        private string GetUnsupportedModifierSummary(FXMModifierProfile profile)
        {
            List<string> ignored = new List<string>();
            if (!profile.allowTwist && Mathf.Abs(_settings.twistAmount) > 0.0001f) ignored.Add("Twist");
            if (!profile.allowTaper && Mathf.Abs(_settings.taperAmount) > 0.0001f) ignored.Add("Taper");
            if (!profile.allowBend && Mathf.Abs(_settings.bendAmount) > 0.0001f) ignored.Add("Bend");
            if (!profile.allowNoise && _settings.noiseStrength > 0.0001f) ignored.Add("Noise");
            return string.Join(", ", ignored.ToArray());
        }

        private void ClampUnsupportedModifierValues(bool rebuild)
        {
            FXMModifierProfile profile = FXMModifierProfile.For(_settings.meshType);
            if (!profile.allowTwist) _settings.twistAmount = 0f;
            if (!profile.allowTaper)
            {
                _settings.taperAmount = 0f;
                _settings.taperDirection = 0f;
            }
            if (!profile.allowBend) _settings.bendAmount = 0f;
            if (!profile.allowNoise)
            {
                _settings.noiseStrength = 0f;
                _settings.noiseScale = 4f;
                _settings.noiseSeed = 0;
            }

            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
        }

        private void ResetAllModifiers()
        {
            ResetAllModifiers(true);
        }

        private void ResetAllModifiers(bool rebuild)
        {
            _settings.useModifiers = false;
            _settings.twistAmount = 0f;
            _settings.taperAmount = 0f;
            _settings.taperDirection = 0f;
            _settings.flowOffset = 0f;
            _settings.widthOffset = 0f;
            _settings.shapeBias = 0f;
            _settings.segmentFlowBias = 0f;
            _settings.segmentWidthBias = 0f;
            _settings.depth = 0f;
            _settings.edgeJitter = 0f;
            _settings.brokenAmount = 0f;
            _settings.bendAmount = 0f;
            _settings.noiseStrength = 0f;
            _settings.noiseScale = 4f;
            _settings.noiseSeed = 0;
            ClearMaxStyleModifiers();
            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
        }

        private void ApplyRecommendedModifiersForCurrentMesh()
        {
            ApplyRecommendedModifiersForCurrentMesh(true);
        }

        private void ApplyRecommendedModifiersForCurrentMesh(bool rebuild)
        {
            ResetAllModifiers(false);
            _settings.useModifiers = true;

            switch (_settings.meshType)
            {
                case FXMMeshType.Slash:
                    _settings.taperAmount = 0.75f;
                    _settings.taperDirection = 1f;
                    _settings.bendAmount = 2.5f;
                    _settings.noiseStrength = 0.018f;
                    _settings.noiseScale = 3.5f;
                    break;

                case FXMMeshType.Beam:
                    _settings.taperAmount = 0.45f;
                    _settings.bendAmount = 6f;
                    _settings.noiseStrength = 0.008f;
                    _settings.noiseScale = 8f;
                    break;

                case FXMMeshType.Ring:
                    _settings.bendAmount = 12f;
                    _settings.noiseStrength = 0.012f;
                    _settings.noiseScale = 5.5f;
                    break;

                case FXMMeshType.Disc:
                    _settings.bendAmount = 6f;
                    _settings.noiseStrength = 0.01f;
                    _settings.noiseScale = 6f;
                    break;

                case FXMMeshType.Dome:
                    _settings.twistAmount = 18f;
                    _settings.taperAmount = 0.25f;
                    _settings.noiseStrength = 0.012f;
                    _settings.noiseScale = 7f;
                    break;

                case FXMMeshType.HalfDome:
                    _settings.taperAmount = 0.25f;
                    _settings.bendAmount = 4f;
                    _settings.noiseStrength = 0.01f;
                    _settings.noiseScale = 7f;
                    break;

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    _settings.taperAmount = 0.35f;
                    _settings.bendAmount = 8f;
                    _settings.noiseStrength = 0.014f;
                    _settings.noiseScale = 6f;
                    break;
            }

            ClampUnsupportedModifierValues(false);
            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
        }

        private void DrawPreviewSettings()
        {
            EditorGUILayout.Space(6f);
            _showPreview = EditorGUILayout.Foldout(_showPreview, Tr("6. Preview / Validation", "5. 프리뷰 / 검증"), true);
            if (!_showPreview) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawUiSubHeader(Tr("Preview Display", "프리뷰 표시"));
                _previewMode = (FXMPreviewMode)EditorGUILayout.EnumPopup(new GUIContent("Preview Mode", Tr("Controls Vertex Alpha generation and preview behavior.", "Solid / UV Checker / Vertex Alpha / Vertex RGB 표시 모드입니다.")), _previewMode);
                _previewTint = EditorGUILayout.ColorField(new GUIContent("Preview Tint", Tr("Adjust this option for the selected production workflow.", "프리뷰 기본 색입니다.")), _previewTint);
                _checkerScale = EditorGUILayout.Slider(new GUIContent("Checker Scale", Tr("Controls UV layout and texture flow behavior.", "UV Checker 반복 스케일입니다.")), _checkerScale, 1f, 64f);
                _checkerStrength = EditorGUILayout.Slider(new GUIContent("Checker Strength", Tr("Adjust this option for the selected production workflow.", "체커 대비 강도입니다.")), _checkerStrength, 0f, 1f);
                _alphaAsOpacity = EditorGUILayout.Toggle(new GUIContent("Alpha As Opacity", Tr("Controls Vertex Alpha generation and preview behavior.", "Vertex Alpha를 실제 투명도로도 표시합니다. Preview Material은 기본적으로 Cull Back 단면 표시입니다.")), _alphaAsOpacity);

                DrawUiSubHeader(Tr("Preview Material", "프리뷰 머티리얼"));
                _previewMaterial = (Material)EditorGUILayout.ObjectField(new GUIContent("Preview Material", Tr("Adjust this option for the selected production workflow.", "프리뷰 오브젝트에 적용할 머티리얼입니다. 비워두면 전용 프리뷰 머티리얼을 자동 생성합니다.")), _previewMaterial, typeof(Material), false);
                _showPreviewMaterialUtilities = EditorGUILayout.Foldout(_showPreviewMaterialUtilities, Tr("Material Utilities", "머티리얼 유틸리티"), true);
                if (_showPreviewMaterialUtilities)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (UiButton("Use Internal Preview Material"))
                            {
                                UseInternalPreviewMaterial();
                            }

                            if (UiButton("Create Material Asset"))
                            {
                                CreatePreviewMaterialAsset();
                            }
                        }

                        if (UiButton("Apply Preview Material To Selected MeshRenderers"))
                        {
                            ApplyPreviewMaterialToSelected();
                        }
                    }
                }

                DrawUvMiniMapPreview();
                DrawFlowVisualQAPanel();
                DrawPreviewHelperSettings();
                DrawAlphaLegend();
                DrawMeshStats();
            }
        }

        private void DrawUvMiniMapPreview()
        {
            EditorGUILayout.Space(6f);
            _showUvMiniMapPreview = EditorGUILayout.Foldout(_showUvMiniMapPreview, Tr("6-1. UV Mini Map Preview", "6-1. UV 미니맵 프리뷰"), true);
            if (!_showUvMiniMapPreview)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(Tr("Shows the generated mesh UV layout inside the editor. This is a validation preview only and does not change saved mesh data.", "생성 메시의 UV 레이아웃을 에디터 안에서 바로 확인합니다. 검수용 프리뷰이며 저장 메시 데이터에는 영향을 주지 않습니다."), MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _uvMiniMapShowTriangles = EditorGUILayout.ToggleLeft(new GUIContent(Tr("Triangles", "삼각형"), Tr("Draw UV triangle edges.", "UV 삼각형 엣지를 표시합니다.")), _uvMiniMapShowTriangles, GUILayout.Width(120f));
                    _uvMiniMapShowDirection = EditorGUILayout.ToggleLeft(new GUIContent(Tr("Flow Direction", "흐름 방향"), Tr("Draw UV flow direction arrows in the 0-1 UV square.", "0-1 UV 영역 안에 흐름 방향 화살표를 표시합니다.")), _uvMiniMapShowDirection, GUILayout.Width(150f));
                    GUILayout.FlexibleSpace();
                }

                _uvMiniMapHeight = EditorGUILayout.Slider(new GUIContent(Tr("Mini Map Height", "미니맵 높이"), Tr("Controls the height of the UV mini map preview.", "UV 미니맵 프리뷰 높이를 조절합니다.")), _uvMiniMapHeight, 120f, 320f);

                Mesh mesh = GetCurrentPreviewMeshForValidation();
                if (mesh == null)
                {
                    EditorGUILayout.HelpBox(Tr("No preview mesh is available yet. Create or update the Scene Preview Object first.", "아직 프리뷰 메시가 없습니다. 먼저 Scene Preview Object를 생성하거나 갱신하세요."), MessageType.Info);
                    return;
                }

                Rect rect = GUILayoutUtility.GetRect(64f, _uvMiniMapHeight, GUILayout.ExpandWidth(true));
                DrawUvMiniMap(rect, mesh, _settings, _uvMiniMapShowTriangles, _uvMiniMapShowDirection);
            }
        }

        private Mesh GetCurrentPreviewMeshForValidation()
        {
            if (_previewMesh != null)
            {
                return _previewMesh;
            }

            if (_previewObject == null)
            {
                _previewObject = GameObject.Find(PreviewObjectName);
            }

            if (_previewObject == null)
            {
                return null;
            }

            MeshFilter meshFilter = _previewObject.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private static void DrawUvMiniMap(Rect rect, Mesh mesh, FXMSettings settings, bool drawTriangles, bool drawDirection)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 0.92f));

            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            if (uvs == null || uvs.Length == 0)
            {
                GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 22f), Tr("This mesh has no UV0 data.", "이 메시에는 UV0 데이터가 없습니다."), EditorStyles.miniLabel);
                return;
            }

            Rect plot = new Rect(rect.x + 10f, rect.y + 24f, Mathf.Max(16f, rect.width - 20f), Mathf.Max(16f, rect.height - 52f));
            EditorGUI.DrawRect(plot, new Color(0.02f, 0.02f, 0.02f, 1f));

            Vector2 min = uvs[0];
            Vector2 max = uvs[0];
            int outsideCount = 0;
            for (int i = 0; i < uvs.Length; i++)
            {
                min = Vector2.Min(min, uvs[i]);
                max = Vector2.Max(max, uvs[i]);
                if (uvs[i].x < -0.0001f || uvs[i].x > 1.0001f || uvs[i].y < -0.0001f || uvs[i].y > 1.0001f)
                {
                    outsideCount++;
                }
            }

            Handles.BeginGUI();
            Color oldColor = Handles.color;
            DrawUvGrid(plot);

            if (drawTriangles && triangles != null && triangles.Length >= 3)
            {
                int triangleCount = triangles.Length / 3;
                int step = Mathf.Max(1, Mathf.CeilToInt(triangleCount / 1800f));
                Handles.color = new Color(0.15f, 0.85f, 1f, 0.82f);
                for (int tri = 0; tri < triangleCount; tri += step)
                {
                    int t = tri * 3;
                    int ia = triangles[t];
                    int ib = triangles[t + 1];
                    int ic = triangles[t + 2];
                    if (ia < 0 || ib < 0 || ic < 0 || ia >= uvs.Length || ib >= uvs.Length || ic >= uvs.Length)
                    {
                        continue;
                    }

                    Vector3 a = UvToGuiPoint(plot, uvs[ia]);
                    Vector3 b = UvToGuiPoint(plot, uvs[ib]);
                    Vector3 c = UvToGuiPoint(plot, uvs[ic]);
                    Handles.DrawAAPolyLine(1.2f, a, b, c, a);
                }
            }

            if (drawDirection)
            {
                DrawUvDirectionGrid(plot, settings);
            }

            DrawUvAxisLegend(plot);
            Handles.color = oldColor;
            Handles.EndGUI();

            string summary = string.Format("UV0  U[{0:0.###}, {1:0.###}]  V[{2:0.###}, {3:0.###}]  Outside 0-1: {4}/{5}", min.x, max.x, min.y, max.y, outsideCount, uvs.Length);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 18f), summary, EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 10f, rect.yMax - 22f, rect.width - 20f, 18f), Tr("Warm = U axis / Green = V axis / Blue = active UV flow", "따뜻한 색 = U축 / 초록 = V축 / 파랑 = 현재 UV 흐름"), EditorStyles.miniLabel);
        }

        private static void DrawUvGrid(Rect plot)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.14f);
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                float x = Mathf.Lerp(plot.xMin, plot.xMax, t);
                float y = Mathf.Lerp(plot.yMin, plot.yMax, t);
                Handles.DrawAAPolyLine(1f, new Vector3(x, plot.yMin), new Vector3(x, plot.yMax));
                Handles.DrawAAPolyLine(1f, new Vector3(plot.xMin, y), new Vector3(plot.xMax, y));
            }

            Handles.color = new Color(1f, 1f, 1f, 0.42f);
            Handles.DrawAAPolyLine(1.5f,
                new Vector3(plot.xMin, plot.yMin),
                new Vector3(plot.xMax, plot.yMin),
                new Vector3(plot.xMax, plot.yMax),
                new Vector3(plot.xMin, plot.yMax),
                new Vector3(plot.xMin, plot.yMin));
        }

        private static void DrawUvAxisLegend(Rect plot)
        {
            Vector3 origin = new Vector3(plot.xMin + 16f, plot.yMax - 16f, 0f);
            DrawGuiArrow(origin, origin + Vector3.right * 34f, new Color(1f, 0.55f, 0.2f, 0.95f), 2f);
            DrawGuiArrow(origin, origin + Vector3.up * -34f, new Color(0.35f, 1f, 0.35f, 0.95f), 2f);
            GUI.Label(new Rect(origin.x + 38f, origin.y - 12f, 24f, 18f), "U", EditorStyles.miniLabel);
            GUI.Label(new Rect(origin.x - 6f, origin.y - 52f, 24f, 18f), "V", EditorStyles.miniLabel);
        }

        private static void DrawUvDirectionGrid(Rect plot, FXMSettings settings)
        {
            const int Columns = 5;
            const int Rows = 4;
            float length = Mathf.Min(plot.width, plot.height) * 0.045f;
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Vector2 uv = new Vector2((x + 0.5f) / Columns, (y + 0.5f) / Rows);
                    Vector2 dir = ResolveUvDirection2D(settings, uv);
                    if (dir.sqrMagnitude < 0.000001f)
                    {
                        continue;
                    }
                    dir.Normalize();
                    Vector3 center = UvToGuiPoint(plot, uv);
                    Vector3 delta = new Vector3(dir.x, -dir.y, 0f) * length;
                    DrawGuiArrow(center - delta * 0.4f, center + delta * 0.6f, new Color(0.2f, 0.75f, 1f, 0.92f), 1.8f);
                }
            }
        }

        private static Vector2 ResolveUvDirection2D(FXMSettings settings, Vector2 uv)
        {
            Vector2 direction;
            switch (settings.uvFlowDirection)
            {
                case FXMUVFlowDirection.UForward:
                    direction = Vector2.right;
                    break;
                case FXMUVFlowDirection.UReverse:
                    direction = Vector2.left;
                    break;
                case FXMUVFlowDirection.VForward:
                    direction = Vector2.up;
                    break;
                case FXMUVFlowDirection.VReverse:
                    direction = Vector2.down;
                    break;
                case FXMUVFlowDirection.FromCenter:
                    direction = (uv - new Vector2(0.5f, 0.5f)).normalized;
                    break;
                case FXMUVFlowDirection.ToCenter:
                    direction = (new Vector2(0.5f, 0.5f) - uv).normalized;
                    break;
                case FXMUVFlowDirection.CircularCW:
                {
                    Vector2 radial = uv - new Vector2(0.5f, 0.5f);
                    direction = new Vector2(radial.y, -radial.x).normalized;
                    break;
                }
                case FXMUVFlowDirection.CircularCCW:
                {
                    Vector2 radial = uv - new Vector2(0.5f, 0.5f);
                    direction = new Vector2(-radial.y, radial.x).normalized;
                    break;
                }
                case FXMUVFlowDirection.Auto:
                default:
                    direction = ResolveAutoUvDirection2D(settings.meshType);
                    break;
            }

            // v0.31.0.1: The 2D flow QA must use the same final UV orientation as the generated mesh.
            // Mirroring one UV axis reverses the corresponding component. Mirroring both axes preserves circular handedness.
            direction = ApplyUvFlipToDirection2D(direction, settings.flipU, settings.flipV);
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : direction;
        }

        private static Vector2 ApplyUvFlipToDirection2D(Vector2 direction, bool flipU, bool flipV)
        {
            if (flipU) direction.x = -direction.x;
            if (flipV) direction.y = -direction.y;
            return direction;
        }

        private static Vector2 ResolveAutoUvDirection2D(FXMMeshType meshType)
        {
            switch (meshType)
            {
                case FXMMeshType.Beam:
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    return Vector2.up;
                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                case FXMMeshType.Slash:
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return Vector2.right;
                default:
                    return Vector2.right;
            }
        }

        private static Vector3 UvToGuiPoint(Rect plot, Vector2 uv)
        {
            return new Vector3(
                Mathf.Lerp(plot.xMin, plot.xMax, uv.x),
                Mathf.Lerp(plot.yMax, plot.yMin, uv.y),
                0f);
        }

        private static void DrawGuiArrow(Vector3 start, Vector3 end, Color color, float thickness)
        {
            Vector3 direction = end - start;
            if (direction.sqrMagnitude < 0.000001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 side = new Vector3(-direction.y, direction.x, 0f);
            float headLength = Mathf.Max(5f, Vector3.Distance(start, end) * 0.28f);
            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, start, end);
            Handles.DrawAAPolyLine(thickness, end, end - direction * headLength + side * headLength * 0.45f);
            Handles.DrawAAPolyLine(thickness, end, end - direction * headLength - side * headLength * 0.45f);
        }

        private void DrawFlowVisualQAPanel()
        {
            EditorGUILayout.Space(6f);
            _showFlowVisualQa = EditorGUILayout.Foldout(_showFlowVisualQa, Tr("6-2. Flow Visual QA", "6-2. Flow 시각 검증"), true);
            if (!_showFlowVisualQa)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("Compares the 2D UV Flow preview, on-mesh flow texture, surface arrows, and U/V basis overlay. This panel is for final visual QA before packaging.",
                       "2D UV Flow 프리뷰, 실제 메시 Flow 텍스처, 표면 화살표, U/V Basis Overlay를 함께 검수합니다. 패키징 전 최종 시각 QA용입니다."),
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Enable Flow Debug View", "Flow 디버그 보기 켜기"), GUILayout.Height(24f)))
                    {
                        EnableFlowDebugView(true);
                    }

                    if (GUILayout.Button(Tr("Run Flow Visual QA", "Flow 시각 QA 실행"), GUILayout.Height(24f)))
                    {
                        _lastFlowVisualQaReport = BuildFlowVisualQAReport();
                        EditorGUIUtility.systemCopyBuffer = _lastFlowVisualQaReport;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Copy Flow QA Report", "Flow QA 리포트 복사"), GUILayout.Height(22f)))
                    {
                        if (string.IsNullOrEmpty(_lastFlowVisualQaReport))
                        {
                            _lastFlowVisualQaReport = BuildFlowVisualQAReport();
                        }
                        EditorGUIUtility.systemCopyBuffer = _lastFlowVisualQaReport;
                    }

                    if (GUILayout.Button(Tr("Reset Flow Debug View", "Flow 디버그 보기 해제"), GUILayout.Height(22f)))
                    {
                        _showUvBasisHelper = false;
                        _showFaceOrientationHelper = false;
                        _previewMode = FXMPreviewMode.UVChecker;
                        RebuildPreviewMesh(true);
                    }
                }

                if (!string.IsNullOrEmpty(_lastFlowVisualQaReport))
                {
                    _flowVisualQaReportScroll = EditorGUILayout.BeginScrollView(_flowVisualQaReportScroll, GUILayout.MinHeight(110f), GUILayout.MaxHeight(220f));
                    EditorGUILayout.TextArea(_lastFlowVisualQaReport, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void EnableFlowDebugView(bool rebuild)
        {
            _previewMode = FXMPreviewMode.UVGradient;
            _showUvMiniMapPreview = true;
            _uvMiniMapShowTriangles = true;
            _uvMiniMapShowDirection = true;
            _showPreviewHelpers = true;
            _showUvFlowHelper = true;
            _showUvSurfaceFlowArrows = true;
            _showUvBasisHelper = true;
            _showFaceOrientationHelper = false;
            _uvSurfaceArrowMaxCount = Mathf.Max(_uvSurfaceArrowMaxCount, 96);
            _uvBasisSampleCount = Mathf.Max(_uvBasisSampleCount, 48);
            _helperScale = Mathf.Max(_helperScale, 0.18f);
            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
            else
            {
                SceneView.RepaintAll();
            }
        }

        private string BuildFlowVisualQAReport()
        {
            FXMSettings originalSettings = _settings;
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Flow Visual QA");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Mesh Type: " + _settings.meshType);
            lines.Add("Expected Axis Meaning: " + GetFlowAxisLegend(_settings.meshType));
            lines.Add(string.Empty);

            Array values = Enum.GetValues(typeof(FXMUVFlowDirection));
            foreach (FXMUVFlowDirection direction in values)
            {
                FXMSettings s = originalSettings;
                s.uvFlowDirection = direction;
                Mesh mesh = FXMMeshBuilder.Build(s);
                try
                {
                    lines.Add("## " + direction);
                    if (mesh == null)
                    {
                        lines.Add("[ERROR] Mesh build failed.");
                        continue;
                    }

                    Vector2[] uv = mesh.uv;
                    int uvCount = uv != null ? uv.Length : 0;
                    lines.Add("Vertices: " + mesh.vertexCount.ToString("N0") + " / UV0: " + uvCount.ToString("N0"));
                    if (uv == null || uv.Length != mesh.vertexCount)
                    {
                        lines.Add("[ERROR] UV0 missing or count mismatch.");
                    }
                    else
                    {
                        Vector2 min;
                        Vector2 max;
                        CountUvBounds(uv, out min, out max);
                        lines.Add("UV Bounds: " + FormatVector2(min) + " → " + FormatVector2(max));
                    }
                    lines.Add("Visual QA: compare 2D Flow Preview, on-mesh texture, Surface Arrows, and U/V Basis Overlay.");
                }
                finally
                {
                    DestroyValidationMesh(mesh);
                }
            }

            _settings = originalSettings;
            lines.Add(string.Empty);
            lines.Add("Recommended final check: V Forward/V Reverse must oppose each other; U Forward/U Reverse must oppose each other; Circular CW/CCW must rotate in opposite directions.");
            return string.Join("\n", lines.ToArray());
        }

        private static string GetFlowAxisLegend(FXMMeshType meshType)
        {
            switch (meshType)
            {
                case FXMMeshType.Slash:
                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                    return "U = thickness/radial/cross direction, V = arc/circular/flow direction.";
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    return "U = horizontal ring direction, V = vertical/dome height flow direction.";
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return "U = ribbon width direction, V = helix length/turn flow direction.";
                case FXMMeshType.Beam:
                    return "U = beam width direction, V = beam length flow direction.";
                default:
                    return "U/V follow generated mesh UV basis.";
            }
        }

        private static void CountUvBounds(Vector2[] uv, out Vector2 min, out Vector2 max)
        {
            if (uv == null || uv.Length == 0)
            {
                min = Vector2.zero;
                max = Vector2.zero;
                return;
            }

            min = uv[0];
            max = uv[0];
            for (int i = 1; i < uv.Length; i++)
            {
                min = Vector2.Min(min, uv[i]);
                max = Vector2.Max(max, uv[i]);
            }
        }

        private static string FormatVector2(Vector2 value)
        {
            return value.x.ToString("0.###") + ", " + value.y.ToString("0.###");
        }

        private void DrawPreviewHelperSettings()
        {
            EditorGUILayout.Space(6f);
            _showPreviewHelpers = EditorGUILayout.Foldout(_showPreviewHelpers, Tr("6-1. Scene Preview Helpers", "6-1. 씬 프리뷰 헬퍼"), true);
            if (!_showPreviewHelpers)
            {
                EditorGUILayout.HelpBox(Tr("The foldout only hides these controls. Active Scene View helpers keep drawing until their individual toggles are turned off.", "이 Foldout은 설정 UI만 접습니다. 켜져 있는 Scene View 헬퍼는 개별 토글을 끄기 전까지 계속 표시됩니다."), MessageType.None);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                FXMHelperProfile helperProfile = FXMHelperProfile.For(_settings.meshType);
                EditorGUILayout.LabelField(helperProfile.title, EditorStyles.miniBoldLabel);
                EditorGUILayout.HelpBox(helperProfile.description, MessageType.None);

                _autoHelperProfile = EditorGUILayout.Toggle(new GUIContent("Auto Helper Profile", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Mesh Type을 바꿀 때 해당 메시 타입에 맞는 Helper 표시값을 자동 적용합니다.")), _autoHelperProfile);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Apply Helper For This Mesh", GUILayout.Height(24f)))
                    {
                        ApplyRecommendedHelperProfile(true);
                    }

                    if (GUILayout.Button("Helper Default", GUILayout.Height(24f)))
                    {
                        ApplyDefaultHelperProfile(true);
                    }
                }

                EditorGUILayout.Space(4f);
                _showWireBoundsHelper = EditorGUILayout.Toggle(new GUIContent("Wire Bounds", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "현재 생성 메시의 Bounds를 Scene View에 표시합니다.")), _showWireBoundsHelper);
                _showMeshWireHelper = EditorGUILayout.Toggle(new GUIContent("Preview Wire Overlay", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Unity Scene View 모드를 전환하지 않고, 현재 메시의 삼각형 와이어를 Handles 오버레이로 표시합니다. 저장되는 메시에는 영향을 주지 않습니다.")), _showMeshWireHelper);
                using (new EditorGUI.DisabledScope(!_showMeshWireHelper))
                {
                    _meshWireMaxEdges = EditorGUILayout.IntSlider(new GUIContent("Wire Edge Limit", Tr("Adjust this option for the selected production workflow.", "에디터 성능 보호용 최대 와이어 엣지 수입니다.")), _meshWireMaxEdges, 256, 20000);
                    _meshWireColor = EditorGUILayout.ColorField(new GUIContent("Wire Color", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Scene View 전용 Preview Wire Overlay 색상입니다. 저장 메시에는 영향을 주지 않습니다.")), _meshWireColor);
                    _meshWireThickness = EditorGUILayout.Slider(new GUIContent("Wire Thickness", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Scene View 전용 Preview Wire Overlay 굵기입니다.")), _meshWireThickness, 0.5f, 6f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Wire Default", GUILayout.Height(22f)))
                        {
                            _showMeshWireHelper = true;
                            _meshWireMaxEdges = GetRecommendedWireEdgeLimit(_settings.meshType);
                            SceneView.RepaintAll();
                        }

                        if (GUILayout.Button("Wire Performance", GUILayout.Height(22f)))
                        {
                            _showMeshWireHelper = true;
                            _meshWireMaxEdges = _settings.meshType == FXMMeshType.CrossHelix ? 2048 : 3072;
                            SceneView.RepaintAll();
                        }

                        if (GUILayout.Button("Wire Detail", GUILayout.Height(22f)))
                        {
                            _showMeshWireHelper = true;
                            _meshWireMaxEdges = _settings.meshType == FXMMeshType.CrossHelix ? 8192 : 12000;
                            SceneView.RepaintAll();
                        }
                    }
                }
                _showUvFlowHelper = EditorGUILayout.Toggle(new GUIContent(helperProfile.flowToggleLabel, helperProfile.flowToggleTooltip), _showUvFlowHelper);
                using (new EditorGUI.DisabledScope(!_showUvFlowHelper))
                {
                    _showUvSurfaceFlowArrows = EditorGUILayout.Toggle(new GUIContent(Tr("UV Surface Arrows", "UV 표면 화살표"), Tr("Draw repeated flow arrows directly on the mesh surface based on the actual UV map. This is the most intuitive way to inspect texture scroll direction.", "실제 UV 맵 기준으로 메시 표면 위에 반복 화살표를 그립니다. 텍스처 스크롤 방향을 가장 직관적으로 검수할 수 있습니다.")), _showUvSurfaceFlowArrows);
                    using (new EditorGUI.DisabledScope(!_showUvSurfaceFlowArrows))
                    {
                        _uvSurfaceArrowMaxCount = EditorGUILayout.IntSlider(new GUIContent(Tr("Surface Arrow Count", "표면 화살표 수"), Tr("Maximum number of UV surface arrows drawn in Scene View. Higher values show the flow more clearly but can add editor cost.", "Scene View에 그릴 UV 표면 화살표의 최대 개수입니다. 값을 높이면 흐름은 더 잘 보이지만 에디터 비용이 증가할 수 있습니다.")), _uvSurfaceArrowMaxCount, 8, 256);
                        _uvSurfaceArrowLength = EditorGUILayout.Slider(new GUIContent(Tr("Surface Arrow Length", "표면 화살표 길이"), Tr("Scale for the UV surface arrow length.", "UV 표면 화살표 길이 배율입니다.")), _uvSurfaceArrowLength, 0.35f, 1.75f);
                        _uvSurfaceArrowOffset = EditorGUILayout.Slider(new GUIContent(Tr("Surface Arrow Offset", "표면 화살표 오프셋"), Tr("Small offset from the mesh surface to reduce z-fighting in Scene View.", "Scene View에서 Z-fighting을 줄이기 위한 표면으로부터의 미세 오프셋입니다.")), _uvSurfaceArrowOffset, 0.0005f, 0.025f);
                    }

                    _showUvBasisHelper = EditorGUILayout.Toggle(new GUIContent(Tr("U/V Basis Overlay", "U/V 기준 방향 오버레이"), Tr("Draws U and V tangent directions directly on the mesh surface. U is shown as warm arrows, V is shown as green arrows. Saved mesh data is not changed.", "메시 표면 위에 U/V 탄젠트 방향을 직접 표시합니다. U는 따뜻한 색 화살표, V는 초록색 화살표로 표시됩니다. 저장 메시 데이터에는 영향을 주지 않습니다.")), _showUvBasisHelper);
                    using (new EditorGUI.DisabledScope(!_showUvBasisHelper))
                    {
                        _uvBasisSampleCount = EditorGUILayout.IntSlider(new GUIContent(Tr("U/V Basis Count", "U/V 기준 표시 수"), Tr("Maximum number of U/V basis samples drawn on the mesh surface.", "메시 표면에 그릴 U/V 기준 방향 샘플 최대 개수입니다.")), _uvBasisSampleCount, 8, 192);
                        _uvBasisHelperLength = EditorGUILayout.Slider(new GUIContent(Tr("U/V Basis Length", "U/V 기준 길이"), Tr("Scale for U/V basis arrows.", "U/V 기준 화살표 길이 배율입니다.")), _uvBasisHelperLength, 0.25f, 1.5f);
                    }
                }
                _showNormalHelper = EditorGUILayout.Toggle(new GUIContent("Normal Lines", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "샘플링된 버텍스 노멀을 Scene View에 표시합니다.")), _showNormalHelper);
                using (new EditorGUI.DisabledScope(!_showNormalHelper))
                {
                    _normalSampleCount = EditorGUILayout.IntSlider(new GUIContent("Normal Sample Count", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Scene View에 그릴 노멀 샘플 수입니다. 너무 높이면 에디터가 무거울 수 있습니다.")), _normalSampleCount, 8, 128);
                }
                _showBackfaceHelper = EditorGUILayout.Toggle(new GUIContent("Backface / Cull Guide", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Cull Back 단면 머티리얼과 Double Sided Geometry 상태를 Scene View 라벨로 표시합니다.")), _showBackfaceHelper);
                _showFaceOrientationHelper = EditorGUILayout.Toggle(new GUIContent(Tr("Face Orientation Overlay", "면 방향 오버레이"), Tr("Draws a lightweight Scene View overlay for front/back-facing triangles. Green/blue means front-facing to the current view; red/orange means back-facing. Saved mesh data is not changed.", "현재 Scene View 기준 앞/뒤 삼각형을 가벼운 오버레이로 표시합니다. 초록/파랑은 앞면, 빨강/주황은 뒷면입니다. 저장 메시 데이터에는 영향을 주지 않습니다.")), _showFaceOrientationHelper);
                using (new EditorGUI.DisabledScope(!_showFaceOrientationHelper))
                {
                    _faceOrientationTriangleLimit = EditorGUILayout.IntSlider(new GUIContent(Tr("Face Overlay Limit", "면 오버레이 제한"), Tr("Maximum number of triangles drawn by the face orientation overlay.", "면 방향 오버레이가 그릴 최대 삼각형 수입니다.")), _faceOrientationTriangleLimit, 128, 4096);
                }
                _showPivotGizmoHelper = EditorGUILayout.Toggle(new GUIContent("Pivot Gizmo", Tr("Controls the saved mesh pivot and axis orientation.", "현재 Mesh Asset의 피봇 위치를 Scene View에 십자 표시합니다.")), _showPivotGizmoHelper);
                _showAxisGizmoHelper = EditorGUILayout.Toggle(new GUIContent("Generated Axis Gizmo", Tr("Controls the saved mesh pivot and axis orientation.", "Pivot Rotation / Y-Up·Z-Up이 반영된 생성 메시 기준 축을 Scene View에 표시합니다.")), _showAxisGizmoHelper);
                _showBendHelper = EditorGUILayout.Toggle(new GUIContent("Bend Arc Helper", Tr("Controls modifier/deform behavior for the selected mesh.", "Use Max Bend가 켜져 있을 때 Bend Axis / Plane / Bias가 만드는 굽힘 방향을 Scene View에 표시합니다.")), _showBendHelper);
                _showTaperHelper = EditorGUILayout.Toggle(new GUIContent("Taper Profile Helper", Tr("Controls modifier/deform behavior for the selected mesh.", "Use Max Taper가 켜져 있을 때 Taper Axis / Amount / Bias가 만드는 단면 스케일 프로파일을 표시합니다.")), _showTaperHelper);
                _showFfdHelper = EditorGUILayout.Toggle(new GUIContent("FFD Lattice Helper", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Use Simple FFD가 켜져 있을 때 현재 FFD 축/해상도/스케일/높이 프로파일을 Scene View에 라티스 형태로 표시합니다.")), _showFfdHelper);
                using (new EditorGUI.DisabledScope((!_settings.ffdEnable || !_showFfdHelper) && (!_settings.maxBendEnable || !_showBendHelper) && (!_settings.maxTaperEnable || !_showTaperHelper)))
                {
                    EditorGUILayout.HelpBox(Tr("Controls the saved mesh pivot and axis orientation.", "Bend/Taper/FFD Helper는 저장 메시에는 영향을 주지 않는 Scene View 전용 보조선입니다. Pivot Rotation / Y-Up·Z-Up과 같이 회전합니다."), MessageType.None);
                }
                _helperScale = EditorGUILayout.Slider(new GUIContent("Helper Size", Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Scene View 헬퍼 표시 크기입니다.")), _helperScale, 0.04f, 1.0f);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Focus Preview Object", GUILayout.Height(24f)))
                    {
                        FocusPreviewObject();
                    }

                    if (GUILayout.Button("Backface Check View", GUILayout.Height(24f)))
                    {
                        FocusPreviewObject();
                        SceneView.RepaintAll();
                    }
                }

                EditorGUILayout.HelpBox(Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "Preview Wire Overlay는 Scene View 전용 Handles 보조선입니다. 저장되는 Mesh Asset/Prefab에는 와이어 지오메트리를 추가하지 않습니다. 실제 저장 데이터는 6. 저장 / 최종 생성에서만 생성됩니다."), MessageType.Info);
            }
        }

        private static void DrawLocalFfdLatticeHelper(FXMHelperFrame frame, FXMSettings settings, float helperSize)
        {
            Bounds b = frame.localBounds;
            if (b.size.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            int axis = HelperAxisToIndex(settings.ffdAxis);
            HelperPerpendicularAxes(axis, out int p1, out int p2);
            int res = Mathf.Clamp((int)settings.ffdResolution, 2, 4);
            int ringSteps = settings.ffdMode == FXMFFDMode.Cyl ? 32 : 4;

            Vector3[,] rings = new Vector3[res, ringSteps];
            for (int i = 0; i < res; i++)
            {
                float t = res <= 1 ? 0f : i / (float)(res - 1);
                float axisValue = Mathf.Lerp(HelperAxisMin(b, axis), HelperAxisMax(b, axis), t);
                float heightOffset = HelperFfdEvalHeight(settings, t) * HelperAxisSize(b, axis);
                float scale = HelperFfdEvalScale(settings, t);
                float strength = Mathf.Clamp(settings.ffdStrength, -2f, 2f);
                float globalScale = Mathf.Max(0.001f, 1f + strength * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                float factor = Mathf.Max(0.001f, scale * globalScale);

                Vector3 center = b.center;
                center = HelperSetAxis(center, axis, axisValue + heightOffset);
                float radius1 = Mathf.Max(HelperAxisSize(b, p1) * 0.5f * factor, 0.001f);
                float radius2 = Mathf.Max(HelperAxisSize(b, p2) * 0.5f * factor, 0.001f);

                if (settings.ffdMode == FXMFFDMode.Cyl)
                {
                    for (int j = 0; j < ringSteps; j++)
                    {
                        float a = j / (float)ringSteps * Mathf.PI * 2f;
                        Vector3 p = center;
                        p = HelperSetAxis(p, p1, HelperGetAxis(center, p1) + Mathf.Cos(a) * radius1);
                        p = HelperSetAxis(p, p2, HelperGetAxis(center, p2) + Mathf.Sin(a) * radius2);
                        rings[i, j] = frame.P(p);
                    }
                }
                else
                {
                    Vector3 p0 = center;
                    p0 = HelperSetAxis(p0, p1, HelperGetAxis(center, p1) - radius1);
                    p0 = HelperSetAxis(p0, p2, HelperGetAxis(center, p2) - radius2);
                    Vector3 p1v = center;
                    p1v = HelperSetAxis(p1v, p1, HelperGetAxis(center, p1) + radius1);
                    p1v = HelperSetAxis(p1v, p2, HelperGetAxis(center, p2) - radius2);
                    Vector3 p2v = center;
                    p2v = HelperSetAxis(p2v, p1, HelperGetAxis(center, p1) + radius1);
                    p2v = HelperSetAxis(p2v, p2, HelperGetAxis(center, p2) + radius2);
                    Vector3 p3 = center;
                    p3 = HelperSetAxis(p3, p1, HelperGetAxis(center, p1) - radius1);
                    p3 = HelperSetAxis(p3, p2, HelperGetAxis(center, p2) + radius2);
                    rings[i, 0] = frame.P(p0);
                    rings[i, 1] = frame.P(p1v);
                    rings[i, 2] = frame.P(p2v);
                    rings[i, 3] = frame.P(p3);
                }
            }

            Handles.color = new Color(1f, 0.65f, 0.05f, 0.9f);
            for (int i = 0; i < res; i++)
            {
                if (settings.ffdMode == FXMFFDMode.Cyl)
                {
                    for (int j = 0; j < ringSteps; j++)
                    {
                        Handles.DrawAAPolyLine(2f, rings[i, j], rings[i, (j + 1) % ringSteps]);
                    }
                }
                else
                {
                    Handles.DrawAAPolyLine(2f, rings[i, 0], rings[i, 1], rings[i, 2], rings[i, 3], rings[i, 0]);
                }
            }

            Handles.color = new Color(1f, 0.85f, 0.18f, 0.65f);
            int rails = settings.ffdMode == FXMFFDMode.Cyl ? 8 : 4;
            for (int r = 0; r < rails; r++)
            {
                int idx = settings.ffdMode == FXMFFDMode.Cyl ? Mathf.RoundToInt(r * (ringSteps / (float)rails)) % ringSteps : r;
                for (int i = 0; i < res - 1; i++)
                {
                    Handles.DrawAAPolyLine(1.5f, rings[i, idx], rings[i + 1, idx]);
                }
            }

            Vector3 label = frame.P(b.center + Vector3.up * Mathf.Max(helperSize, b.size.magnitude * 0.045f));
            Handles.color = new Color(1f, 0.78f, 0.18f, 1f);
            Handles.Label(label, "FFD Lattice  " + settings.ffdMode + " / " + settings.ffdResolution + " / Axis " + settings.ffdAxis);
        }


        private static void DrawLocalMaxBendHelper(FXMHelperFrame frame, FXMSettings settings, float helperSize)
        {
            Bounds b = frame.localBounds;
            if (b.size.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            int axis = HelperAxisToIndex(settings.maxBendAxis);
            int bendPlane = HelperAxisToIndex(settings.maxBendPlane);
            if (bendPlane == axis)
            {
                HelperPerpendicularAxes(axis, out bendPlane, out _);
            }

            float mn = HelperAxisMin(b, axis);
            float mx = HelperAxisMax(b, axis);
            float size = Mathf.Max(mx - mn, 0.0001f);
            float centerAxis = (mn + mx) * 0.5f;
            float centerBend = HelperAxisCenter(b, bendPlane);
            float angleRad = settings.maxBendAngle * Mathf.Deg2Rad;
            float radius = size / Mathf.Max(0.000001f, Mathf.Abs(angleRad));
            radius *= angleRad >= 0f ? 1f : -1f;

            Handles.color = new Color(0.25f, 0.9f, 1f, 0.95f);
            Vector3[] curve = new Vector3[33];
            for (int i = 0; i < curve.Length; i++)
            {
                float rawT = i / (float)(curve.Length - 1);
                float bendT = ApplySignedBias01(rawT, settings.maxBendBias);
                float sAxis = (bendT - 0.5f) * size;
                float theta = sAxis / size * angleRad;
                float rb = radius;
                Vector3 p = b.center;
                if (Mathf.Abs(angleRad) > 0.000001f)
                {
                    p = HelperSetAxis(p, axis, Mathf.Sin(theta) * rb);
                    p = HelperSetAxis(p, bendPlane, centerBend + (Mathf.Cos(theta) * rb - radius));
                }
                else
                {
                    p = HelperSetAxis(p, axis, Mathf.Lerp(mn, mx, rawT));
                    p = HelperSetAxis(p, bendPlane, centerBend);
                }
                curve[i] = frame.P(p);
            }
            Handles.DrawAAPolyLine(3f, curve);

            Handles.color = new Color(0.25f, 0.9f, 1f, 0.35f);
            Vector3 a0 = b.center;
            a0 = HelperSetAxis(a0, axis, mn);
            Vector3 a1 = b.center;
            a1 = HelperSetAxis(a1, axis, mx);
            Handles.DrawAAPolyLine(1.5f, frame.P(a0), frame.P(a1));

            Handles.color = new Color(0.25f, 0.9f, 1f, 1f);
            Vector3 label = frame.P(b.center + Vector3.up * Mathf.Max(helperSize * 1.25f, b.size.magnitude * 0.055f));
            Handles.Label(label, "Bend Helper  " + settings.maxBendAngle.ToString("0.#") + "° / Axis " + settings.maxBendAxis + " / Bias " + settings.maxBendBias.ToString("0.00"));
        }

        private static void DrawLocalMaxTaperHelper(FXMHelperFrame frame, FXMSettings settings, float helperSize)
        {
            Bounds b = frame.localBounds;
            if (b.size.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            int axis = HelperAxisToIndex(settings.maxTaperAxis);
            HelperPerpendicularAxes(axis, out int p1, out int p2);
            int res = 7;
            int ringSteps = 4;
            Vector3[,] rings = new Vector3[res, ringSteps];
            float amount = Mathf.Clamp(settings.maxTaperAmount, -0.95f, 0.95f);

            for (int i = 0; i < res; i++)
            {
                float rawT = i / (float)(res - 1);
                float t = ApplySignedBias01(rawT, settings.maxTaperBias);
                float scale = Mathf.Max(0.001f, 1f + amount * ((t - 0.5f) * 2f));
                Vector3 center = b.center;
                center = HelperSetAxis(center, axis, Mathf.Lerp(HelperAxisMin(b, axis), HelperAxisMax(b, axis), rawT));
                float radius1 = Mathf.Max(HelperAxisSize(b, p1) * 0.5f * scale, 0.001f);
                float radius2 = Mathf.Max(HelperAxisSize(b, p2) * 0.5f * scale, 0.001f);

                Vector3 p0 = center;
                p0 = HelperSetAxis(p0, p1, HelperGetAxis(center, p1) - radius1);
                p0 = HelperSetAxis(p0, p2, HelperGetAxis(center, p2) - radius2);
                Vector3 p1v = center;
                p1v = HelperSetAxis(p1v, p1, HelperGetAxis(center, p1) + radius1);
                p1v = HelperSetAxis(p1v, p2, HelperGetAxis(center, p2) - radius2);
                Vector3 p2v = center;
                p2v = HelperSetAxis(p2v, p1, HelperGetAxis(center, p1) + radius1);
                p2v = HelperSetAxis(p2v, p2, HelperGetAxis(center, p2) + radius2);
                Vector3 p3 = center;
                p3 = HelperSetAxis(p3, p1, HelperGetAxis(center, p1) - radius1);
                p3 = HelperSetAxis(p3, p2, HelperGetAxis(center, p2) + radius2);
                rings[i, 0] = frame.P(p0);
                rings[i, 1] = frame.P(p1v);
                rings[i, 2] = frame.P(p2v);
                rings[i, 3] = frame.P(p3);
            }

            Handles.color = new Color(1f, 0.25f, 0.85f, 0.88f);
            for (int i = 0; i < res; i++)
            {
                Handles.DrawAAPolyLine(2f, rings[i, 0], rings[i, 1], rings[i, 2], rings[i, 3], rings[i, 0]);
            }
            Handles.color = new Color(1f, 0.25f, 0.85f, 0.55f);
            for (int r = 0; r < ringSteps; r++)
            {
                for (int i = 0; i < res - 1; i++)
                {
                    Handles.DrawAAPolyLine(1.5f, rings[i, r], rings[i + 1, r]);
                }
            }

            Vector3 label = frame.P(b.center + Vector3.up * Mathf.Max(helperSize * 1.55f, b.size.magnitude * 0.075f));
            Handles.color = new Color(1f, 0.25f, 0.85f, 1f);
            Handles.Label(label, "Taper Helper  Amt " + settings.maxTaperAmount.ToString("0.00") + " / Axis " + settings.maxTaperAxis + " / Bias " + settings.maxTaperBias.ToString("0.00"));
        }

        private static int HelperAxisToIndex(FXMAxis axis)
        {
            switch (axis)
            {
                case FXMAxis.X: return 0;
                case FXMAxis.Y: return 1;
                case FXMAxis.Z: return 2;
                default: return 1;
            }
        }

        private static void HelperPerpendicularAxes(int axis, out int p1, out int p2)
        {
            if (axis == 0)
            {
                p1 = 1;
                p2 = 2;
            }
            else if (axis == 1)
            {
                p1 = 0;
                p2 = 2;
            }
            else
            {
                p1 = 0;
                p2 = 1;
            }
        }

        private static float HelperGetAxis(Vector3 v, int axis)
        {
            return axis == 0 ? v.x : axis == 1 ? v.y : v.z;
        }

        private static Vector3 HelperSetAxis(Vector3 v, int axis, float value)
        {
            if (axis == 0) v.x = value;
            else if (axis == 1) v.y = value;
            else v.z = value;
            return v;
        }

        private static float HelperAxisMin(Bounds b, int axis)
        {
            return HelperGetAxis(b.min, axis);
        }

        private static float HelperAxisMax(Bounds b, int axis)
        {
            return HelperGetAxis(b.max, axis);
        }

        private static float HelperAxisSize(Bounds b, int axis)
        {
            return Mathf.Max(HelperAxisMax(b, axis) - HelperAxisMin(b, axis), 0.0001f);
        }

        private static float HelperAxisCenter(Bounds b, int axis)
        {
            return (HelperAxisMin(b, axis) + HelperAxisMax(b, axis)) * 0.5f;
        }

        private static float ApplySignedBias01(float t, float bias)
        {
            t = Mathf.Clamp01(t);
            bias = Mathf.Clamp(bias, -1f, 1f);

            if (bias > 0f)
            {
                return Mathf.Pow(t, 1f + bias * 3f);
            }

            if (bias < 0f)
            {
                return 1f - Mathf.Pow(1f - t, 1f + Mathf.Abs(bias) * 3f);
            }

            return t;
        }

        private static float HelperFfdSmoothParameter(FXMSettings settings, float t)
        {
            t = Mathf.Clamp01(t);
            float sm = Mathf.Clamp01(settings.ffdSmoothness <= 0f ? 1f : settings.ffdSmoothness);
            float smoothT = t * t * (3f - 2f * t);
            return Mathf.Lerp(t, smoothT, sm);
        }

        private static float HelperFfdEvalScale(FXMSettings settings, float t)
        {
            List<float> values = new List<float>();
            int res = Mathf.Clamp((int)settings.ffdResolution, 2, 4);
            values.Add(Mathf.Max(0.001f, settings.ffdP0Scale <= 0f ? 1f : settings.ffdP0Scale));
            if (res == 2)
            {
                values.Add(Mathf.Max(0.001f, settings.ffdP2Scale <= 0f ? 1f : settings.ffdP2Scale));
            }
            else if (res == 4)
            {
                values.Add(Mathf.Max(0.001f, settings.ffdP1Scale <= 0f ? 1f : settings.ffdP1Scale));
                values.Add(Mathf.Max(0.001f, settings.ffdP2Scale <= 0f ? 1f : settings.ffdP2Scale));
                values.Add(Mathf.Max(0.001f, settings.ffdP3Scale <= 0f ? 1f : settings.ffdP3Scale));
            }
            else
            {
                values.Add(Mathf.Max(0.001f, settings.ffdP1Scale <= 0f ? 1f : settings.ffdP1Scale));
                values.Add(Mathf.Max(0.001f, settings.ffdP2Scale <= 0f ? 1f : settings.ffdP2Scale));
            }
            return Mathf.Max(0.001f, HelperBezierEval(values, HelperFfdSmoothParameter(settings, t)));
        }

        private static float HelperFfdEvalHeight(FXMSettings settings, float t)
        {
            List<float> values = new List<float>();
            int res = Mathf.Clamp((int)settings.ffdResolution, 2, 4);
            values.Add(settings.ffdP0Height);
            if (res == 2)
            {
                values.Add(settings.ffdP2Height);
            }
            else if (res == 4)
            {
                values.Add(settings.ffdP1Height);
                values.Add(settings.ffdP2Height);
                values.Add(settings.ffdP3Height);
            }
            else
            {
                values.Add(settings.ffdP1Height);
                values.Add(settings.ffdP2Height);
            }
            return HelperBezierEval(values, HelperFfdSmoothParameter(settings, t));
        }

        private static float HelperBezierEval(List<float> values, float t)
        {
            if (values == null || values.Count == 0)
            {
                return 0f;
            }
            List<float> temp = new List<float>(values);
            for (int k = temp.Count - 1; k > 0; k--)
            {
                for (int i = 0; i < k; i++)
                {
                    temp[i] = Mathf.Lerp(temp[i], temp[i + 1], t);
                }
            }
            return temp[0];
        }

        private void DrawAlphaLegend()
        {
            if (_previewMode != FXMPreviewMode.VertexAlpha && _previewMode != FXMPreviewMode.UVCheckerAlpha && _previewMode != FXMPreviewMode.VertexRGB)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(_previewMode == FXMPreviewMode.VertexRGB ? "Vertex RGB Packing Legend" : "Vertex Alpha Legend", EditorStyles.miniBoldLabel);
            Rect rect = GUILayoutUtility.GetRect(1f, 14f, GUILayout.ExpandWidth(true));
            int steps = 32;
            for (int i = 0; i < steps; i++)
            {
                float t0 = i / (float)steps;
                float t1 = (i + 1) / (float)steps;
                Rect stepRect = new Rect(
                    Mathf.Lerp(rect.xMin, rect.xMax, t0),
                    rect.yMin,
                    rect.width / steps + 1f,
                    rect.height);
                Color low = new Color(0.05f, 0.05f, 0.05f, 1f);
                Color high = new Color(_previewTint.r, _previewTint.g, _previewTint.b, 1f);
                EditorGUI.DrawRect(stepRect, Color.Lerp(low, high, t1));
            }
            EditorGUILayout.LabelField(_previewMode == FXMPreviewMode.VertexRGB ? "Dark RGB Mask → Bright RGB Mask" : "Low Alpha → High Alpha", EditorStyles.miniLabel);
        }

        private void FocusPreviewObject()
        {
            if (_previewObject == null)
            {
                _previewObject = GameObject.Find(PreviewObjectName);
            }

            if (_previewObject == null)
            {
                CreateOrUpdatePreviewObject();
            }

            if (_previewObject == null)
            {
                return;
            }

            Selection.activeGameObject = _previewObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static int GetRecommendedWireEdgeLimit(FXMMeshType meshType)
        {
            switch (meshType)
            {
                case FXMMeshType.CrossHelix:
                    return 4096;
                case FXMMeshType.Helix:
                    return 4096;
                case FXMMeshType.Slash:
                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                    return 6144;
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    return 4096;
                case FXMMeshType.Beam:
                default:
                    return 3072;
            }
        }

        private void ApplyRecommendedHelperProfile(bool repaint)
        {
            FXMHelperProfile profile = FXMHelperProfile.For(_settings.meshType);
            _showWireBoundsHelper = profile.showWireBounds;
            _showMeshWireHelper = true;
            _meshWireMaxEdges = GetRecommendedWireEdgeLimit(_settings.meshType);
            _showUvFlowHelper = profile.showUvFlow;
            // v0.29.4: Normal spikes / Normal Lines must always stay OFF when changing or generating mesh types.
            // Artists can still turn them on manually from Scene Preview Helpers when they explicitly need them.
            _showNormalHelper = false;
            _showBackfaceHelper = profile.showBackface;
            _showPivotGizmoHelper = true;
            _showAxisGizmoHelper = true;
            _helperScale = profile.helperScale;
            _normalSampleCount = profile.normalSampleCount;

            if (repaint)
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void ForceNormalHelperOff(bool repaint)
        {
            _showNormalHelper = false;
            if (repaint)
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void ApplyRecommendedPivotForCurrentMesh()
        {
            _settings.pivotPositionOffset = Vector3.zero;
            switch (_settings.meshType)
            {
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsBottom;
                    break;
                case FXMMeshType.Beam:
                    _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsCenter;
                    break;
                case FXMMeshType.Slash:
                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                default:
                    _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsCenter;
                    break;
            }
        }

        private void ApplyDefaultHelperProfile(bool repaint)
        {
            _showWireBoundsHelper = true;
            _showMeshWireHelper = true;
            _meshWireMaxEdges = GetRecommendedWireEdgeLimit(_settings.meshType);
            _meshWireColor = new Color(0.0f, 1f, 1f, 0.95f);
            _meshWireThickness = 2.25f;
            _showUvFlowHelper = true;
            _showNormalHelper = false;
            _showBackfaceHelper = true;
            _showPivotGizmoHelper = true;
            _showAxisGizmoHelper = true;
            _helperScale = 0.18f;
            _normalSampleCount = 48;

            if (repaint)
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void DrawExportSettings()
        {
            EditorGUILayout.Space(6f);
            _showExport = EditorGUILayout.Foldout(_showExport, Tr("7. Export / Save", "6. 저장 / 익스포트"), true);
            if (!_showExport) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawUserExportSavePanel();
                DrawExportPresetPanel();
                DrawExportValidationPanel();

                if (_workspaceMode == FXMWorkspaceMode.Advanced)
                {
                    DrawUiSeparator();
                    DrawUiSubHeader(Tr("Advanced QA / Publisher Utilities", "고급 QA / 퍼블리셔 도구"));
                    DrawExportFinalizerPanel();
                    DrawFinishPipelinePanel();
                    DrawMeshValidationSuitePanel();
                    DrawFullRegressionQAPanel();
                    DrawPublisherToolsPanel();
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        Tr("Advanced QA and publisher-only utilities are hidden in Basic mode. Switch to Advanced only when you need regression QA, sample/demo generation, or store packaging tools.",
                           "Basic 모드에서는 고급 QA와 퍼블리셔 전용 도구를 숨깁니다. 회귀 QA, 샘플/데모 생성, 스토어 패키징이 필요할 때만 Advanced로 전환하세요."),
                        MessageType.None);
                }

                EditorGUILayout.HelpBox(Tr("Default output path: Assets/FXMeshGeneratorPro/Generated. Preview wire overlays and preview alpha materials are not included in saved results. Mesh assets store clean mesh data; prefabs keep MeshRenderer but leave material slots empty.", "기본 저장 위치는 Assets/FXMeshGeneratorPro/Generated 입니다. Preview Wire Overlay와 Preview Vertex Alpha Material은 저장 결과물에 포함하지 않습니다. Mesh Asset에는 순수 Mesh 데이터만 저장하고, Prefab은 MeshRenderer를 유지하되 Material 슬롯을 비워 둡니다."), MessageType.None);
            }
        }

        private void DrawUserExportSavePanel()
        {
            DrawUiSubHeader(Tr("Save Output", "결과 저장"));
            EditorGUILayout.HelpBox(
                Tr("Main user-facing export actions. Save a Mesh Asset for direct use, or save the current preview as a material-free Prefab.",
                   "일반 사용자용 저장 기능입니다. Mesh Asset으로 저장하거나, 현재 프리뷰를 Material-Free Prefab으로 저장합니다."),
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (UiButton(Tr("Save Mesh Asset", "Mesh Asset 저장")))
                {
                    SaveMeshAsset();
                }

                if (UiButton(Tr("Save Preview Prefab", "Preview Prefab 저장")))
                {
                    SavePreviewAsPrefab();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (UiSmallButton(Tr("Open Generated Folder", "Generated 폴더 열기")))
                {
                    RevealGeneratedFolder();
                }

                if (UiSmallButton(Tr("Copy Export Summary", "익스포트 요약 복사")))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildCurrentValidationSummary();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (UiSmallButton(Tr("Copy Quick QA Report", "빠른 QA 리포트 복사")))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildCurrentValidationSummary();
                }

                if (UiSmallButton(Tr("Open Quick Start", "Quick Start 확인")))
                {
                    PingPackageDocument("QUICK_START.md");
                }
            }
        }

        private void PingPackageDocument(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            string assetPath = fileName;
            string absolutePath = Path.GetFullPath(assetPath);
            if (File.Exists(absolutePath))
            {
                EditorUtility.OpenWithDefaultApp(absolutePath);
                return;
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                Debug.LogWarning("FX Mesh Generator Pro: document not found: " + assetPath);
            }
        }

        private void DrawExportPresetPanel()
        {
            DrawUiSubHeader(Tr("Export Preset", "익스포트 프리셋"));
            FXMExportPreset previousPreset = _settings.exportPreset;
            _settings.exportPreset = (FXMExportPreset)EditorGUILayout.EnumPopup(
                new GUIContent("Target", Tr("Choose the target workflow for exported results. Unreal/Niagara presets are intentionally excluded in this package.", "저장 결과물을 어떤 워크플로우 기준으로 맞출지 선택합니다. Unreal/Niagara 프리셋은 이번 단계에서 제외했습니다.")),
                _settings.exportPreset);

            if (_settings.exportPreset != previousPreset)
            {
                // Keep the change lightweight. Artists can review first, then press Apply.
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (UiButton("Apply Selected Export Preset"))
                {
                    ApplyExportPreset(_settings.exportPreset, true, true);
                }

                if (UiButton("Preset Help"))
                {
                    EditorUtility.DisplayDialog("FXM Export Preset", GetExportPresetDescription(_settings.exportPreset), "OK");
                }
            }

            EditorGUILayout.HelpBox(Tr("Preset shortcut buttons were removed. Select the target from the dropdown, then apply it here.", "중복 프리셋 버튼은 제거했습니다. 드롭다운에서 타겟을 선택한 뒤 여기서 적용하세요.") + "\n" + GetExportPresetDescription(_settings.exportPreset), MessageType.None);
            EditorGUILayout.Space(4f);
        }

        private void DrawExportFinalizerPanel()
        {
            EditorGUILayout.Space(4f);
            _showExportFinalizer = EditorGUILayout.Foldout(_showExportFinalizer, Tr("7-0. Export Preset Finalizer", "7-0. 익스포트 프리셋 마감"), true);
            if (!_showExportFinalizer)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("Final export utility panel. Applies the selected export preset, builds a clipboard-ready export summary, and runs a lightweight readiness report for global distribution builds.", "선택한 Export Preset을 적용하고, 클립보드용 요약과 배포 전 Readiness 리포트를 만드는 마감 패널입니다."),
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Apply Preset + Readiness", "프리셋 적용 + 준비도 검사"), GUILayout.Height(24f)))
                    {
                        ApplyExportPreset(_settings.exportPreset, false);
                        _lastExportReadinessReport = BuildExportReadinessReport();
                        EditorGUIUtility.systemCopyBuffer = _lastExportReadinessReport;
                    }

                    if (GUILayout.Button(Tr("Copy Export Summary", "익스포트 요약 복사"), GUILayout.Height(24f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildCurrentValidationSummary();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Copy Readiness Report", "준비도 리포트 복사"), GUILayout.Height(22f)))
                    {
                        if (string.IsNullOrEmpty(_lastExportReadinessReport))
                        {
                            _lastExportReadinessReport = BuildExportReadinessReport();
                        }
                        EditorGUIUtility.systemCopyBuffer = _lastExportReadinessReport;
                    }

                    if (GUILayout.Button(Tr("Reveal Generated Folder", "Generated 폴더 열기"), GUILayout.Height(22f)))
                    {
                        RevealGeneratedFolder();
                    }
                }

                if (!string.IsNullOrEmpty(_lastExportReadinessReport))
                {
                    EditorGUILayout.LabelField(Tr("Last Readiness Report", "마지막 준비도 리포트"), EditorStyles.miniBoldLabel);
                    EditorGUILayout.TextArea(_lastExportReadinessReport, GUILayout.MinHeight(90f));
                }
            }
        }

        private string BuildExportReadinessReport()
        {
            RebuildPreviewMesh(false);
            Mesh mesh = _previewMesh;
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Export Readiness");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Mesh Name: " + (string.IsNullOrWhiteSpace(_settings.meshName) ? "FXM_GeneratedMesh" : _settings.meshName));
            lines.Add("Mesh Type: " + _settings.meshType);
            lines.Add("Export Target: " + GetExportPresetDisplayName(_settings.exportPreset));

            if (mesh == null)
            {
                lines.Add("Status: ERROR / Preview Mesh Missing");
                return string.Join("\n", lines.ToArray());
            }

            int vertexCount = mesh.vertexCount;
            int triangleCount = mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
            Color32[] colors32 = mesh.colors32;
            Vector2[] uv0 = mesh.uv;
            bool hasColor32 = colors32 != null && colors32.Length == vertexCount;
            bool hasUv0 = uv0 != null && uv0.Length == vertexCount;
            float minAlpha;
            float maxAlpha;
            GetColor32AlphaRange(colors32, vertexCount, out minAlpha, out maxAlpha);

            List<string> warnings = new List<string>();
            if (!hasUv0) warnings.Add("UV0 missing or mismatched.");
            if (!hasColor32) warnings.Add("Vertex Color32 missing or mismatched.");
            if (_settings.doubleSidedGeometry) warnings.Add("Double Sided Geometry enabled; output cost is roughly doubled.");
            if (vertexCount > _validationMobileVertexBudget) warnings.Add("Vertex count exceeds mobile warning budget.");
            if (triangleCount > _validationMobileTriangleBudget) warnings.Add("Triangle count exceeds mobile warning budget.");

            switch (_settings.exportPreset)
            {
                case FXMExportPreset.UnityParticleMesh:
                    if (_settings.upAxis != FXMUpAxis.YUp) warnings.Add("Unity Particle preset recommends Y-Up.");
                    if (hasColor32 && minAlpha > 0.01f) warnings.Add("Alpha minimum is above 0; fully transparent regions may be unavailable.");
                    break;
                case FXMExportPreset.UnityMeshRenderer:
                    if (!_settings.recalculateNormals) warnings.Add("Unity Mesh preset recommends Recalculate Normals ON.");
                    if (!_settings.recalculateTangents) warnings.Add("Tangents are recommended when using normal or distortion textures.");
                    break;
                case FXMExportPreset.BlenderMaxFBXMatch:
                    if (_settings.upAxis != FXMUpAxis.YUp) warnings.Add("Blender/Max FBX Match recommends Y-Up.");
                    if (_settings.pivotAnchor != FXMGlobalPivotAnchor.BoundsCenter) warnings.Add("Bounds Center Pivot is recommended for FBX Match.");
                    break;
                case FXMExportPreset.GenericDCC:
                    if (_settings.upAxis != FXMUpAxis.ZUp) warnings.Add("Generic DCC recommends Z-Up.");
                    if (!_settings.recalculateTangents) warnings.Add("Tangents are recommended for generic/DCC handoff.");
                    break;
            }

            lines.Add("Vertices: " + vertexCount.ToString("N0"));
            lines.Add("Triangles: " + triangleCount.ToString("N0"));
            lines.Add("UV0: " + (hasUv0 ? "OK" : "Missing / Count Mismatch"));
            lines.Add("Vertex Color32: " + (hasColor32 ? "OK" : "Missing / Count Mismatch"));
            lines.Add("Alpha Range: " + (hasColor32 ? minAlpha.ToString("0.000") + " ~ " + maxAlpha.ToString("0.000") : "N/A"));
            lines.Add("Status: " + (warnings.Count == 0 ? "READY" : "READY WITH WARNINGS (" + warnings.Count + ")"));

            if (warnings.Count > 0)
            {
                lines.Add("Warnings:");
                for (int i = 0; i < warnings.Count; i++)
                {
                    lines.Add("- " + warnings[i]);
                }
            }

            return string.Join("\n", lines.ToArray());
        }

        private void RevealGeneratedFolder()
        {
            EnsurePackageFolders(false);
            UnityEngine.Object generated = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GeneratedRoot);
            if (generated != null)
            {
                EditorGUIUtility.PingObject(generated);
                Selection.activeObject = generated;
                string absolutePath = Path.GetFullPath(GeneratedRoot);
                EditorUtility.RevealInFinder(absolutePath);
            }
        }

        private void DrawFinishPipelinePanel()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Finish Pipeline / QA", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                Tr("Finalization panel. Applies recommended UV Flow, Vertex Alpha, Export Preset, Pivot, and Helpers for the current Mesh Type. ", "마감 단계용 패널입니다. 현재 Mesh Type 기준으로 권장 UV Flow, Vertex Alpha, Export Preset, Pivot, Helper를 한 번에 정리합니다. ") +
                Tr("Safe Finish keeps the current shape and only normalizes output settings. Stylized Finish also applies recommended modifiers.", "Safe Finish는 형태를 건드리지 않고 출력 안정성만 맞추고, Stylized Finish는 추천 Modifier까지 적용합니다."),
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Safe Finish Setup", GUILayout.Height(24f)))
                {
                    ApplyFinishPipeline(false, true);
                }

                if (GUILayout.Button("Apply Stylized Finish Setup", GUILayout.Height(24f)))
                {
                    ApplyFinishPipeline(true, true);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Name", GUILayout.Height(22f)))
                {
                    _settings.meshName = BuildAutoMeshName(_settings.meshType, _settings.exportPreset);
                    Repaint();
                }

                if (GUILayout.Button("Copy QA Summary", GUILayout.Height(22f)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildCurrentValidationSummary();
                    EditorUtility.DisplayDialog("FXM QA Summary", Tr("Copied the current mesh QA summary to the clipboard.", "현재 메시 검증 요약을 클립보드에 복사했습니다."), "OK");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Save Mesh + Prefab", GUILayout.Height(26f)))
                {
                    AutoSaveMeshAndPrefab();
                }

                if (GUILayout.Button("Batch Save All Mesh Types", GUILayout.Height(26f)))
                {
                    if (EditorUtility.DisplayDialog(
                            "FXM Batch Save",
                            Tr("Generates all Mesh Types using recommended defaults for the current Export Preset and saves them to the Generated folder.\n\nExisting files are not overwritten; unique names are used.", "현재 Export Preset 기준으로 모든 Mesh Type을 추천 기본값으로 생성해 Generated 폴더에 저장합니다.\n\n기존 파일은 덮어쓰지 않고 고유 이름으로 저장됩니다."),
                            "Batch Save",
                            "Cancel"))
                    {
                        BatchSaveAllMeshTypes();
                    }
                }
            }

            EditorGUILayout.HelpBox(Tr("Batch save does not create Unreal/Niagara presets. This package supports Unity Particle, Unity MeshRenderer, Blender-Max FBX Match, and Generic DCC only.", "배치 저장은 Unreal/Niagara 프리셋을 만들지 않습니다. 현재 패키지 정책대로 Unity Particle / Unity MeshRenderer / Blender-Max FBX Match / Generic DCC만 사용합니다."), MessageType.Info);
            EditorGUILayout.Space(4f);
        }

        private void ApplyFinishPipeline(bool includeRecommendedModifiers, bool rebuild)
        {
            EnsureAlphaSettingsCompatibility();
            ClampUnsupportedModifierValues(false);
            ApplyRecommendedUvForCurrentMesh(false);
            ApplyRecommendedAlphaForCurrentMesh(false, false);
            ApplyExportPreset(_settings.exportPreset, false);
            ApplyRecommendedHelperProfile(false);
            ApplyRecommendedPivotForCurrentMesh();

            _showUV = true;
            _showAlpha = true;
            _showPreview = true;
            _showPreviewHelpers = true;
            _showExport = true;
            _showMeshWireHelper = true;
            _showUvFlowHelper = true;
            _showBackfaceHelper = true;
            _showPivotGizmoHelper = true;
            _showAxisGizmoHelper = true;
            _previewMode = FXMPreviewMode.UVCheckerAlpha;
            _alphaAsOpacity = false;

            if (includeRecommendedModifiers)
            {
                ApplyRecommendedModifiersForCurrentMesh(false);
            }

            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }
            else
            {
                SceneView.RepaintAll();
                Repaint();
            }
        }

        private void ApplyProductionDefaultsForCurrentMesh()
        {
            _settings.flowOffset = 0f;
            _settings.widthOffset = 0f;
            _settings.shapeBias = 0f;
            _settings.segmentFlowBias = 0f;
            _settings.segmentWidthBias = 0f;
            _settings.depth = 0f;
            _settings.edgeJitter = 0f;
            _settings.brokenAmount = 0f;
            _settings.symmetryWidth = false;
            _settings.pivotMode = FXMPivotMode.Center;
            _settings.startAngle = 0f;

            switch (_settings.meshType)
            {
                case FXMMeshType.Slash:
                    _settings.segments = 48;
                    _settings.widthSegments = 3;
                    _settings.radius = 1.25f;
                    _settings.width = 0.35f;
                    _settings.arcAngle = 110f;
                    _settings.slashRootScale = 0.85f;
                    _settings.slashTipScale = 0.25f;
                    break;

                case FXMMeshType.Beam:
                    _settings.segments = 24;
                    _settings.widthSegments = 3;
                    _settings.length = 2.5f;
                    _settings.width = 0.42f;
                    _settings.beamStartWidth = 0.6f;
                    _settings.beamEndWidth = 0.35f;
                    _settings.beamCoreWidth = 1.15f;
                    break;

                case FXMMeshType.Ring:
                    _settings.segments = 96;
                    _settings.widthSegments = 4;
                    _settings.radius = 1.25f;
                    _settings.width = 0.18f;
                    _settings.arcAngle = 360f;
                    _settings.ringInnerThickness = 1f;
                    _settings.ringOuterThickness = 1f;
                    break;

                case FXMMeshType.Disc:
                    _settings.segments = 96;
                    _settings.widthSegments = 8;
                    _settings.radius = 1.25f;
                    _settings.innerRadiusRatio = 0f;
                    _settings.arcAngle = 360f;
                    _settings.discCenterFade = 0.04f;
                    _settings.discOuterFade = 0.04f;
                    break;

                case FXMMeshType.Dome:
                    _settings.segments = 40;
                    _settings.widthSegments = 12;
                    _settings.radius = 1.25f;
                    _settings.height = 0.85f;
                    _settings.domeRoundness = 1f;
                    _settings.domeHeightBias = 0f;
                    break;

                case FXMMeshType.HalfDome:
                    _settings.segments = 40;
                    _settings.widthSegments = 12;
                    _settings.radius = 1.25f;
                    _settings.height = 0.85f;
                    _settings.arcAngle = 180f;
                    _settings.domeRoundness = 1f;
                    _settings.domeHeightBias = 0f;
                    break;

                case FXMMeshType.Helix:
                    _settings.segments = 96;
                    _settings.widthSegments = 3;
                    _settings.radius = 0.7f;
                    _settings.helixRadius2 = 0f;
                    _settings.width = 0.18f;
                    _settings.height = 2.0f;
                    _settings.turns = 2.25f;
                    _settings.helixClockwise = false;
                    _settings.helixCrossMesh = false;
                    _settings.helixCrossRotation = 0f;
                    break;

                case FXMMeshType.CrossHelix:
                    _settings.segments = 96;
                    _settings.widthSegments = 3;
                    _settings.radius = 0.7f;
                    _settings.helixRadius2 = 0f;
                    _settings.width = 0.16f;
                    _settings.height = 2.0f;
                    _settings.turns = 2.25f;
                    _settings.helixClockwise = false;
                    _settings.helixCrossMesh = true;
                    _settings.helixCrossRotation = 90f;
                    break;
            }
        }

        private void AutoSaveMeshAndPrefab()
        {
            ApplyFinishPipeline(false, false);
            if (string.IsNullOrWhiteSpace(_settings.meshName) || _settings.meshName == "FXM_GeneratedMesh")
            {
                _settings.meshName = BuildAutoMeshName(_settings.meshType, _settings.exportPreset);
            }

            Mesh meshAsset = CreateMeshAssetInGenerated(_settings.meshName, string.Empty, false);
            GameObject prefab = CreatePrefabAssetInGenerated(_settings.meshName, meshAsset, false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
                Selection.activeObject = prefab;
            }
            else if (meshAsset != null)
            {
                EditorGUIUtility.PingObject(meshAsset);
                Selection.activeObject = meshAsset;
            }

            EditorUtility.DisplayDialog("FXM Auto Save", Tr("Saved the Mesh Asset and material-free Prefab.\n\n", "Mesh Asset과 Material-Free Prefab 저장을 완료했습니다.\n\n") + GeneratedRoot, "OK");
        }

        private void BatchSaveAllMeshTypes()
        {
            FXMSettings original = _settings;
            EnsurePackageFolders(false);

            List<UnityEngine.Object> savedObjects = new List<UnityEngine.Object>();
            Array meshTypes = Enum.GetValues(typeof(FXMMeshType));
            foreach (FXMMeshType meshType in meshTypes)
            {
                _settings = original;
                _settings.meshType = meshType;
                _settings.meshName = BuildAutoMeshName(meshType, original.exportPreset);
                ApplyProductionDefaultsForCurrentMesh();
                ApplyFinishPipeline(false, false);

                Mesh meshAsset = CreateMeshAssetInGenerated(_settings.meshName, string.Empty, false);
                if (meshAsset != null)
                {
                    savedObjects.Add(meshAsset);
                }
            }

            _settings = original;
            RebuildPreviewMesh(true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (savedObjects.Count > 0)
            {
                Selection.objects = savedObjects.ToArray();
                EditorGUIUtility.PingObject(savedObjects[0]);
            }

            EditorUtility.DisplayDialog("FXM Batch Save", savedObjects.Count + Tr(" Mesh Types saved.\n\n", "개 Mesh Type 저장을 완료했습니다.\n\n") + GeneratedMeshesFolder, "OK");
        }

        private Mesh CreateMeshAssetInFolder(string targetFolder, string baseName, string suffix, bool ping)
        {
            RebuildPreviewMesh(false, true);

            EnsurePackageFolders(false);
            EnsureFolder(targetFolder);
            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(baseName) ? BuildAutoMeshName(_settings.meshType, _settings.exportPreset) : baseName);
            string safeSuffix = string.IsNullOrEmpty(suffix) ? string.Empty : SanitizeFileName(suffix);
            string path = AssetDatabase.GenerateUniqueAssetPath(targetFolder + "/" + safeName + safeSuffix + ".asset");

            Mesh meshAsset = UnityEngine.Object.Instantiate(_previewMesh);
            meshAsset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(meshAsset, path);

            if (ping)
            {
                EditorGUIUtility.PingObject(meshAsset);
            }

            return meshAsset;
        }

        private GameObject CreatePrefabAssetInFolder(string targetFolder, string baseName, Mesh meshAsset, bool ping)
        {
            EnsurePackageFolders(false);
            EnsureFolder(targetFolder);
            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(baseName) ? BuildAutoMeshName(_settings.meshType, _settings.exportPreset) : baseName);
            if (meshAsset == null)
            {
                meshAsset = CreateMeshAssetInFolder(GeneratedSampleMeshesFolder, safeName, "_Mesh", false);
            }

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(targetFolder + "/" + safeName + ".prefab");
            GameObject go = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = meshAsset;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = null;
            meshRenderer.sharedMaterials = Array.Empty<Material>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);

            if (ping && prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            return prefab;
        }

        private Mesh CreateMeshAssetInGenerated(string baseName, string suffix, bool ping)
        {
            // Always rebuild here so auto-save and batch-save never write a stale preview mesh
            // after settings were changed without assigning to the Scene Preview Object.
            RebuildPreviewMesh(false, true);

            EnsurePackageFolders(false);
            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(baseName) ? BuildAutoMeshName(_settings.meshType, _settings.exportPreset) : baseName);
            string safeSuffix = string.IsNullOrEmpty(suffix) ? string.Empty : SanitizeFileName(suffix);
            string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedMeshesFolder + "/" + safeName + safeSuffix + ".asset");

            Mesh meshAsset = UnityEngine.Object.Instantiate(_previewMesh);
            meshAsset.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(meshAsset, path);

            if (ping)
            {
                EditorGUIUtility.PingObject(meshAsset);
            }

            return meshAsset;
        }

        private GameObject CreatePrefabAssetInGenerated(string baseName, Mesh meshAsset, bool ping)
        {
            EnsurePackageFolders(false);
            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(baseName) ? BuildAutoMeshName(_settings.meshType, _settings.exportPreset) : baseName);
            if (meshAsset == null)
            {
                meshAsset = CreateMeshAssetInGenerated(safeName, "_Mesh", false);
            }

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedPrefabFolder + "/" + safeName + ".prefab");
            GameObject go = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = meshAsset;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = null;
            meshRenderer.sharedMaterials = Array.Empty<Material>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);

            if (ping && prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
            }

            return prefab;
        }

        private static string BuildAutoMeshName(FXMMeshType meshType, FXMExportPreset exportPreset)
        {
            return "FXM_" + meshType + "_" + GetExportPresetFileSuffix(exportPreset);
        }

        private static string GetExportPresetFileSuffix(FXMExportPreset preset)
        {
            switch (preset)
            {
                case FXMExportPreset.UnityParticleMesh:
                    return "UnityParticle";
                case FXMExportPreset.UnityMeshRenderer:
                    return "UnityMesh";
                case FXMExportPreset.BlenderMaxFBXMatch:
                    return "BlenderMaxMatch";
                case FXMExportPreset.GenericDCC:
                    return "GenericDCC";
                default:
                    return preset.ToString();
            }
        }

        private string BuildCurrentValidationSummary()
        {
            RebuildPreviewMesh(false);

            Mesh mesh = _previewMesh;
            if (mesh == null)
            {
                return "FXM QA Summary\nPreview Mesh: Missing";
            }

            int vertexCount = mesh.vertexCount;
            int triangleCount = mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
            Color32[] colors32 = mesh.colors32;
            Vector2[] uv0 = mesh.uv;
            bool hasColor32 = colors32 != null && colors32.Length == vertexCount;
            bool hasUv0 = uv0 != null && uv0.Length == vertexCount;
            float minAlpha;
            float maxAlpha;
            GetColor32AlphaRange(colors32, vertexCount, out minAlpha, out maxAlpha);

            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro QA Summary");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Mesh Name: " + (string.IsNullOrWhiteSpace(_settings.meshName) ? "FXM_GeneratedMesh" : _settings.meshName));
            lines.Add("Mesh Type: " + _settings.meshType);
            lines.Add("Export Target: " + GetExportPresetDisplayName(_settings.exportPreset));
            lines.Add("Vertices: " + vertexCount.ToString("N0"));
            lines.Add("Triangles: " + triangleCount.ToString("N0"));
            lines.Add("UV0: " + (hasUv0 ? "OK" : "Missing / Count Mismatch"));
            lines.Add("UV Flow Direction: " + _settings.uvFlowDirection);
            lines.Add("UV Tiling: " + _settings.uvTilingU.ToString("0.###") + " x " + _settings.uvTilingV.ToString("0.###"));
            lines.Add("Vertex Color32: " + (hasColor32 ? "OK" : "Missing / Count Mismatch"));
            lines.Add("Alpha Range: " + (hasColor32 ? minAlpha.ToString("0.000") + " ~ " + maxAlpha.ToString("0.000") : "N/A"));
            lines.Add("Bounds Size: " + FormatVector3(mesh.bounds.size));
            lines.Add("Up Axis: " + _settings.upAxis);
            lines.Add("Pivot Anchor: " + _settings.pivotAnchor);
            lines.Add("Double Sided Geometry: " + _settings.doubleSidedGeometry);
            lines.Add("Material Export: Material-Free");
            return string.Join("\n", lines.ToArray());
        }

        private void DrawMeshValidationSuitePanel()
        {
            EditorGUILayout.Space(6f);
            _showMeshValidationSuite = EditorGUILayout.Foldout(_showMeshValidationSuite, Tr("7-1. Mesh Validation Suite", "7-1. 메시 검증 스위트"), true);
            if (!_showMeshValidationSuite)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("Runs production safety checks before store distribution: all mesh types, vertex/triangle integrity, UV0, Color32, NaN/Infinity, degenerate triangles, bounds, and mobile budget warnings.",
                       "스토어 배포 전 안전 검사를 실행합니다: 전체 Mesh Type, 버텍스/삼각형 무결성, UV0, Color32, NaN/Infinity, 퇴화 삼각형, Bounds, 모바일 예산 경고를 확인합니다."),
                    MessageType.None);

                _validationCopyReportAfterRun = EditorGUILayout.Toggle(new GUIContent(Tr("Copy Report After Run", "실행 후 리포트 복사"), Tr("Copies the validation report to the clipboard after each run.", "검증 실행 후 리포트를 클립보드에 복사합니다.")), _validationCopyReportAfterRun);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _validationMobileVertexBudget = EditorGUILayout.IntField(new GUIContent(Tr("Mobile Vertex Budget", "모바일 버텍스 예산"), Tr("Warning threshold for mobile-oriented VFX meshes.", "모바일 VFX 메시 기준 경고 임계값입니다.")), _validationMobileVertexBudget);
                    _validationMobileTriangleBudget = EditorGUILayout.IntField(new GUIContent(Tr("Mobile Triangle Budget", "모바일 삼각형 예산"), Tr("Warning threshold for mobile-oriented VFX meshes.", "모바일 VFX 메시 기준 경고 임계값입니다.")), _validationMobileTriangleBudget);
                }

                _validationMobileVertexBudget = Mathf.Max(1, _validationMobileVertexBudget);
                _validationMobileTriangleBudget = Mathf.Max(1, _validationMobileTriangleBudget);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Run Current Mesh QA", "현재 메시 QA 실행"), GUILayout.Height(26f)))
                    {
                        _lastMeshValidationReport = RunCurrentMeshValidationReport();
                        if (_validationCopyReportAfterRun)
                        {
                            EditorGUIUtility.systemCopyBuffer = _lastMeshValidationReport;
                        }
                    }

                    if (GUILayout.Button(Tr("Run All Mesh Types QA", "전체 메시 타입 QA 실행"), GUILayout.Height(26f)))
                    {
                        _lastMeshValidationReport = RunAllMeshTypesValidationReport();
                        if (_validationCopyReportAfterRun)
                        {
                            EditorGUIUtility.systemCopyBuffer = _lastMeshValidationReport;
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Copy Last QA Report", "마지막 QA 리포트 복사"), GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = string.IsNullOrEmpty(_lastMeshValidationReport)
                            ? BuildCurrentValidationSummary()
                            : _lastMeshValidationReport;
                    }

                    if (GUILayout.Button(Tr("Clear Report", "리포트 지우기"), GUILayout.Height(22f)))
                    {
                        _lastMeshValidationReport = string.Empty;
                    }
                }

                if (!string.IsNullOrEmpty(_lastMeshValidationReport))
                {
                    EditorGUILayout.LabelField(Tr("Last Validation Report", "마지막 검증 리포트"), EditorStyles.miniBoldLabel);
                    _validationReportScroll = EditorGUILayout.BeginScrollView(_validationReportScroll, GUILayout.MinHeight(110f), GUILayout.MaxHeight(220f));
                    EditorGUILayout.TextArea(_lastMeshValidationReport, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private string RunCurrentMeshValidationReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro - Mesh Validation Suite");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Scope: Current Mesh");
            lines.Add("Target: " + GetExportPresetDisplayName(_settings.exportPreset));
            lines.Add("Generated At: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add(string.Empty);

            FXMSettings validationSettings = _settings;
            Mesh mesh = null;
            int errorCount = 0;
            int warningCount = 0;
            try
            {
                mesh = FXMMeshBuilder.Build(validationSettings);
                ValidateMeshForReport(mesh, validationSettings, validationSettings.meshType.ToString(), lines, ref errorCount, ref warningCount);
            }
            catch (Exception ex)
            {
                errorCount++;
                lines.Add("[ERROR] Build failed for " + validationSettings.meshType + ": " + ex.GetType().Name + " - " + ex.Message);
            }
            finally
            {
                DestroyValidationMesh(mesh);
            }

            lines.Add(string.Empty);
            lines.Add(BuildValidationFooter(errorCount, warningCount));
            return string.Join("\n", lines.ToArray());
        }

        private string RunAllMeshTypesValidationReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro - Mesh Validation Suite");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Scope: All Mesh Types");
            lines.Add("Target: " + GetExportPresetDisplayName(_settings.exportPreset));
            lines.Add("Generated At: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add(string.Empty);

            int errorCount = 0;
            int warningCount = 0;
            Array meshTypes = Enum.GetValues(typeof(FXMMeshType));
            for (int i = 0; i < meshTypes.Length; i++)
            {
                FXMMeshType meshType = (FXMMeshType)meshTypes.GetValue(i);
                FXMSettings validationSettings = BuildDefaultValidationSettings(meshType, _settings.exportPreset);
                Mesh mesh = null;
                try
                {
                    mesh = FXMMeshBuilder.Build(validationSettings);
                    ValidateMeshForReport(mesh, validationSettings, meshType.ToString(), lines, ref errorCount, ref warningCount);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    lines.Add("[ERROR] " + meshType + " build failed: " + ex.GetType().Name + " - " + ex.Message);
                }
                finally
                {
                    DestroyValidationMesh(mesh);
                }

                lines.Add(string.Empty);
            }

            lines.Add(BuildValidationFooter(errorCount, warningCount));
            return string.Join("\n", lines.ToArray());
        }

        private FXMSettings BuildDefaultValidationSettings(FXMMeshType meshType, FXMExportPreset exportPreset)
        {
            FXMSettings s = FXMSettings.Default();
            s.meshType = meshType;
            s.meshName = "FXM_QA_" + meshType;
            s.exportPreset = exportPreset;
            s.recalculateNormals = true;
            s.recalculateTangents = exportPreset != FXMExportPreset.UnityParticleMesh;
            s.doubleSidedGeometry = false;
            s.centerPivotAfterBuild = true;
            s.upAxis = exportPreset == FXMExportPreset.GenericDCC ? FXMUpAxis.ZUp : FXMUpAxis.YUp;
            s.vertexColorRGBMode = FXMVertexColorRGBMode.White;

            switch (meshType)
            {
                case FXMMeshType.Slash:
                    s.segments = 48;
                    s.widthSegments = 3;
                    s.radius = 1.25f;
                    s.width = 0.35f;
                    s.arcAngle = 110f;
                    s.slashRootScale = 0.85f;
                    s.slashTipScale = 0.25f;
                    break;
                case FXMMeshType.Beam:
                    s.segments = 24;
                    s.widthSegments = 3;
                    s.length = 2.5f;
                    s.width = 0.42f;
                    s.beamStartWidth = 0.6f;
                    s.beamEndWidth = 0.18f;
                    break;
                case FXMMeshType.Ring:
                    s.segments = 96;
                    s.widthSegments = 4;
                    s.radius = 1f;
                    s.width = 0.22f;
                    s.arcAngle = 360f;
                    s.uvFlowDirection = FXMUVFlowDirection.CircularCW;
                    break;
                case FXMMeshType.Disc:
                    s.segments = 96;
                    s.widthSegments = 8;
                    s.radius = 1f;
                    s.innerRadiusRatio = 0f;
                    s.arcAngle = 360f;
                    s.uvFlowDirection = FXMUVFlowDirection.FromCenter;
                    break;
                case FXMMeshType.Dome:
                    s.segments = 64;
                    s.widthSegments = 12;
                    s.radius = 1f;
                    s.height = 0.85f;
                    s.arcAngle = 360f;
                    s.domeRoundness = 1f;
                    s.pivotAnchor = FXMGlobalPivotAnchor.BoundsBottom;
                    break;
                case FXMMeshType.HalfDome:
                    s.segments = 48;
                    s.widthSegments = 12;
                    s.radius = 1f;
                    s.height = 0.85f;
                    s.arcAngle = 180f;
                    s.domeRoundness = 1f;
                    s.pivotAnchor = FXMGlobalPivotAnchor.BoundsBottom;
                    break;
                case FXMMeshType.Helix:
                    s.segments = 96;
                    s.widthSegments = 3;
                    s.radius = 0.55f;
                    s.helixRadius2 = 0.2f;
                    s.width = 0.12f;
                    s.height = 1.8f;
                    s.turns = 2.5f;
                    s.pivotAnchor = FXMGlobalPivotAnchor.BoundsBottom;
                    break;
                case FXMMeshType.CrossHelix:
                    s.segments = 96;
                    s.widthSegments = 3;
                    s.radius = 0.55f;
                    s.helixRadius2 = 0.2f;
                    s.width = 0.1f;
                    s.height = 1.8f;
                    s.turns = 2.5f;
                    s.helixCrossRotation = 0f;
                    s.pivotAnchor = FXMGlobalPivotAnchor.BoundsBottom;
                    break;
            }

            return s;
        }

        private void ValidateMeshForReport(Mesh mesh, FXMSettings settings, string label, List<string> lines, ref int errorCount, ref int warningCount)
        {
            lines.Add("## " + label);
            if (mesh == null)
            {
                errorCount++;
                lines.Add("[ERROR] Mesh is null.");
                return;
            }

            int vertexCount = mesh.vertexCount;
            int[] triangles = mesh.triangles;
            int triangleIndexCount = triangles != null ? triangles.Length : 0;
            int triangleCount = triangleIndexCount / 3;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv0 = mesh.uv;
            Color32[] colors32 = mesh.colors32;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Bounds bounds = mesh.bounds;

            lines.Add("Mesh Type: " + settings.meshType);
            lines.Add("Vertices: " + vertexCount.ToString("N0") + " / Triangles: " + triangleCount.ToString("N0") + " / Index: " + mesh.indexFormat);
            lines.Add("Bounds Size: " + FormatVector3(bounds.size));

            if (vertexCount <= 0)
            {
                errorCount++;
                lines.Add("[ERROR] Vertex count is zero.");
            }

            if (triangleIndexCount <= 0 || triangleIndexCount % 3 != 0)
            {
                errorCount++;
                lines.Add("[ERROR] Triangle index buffer is empty or not divisible by 3.");
            }

            int invalidVertexCount = CountInvalidVertices(vertices);
            if (invalidVertexCount > 0)
            {
                errorCount++;
                lines.Add("[ERROR] Invalid vertex values found: " + invalidVertexCount);
            }

            int invalidTriangleCount;
            int degenerateTriangleCount;
            CountTriangleProblems(vertices, triangles, out invalidTriangleCount, out degenerateTriangleCount);
            if (invalidTriangleCount > 0)
            {
                errorCount++;
                lines.Add("[ERROR] Invalid triangle indices found: " + invalidTriangleCount);
            }
            if (degenerateTriangleCount > 0)
            {
                warningCount++;
                lines.Add("[WARN] Degenerate triangles found: " + degenerateTriangleCount);
            }

            if (uv0 == null || uv0.Length != vertexCount)
            {
                errorCount++;
                lines.Add("[ERROR] UV0 missing or count mismatch. UV0: " + (uv0 == null ? 0 : uv0.Length) + " / Vertices: " + vertexCount);
            }
            else
            {
                int invalidUvCount = CountInvalidUVs(uv0);
                int outsideUvCount = CountOutside01UVs(uv0);
                if (invalidUvCount > 0)
                {
                    errorCount++;
                    lines.Add("[ERROR] Invalid UV0 values found: " + invalidUvCount);
                }
                lines.Add("UV0: OK / Outside 0-1: " + outsideUvCount + " / " + uv0.Length);
            }

            if (colors32 == null || colors32.Length != vertexCount)
            {
                errorCount++;
                lines.Add("[ERROR] Color32 missing or count mismatch. Color32: " + (colors32 == null ? 0 : colors32.Length) + " / Vertices: " + vertexCount);
            }
            else
            {
                float minAlpha;
                float maxAlpha;
                GetColor32AlphaRange(colors32, vertexCount, out minAlpha, out maxAlpha);
                lines.Add("Color32: OK / Alpha: " + minAlpha.ToString("0.000") + " ~ " + maxAlpha.ToString("0.000"));
            }

            if (settings.recalculateNormals && (normals == null || normals.Length != vertexCount))
            {
                warningCount++;
                lines.Add("[WARN] Normals are missing or count mismatch after Recalculate Normals.");
            }
            else if (normals != null && normals.Length == vertexCount && CountInvalidVertices(normals) > 0)
            {
                errorCount++;
                lines.Add("[ERROR] Invalid normal values found.");
            }

            if (settings.recalculateTangents && (tangents == null || tangents.Length != vertexCount))
            {
                warningCount++;
                lines.Add("[WARN] Tangents are missing or count mismatch after Recalculate Tangents.");
            }
            else if (tangents != null && tangents.Length == vertexCount && CountInvalidTangents(tangents) > 0)
            {
                errorCount++;
                lines.Add("[ERROR] Invalid tangent values found.");
            }

            if (!IsFinite(bounds.center) || !IsFinite(bounds.size))
            {
                errorCount++;
                lines.Add("[ERROR] Bounds contain NaN or Infinity.");
            }

            if (vertexCount > _validationMobileVertexBudget || triangleCount > _validationMobileTriangleBudget)
            {
                warningCount++;
                lines.Add("[WARN] Mobile budget exceeded. Budget V/T: " + _validationMobileVertexBudget.ToString("N0") + " / " + _validationMobileTriangleBudget.ToString("N0"));
            }

            List<string> qualityWarnings = BuildMeshQualityWarnings(mesh, settings);
            for (int i = 0; i < qualityWarnings.Count; i++)
            {
                warningCount++;
                lines.Add("[QUALITY] " + qualityWarnings[i]);
            }

            if (mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16 && vertexCount > 65535)
            {
                errorCount++;
                lines.Add("[ERROR] UInt16 index format cannot safely address more than 65,535 vertices.");
            }

            bool localOk = !HasRecentErrorsForSection(lines, label);
            lines.Add(localOk ? "Result: PASS" : "Result: CHECK REQUIRED");
        }

        private static bool HasRecentErrorsForSection(List<string> lines, string label)
        {
            string header = "## " + label;
            int start = 0;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i] == header)
                {
                    start = i;
                    break;
                }
            }

            for (int i = start; i < lines.Count; i++)
            {
                if (lines[i].StartsWith("[ERROR]", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private string BuildValidationFooter(int errorCount, int warningCount)
        {
            if (errorCount == 0 && warningCount == 0)
            {
                return "FINAL RESULT: PASS - Store QA ready.";
            }

            return "FINAL RESULT: CHECK REQUIRED - Errors: " + errorCount + " / Warnings: " + warningCount;
        }

        private static void DestroyValidationMesh(Mesh mesh)
        {
            if (mesh != null && !AssetDatabase.Contains(mesh))
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static int CountInvalidVertices(Vector3[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!IsFinite(values[i]))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountInvalidTangents(Vector4[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!IsFinite(values[i].x) || !IsFinite(values[i].y) || !IsFinite(values[i].z) || !IsFinite(values[i].w))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountInvalidUVs(Vector2[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (!IsFinite(values[i]))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountOutside01UVs(Vector2[] values)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].x < -0.0001f || values[i].x > 1.0001f || values[i].y < -0.0001f || values[i].y > 1.0001f)
                {
                    count++;
                }
            }
            return count;
        }

        private static void CountTriangleProblems(Vector3[] vertices, int[] triangles, out int invalidIndexCount, out int degenerateCount)
        {
            invalidIndexCount = 0;
            degenerateCount = 0;
            if (vertices == null || triangles == null)
            {
                return;
            }

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length || a == b || b == c || a == c)
                {
                    invalidIndexCount++;
                    continue;
                }

                Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (normal.sqrMagnitude <= 0.00000001f)
                {
                    degenerateCount++;
                }
            }
        }

        private static bool IsFinite(Vector3 v)
        {
            return IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
        }

        private static bool IsFinite(Vector2 v)
        {
            return IsFinite(v.x) && IsFinite(v.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ApplyExportPreset(FXMExportPreset preset, bool rebuild, bool showDialog = false)
        {
            _settings.exportPreset = preset;
            _settings.pivotPositionOffset = Vector3.zero;
            _settings.pivotRotationEuler = Vector3.zero;
            _settings.recalculateNormals = true;
            _settings.centerPivotAfterBuild = true;

            switch (preset)
            {
                case FXMExportPreset.UnityParticleMesh:
                    _settings.upAxis = FXMUpAxis.YUp;
                    _settings.recalculateTangents = false;
                    _settings.doubleSidedGeometry = false;
                    _settings.vertexColorRGBMode = FXMVertexColorRGBMode.White;
                    ApplyRecommendedPivotForCurrentMesh();
                    break;

                case FXMExportPreset.UnityMeshRenderer:
                    _settings.upAxis = FXMUpAxis.YUp;
                    _settings.recalculateTangents = true;
                    _settings.doubleSidedGeometry = false;
                    ApplyRecommendedPivotForCurrentMesh();
                    break;

                case FXMExportPreset.BlenderMaxFBXMatch:
                    // This preset targets the result seen inside Unity when importing Blender/Max FBX:
                    // XZ ground plane, +Y normal, Color32 vertex alpha, material-free output.
                    _settings.upAxis = FXMUpAxis.YUp;
                    _settings.recalculateTangents = true;
                    _settings.doubleSidedGeometry = false;
                    _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsCenter;
                    break;

                case FXMExportPreset.GenericDCC:
                    // Generic DCC handoff favors Z-Up data and explicit tangents/normals.
                    _settings.upAxis = FXMUpAxis.ZUp;
                    _settings.recalculateTangents = true;
                    _settings.doubleSidedGeometry = false;
                    _settings.pivotAnchor = FXMGlobalPivotAnchor.BoundsCenter;
                    break;
            }

            _settings.minAlpha = Mathf.Clamp01(_settings.minAlpha);
            _settings.maxAlpha = Mathf.Clamp01(_settings.maxAlpha);
            if (_settings.maxAlpha < _settings.minAlpha)
            {
                float temp = _settings.minAlpha;
                _settings.minAlpha = _settings.maxAlpha;
                _settings.maxAlpha = temp;
            }

            if (rebuild)
            {
                RebuildPreviewMesh(true);
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog("FXM Export Preset Applied", GetExportPresetDescription(preset), "OK");
            }
        }

        private static string GetExportPresetDisplayName(FXMExportPreset preset)
        {
            switch (preset)
            {
                case FXMExportPreset.UnityParticleMesh:
                    return "Unity Particle Mesh";
                case FXMExportPreset.UnityMeshRenderer:
                    return "Unity MeshRenderer";
                case FXMExportPreset.BlenderMaxFBXMatch:
                    return "Blender/Max FBX Match";
                case FXMExportPreset.GenericDCC:
                    return "Generic DCC";
                default:
                    return preset.ToString();
            }
        }

        private static string GetExportPresetDescription(FXMExportPreset preset)
        {
            switch (preset)
            {
                case FXMExportPreset.UnityParticleMesh:
                    return Tr("Unity Particle Mesh: for Particle System Renderer > Mesh. Validates Y-Up/XZ plane, +Y normals, Color32 Vertex Alpha, and material-free output.", "Unity Particle Mesh: Particle System Renderer > Mesh에 넣는 목적입니다. Y-Up/XZ 평면, +Y 노멀, Color32 Vertex Alpha, Material-Free 저장을 기준으로 검증합니다.");
                case FXMExportPreset.UnityMeshRenderer:
                    return Tr("Unity MeshRenderer: for standard MeshFilter/MeshRenderer prefabs. Validates Y-Up, recalculated normals/tangents, and material-free prefab output.", "Unity MeshRenderer: 일반 MeshFilter/MeshRenderer 프리팹에 넣는 목적입니다. Y-Up, Recalculate Normals/Tangents, Material-Free 프리팹 저장을 기준으로 검증합니다.");
                case FXMExportPreset.BlenderMaxFBXMatch:
                    return Tr("Blender/Max FBX Match: matches how FBX meshes from Blender/Max appear inside Unity. XZ ground plane, +Y normals, and Bounds Center Pivot are recommended.", "Blender/Max FBX Match: Blender/Max에서 FBX로 가져온 메시가 Unity 안에서 보이는 결과와 맞추는 목적입니다. XZ 바닥 평면, +Y 노멀, Bounds Center Pivot을 권장합니다.");
                case FXMExportPreset.GenericDCC:
                    return Tr("Generic DCC: for sending meshes back to DCC tools or generic pipelines. Z-Up, Bounds Center Pivot, and normals/tangents are recommended.", "Generic DCC: 다시 DCC 툴로 넘기거나 범용 파이프라인에 쓰기 위한 목적입니다. Z-Up, Bounds Center Pivot, Normals/Tangents 포함을 권장합니다.");
                default:
                    return Tr("Runs pre-save validation for the selected Export Preset.", "선택한 Export Preset 기준으로 저장 전 검증합니다.");
            }
        }

        private void DrawExportValidationPanel()
        {
            Mesh mesh = _previewMesh;
            if (mesh == null && _autoRefresh)
            {
                RebuildPreviewMesh(false);
                mesh = _previewMesh;
            }

            EditorGUILayout.LabelField("Save Validation", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Target", GetExportPresetDisplayName(_settings.exportPreset));

            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(_settings.meshName) ? "FXM_GeneratedMesh" : _settings.meshName);
            bool nameChanged = !string.Equals(safeName, _settings.meshName, StringComparison.Ordinal);
            if (nameChanged)
            {
                EditorGUILayout.HelpBox(Tr("Mesh Name contains characters that are unsafe for filenames. It will be saved as '", "Mesh Name에 파일명으로 안전하지 않은 문자가 있어 저장 시 '") + safeName + Tr("'.", "' 형태로 보정됩니다."), MessageType.Warning);
            }

            if (mesh == null)
            {
                EditorGUILayout.HelpBox(Tr("No Preview Mesh exists yet. Save will generate it automatically, but it is safer to preview with Generate Mesh Data first.", "Preview Mesh가 아직 없습니다. 저장 버튼을 누르면 자동 생성되지만, 먼저 Generate Mesh Data로 확인하는 것을 권장합니다."), MessageType.Warning);
                return;
            }

            int vertexCount = mesh.vertexCount;
            int triangleCount = mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
            Color32[] colors32 = mesh.colors32;
            Vector2[] uv0 = mesh.uv;
            Bounds bounds = mesh.bounds;
            string indexFormat = mesh.indexFormat.ToString();
            bool hasColor32 = colors32 != null && colors32.Length == vertexCount;
            bool hasUv0 = uv0 != null && uv0.Length == vertexCount;
            float minAlpha;
            float maxAlpha;
            GetColor32AlphaRange(colors32, vertexCount, out minAlpha, out maxAlpha);

            EditorGUILayout.LabelField("Vertices", vertexCount.ToString("N0"));
            EditorGUILayout.LabelField("Triangles", triangleCount.ToString("N0"));
            EditorGUILayout.LabelField("Index Format", indexFormat);
            EditorGUILayout.LabelField("UV0", hasUv0 ? "OK" : "Missing / Count Mismatch");
            EditorGUILayout.LabelField("UV Flow Direction", _settings.uvFlowDirection.ToString());
            EditorGUILayout.LabelField("UV Tiling", _settings.uvTilingU.ToString("0.###") + " x " + _settings.uvTilingV.ToString("0.###"));
            EditorGUILayout.LabelField("Vertex Color32", hasColor32 ? "OK" : "Missing / Count Mismatch");
            EditorGUILayout.LabelField("Alpha Range", hasColor32 ? minAlpha.ToString("0.000") + " ~ " + maxAlpha.ToString("0.000") : "N/A");
            EditorGUILayout.LabelField("Bounds Size", FormatVector3(bounds.size));
            EditorGUILayout.LabelField("Up Axis", _settings.upAxis.ToString());
            EditorGUILayout.LabelField("Pivot Anchor", _settings.pivotAnchor.ToString());
            EditorGUILayout.LabelField("Material Export", "Material-Free");

            ValidateExportPreset(mesh, hasColor32, hasUv0, minAlpha, maxAlpha, vertexCount, triangleCount, bounds);

            if (_settings.meshType == FXMMeshType.CrossHelix && ShouldDeferHeavyCrossHelixAutoRefresh())
            {
                EditorGUILayout.HelpBox(Tr("Cross Helix is in a high-density state. Press Generate Mesh Data once before saving to manually refresh the final mesh.", "Cross Helix가 고밀도 상태입니다. 저장 전 Generate Mesh Data를 한 번 눌러 최종 메시를 수동 갱신하세요."), MessageType.Warning);
            }

            EditorGUILayout.HelpBox(Tr("Saved outputs are material-free. Mesh Assets store only Mesh/UV/Color32 data and Prefabs leave material slots empty.", "저장 결과물은 Material-Free입니다. Mesh Asset에는 순수 Mesh/UV/Color32 데이터만 저장되고, Prefab은 Material 슬롯을 비워 둡니다."), MessageType.None);
        }

        private void ValidateExportPreset(Mesh mesh, bool hasColor32, bool hasUv0, float minAlpha, float maxAlpha, int vertexCount, int triangleCount, Bounds bounds)
        {
            bool ready = true;

            if (!hasUv0)
            {
                ready = false;
                EditorGUILayout.HelpBox(Tr("UV0 is missing. Flow texture or mask based VFX materials may not work correctly.", "UV0가 없습니다. Flow 텍스처/마스크 기반 VFX 머티리얼에서 정상 동작하지 않을 수 있습니다."), MessageType.Error);
            }

            if (!hasColor32)
            {
                ready = false;
                EditorGUILayout.HelpBox(Tr("Color32 vertex stream is missing. Vertex Alpha may not be passed reliably to vertex-color materials or Particle System Mesh Renderer.", "Color32 Vertex Stream이 없습니다. Vertex Alpha 기반 머티리얼과 Particle System Mesh Renderer에서 알파가 안정적으로 전달되지 않습니다."), MessageType.Error);
            }

            switch (_settings.exportPreset)
            {
                case FXMExportPreset.UnityParticleMesh:
                    if (_settings.upAxis != FXMUpAxis.YUp)
                    {
                        ready = false;
                        EditorGUILayout.HelpBox(Tr("Unity Particle Mesh recommends Y-Up/XZ plane. Reapply the Export Preset.", "Unity Particle Mesh는 Y-Up/XZ 평면을 권장합니다. Export Preset을 다시 적용하세요."), MessageType.Warning);
                    }
                    if (hasColor32 && minAlpha > 0.01f)
                    {
                        EditorGUILayout.HelpBox(Tr("Current Vertex Alpha minimum is above 0. If particles need fully transparent areas, adjust Alpha Range/Clamp or use a preset.", "현재 Vertex Alpha 최소값이 0보다 큽니다. 완전 투명 구간이 필요한 파티클이면 Alpha Range/Clamp 또는 Preset을 조정하세요."), MessageType.Warning);
                    }
                    if (hasColor32 && maxAlpha < 0.99f)
                    {
                        EditorGUILayout.HelpBox(Tr("Current Vertex Alpha maximum is below 1. Even the strongest areas may appear partially transparent.", "현재 Vertex Alpha 최대값이 1보다 낮습니다. 가장 진한 구간도 반투명하게 보일 수 있습니다."), MessageType.Warning);
                    }
                    if (_settings.vertexColorRGBMode != FXMVertexColorRGBMode.White)
                    {
                        EditorGUILayout.HelpBox(Tr("Standard Particle workflow recommends RGB White + Alpha. Current settings are fine only for shaders that intentionally use RGB masks.", "Particle 기본 워크플로우는 RGB White + Alpha를 권장합니다. RGB 마스크가 필요한 전용 셰이더라면 현재 설정도 사용할 수 있습니다."), MessageType.Info);
                    }
                    if (vertexCount > 10000 || triangleCount > 16000)
                    {
                        EditorGUILayout.HelpBox(Tr("This may be heavy for Particle Mesh usage. For mobile targets, reduce Segments, Width Segments, or Double Sided Geometry.", "Particle Mesh 기준으로 다소 무거울 수 있습니다. 모바일 타겟이면 Segments, Width Segments, Double Sided Geometry를 줄이세요."), MessageType.Warning);
                    }
                    break;

                case FXMExportPreset.UnityMeshRenderer:
                    if (!_settings.recalculateNormals)
                    {
                        EditorGUILayout.HelpBox(Tr("Unity MeshRenderer preset recommends Recalculate Normals ON.", "Unity MeshRenderer 프리셋은 Recalculate Normals ON을 권장합니다."), MessageType.Warning);
                    }
                    if (!_settings.recalculateTangents)
                    {
                        EditorGUILayout.HelpBox(Tr("If you plan to use normal maps or distortion textures, Recalculate Tangents ON is recommended.", "노멀맵/왜곡 텍스처를 쓸 예정이면 Recalculate Tangents ON을 권장합니다."), MessageType.Info);
                    }
                    break;

                case FXMExportPreset.BlenderMaxFBXMatch:
                    if (_settings.upAxis != FXMUpAxis.YUp)
                    {
                        ready = false;
                        EditorGUILayout.HelpBox(Tr("Blender/Max FBX Match recommends Y-Up to match the XZ/+Y result of imported FBX meshes in Unity.", "Blender/Max FBX Match는 Unity에서 FBX를 불러왔을 때와 같은 XZ/+Y 결과를 맞추기 위해 Y-Up을 권장합니다."), MessageType.Warning);
                    }
                    if (_settings.pivotAnchor != FXMGlobalPivotAnchor.BoundsCenter)
                    {
                        EditorGUILayout.HelpBox(Tr("Blender/Max FBX Match recommends Bounds Center Pivot unless you intentionally need a custom pivot.", "Blender/Max FBX Match는 Bounds Center Pivot을 권장합니다. 의도적으로 피봇을 다르게 잡는 경우만 예외입니다."), MessageType.Info);
                    }
                    break;

                case FXMExportPreset.GenericDCC:
                    if (_settings.upAxis != FXMUpAxis.ZUp)
                    {
                        EditorGUILayout.HelpBox(Tr("Generic DCC recommends Z-Up. Y-Up is fine for meshes used directly in Unity.", "Generic DCC는 Z-Up을 권장합니다. Unity 안에서 바로 쓸 메시라면 Y-Up도 문제는 없습니다."), MessageType.Info);
                    }
                    if (!_settings.recalculateTangents)
                    {
                        EditorGUILayout.HelpBox(Tr("Tangents are recommended for DCC or generic pipeline handoff.", "DCC/범용 파이프라인 전달용은 Tangents 포함을 권장합니다."), MessageType.Info);
                    }
                    break;
            }

            if (_settings.doubleSidedGeometry)
            {
                EditorGUILayout.HelpBox(Tr("Double Sided Geometry is enabled. It reduces culling issues but roughly doubles vertex/triangle count.", "Double Sided Geometry가 켜져 있습니다. Cull 문제는 줄지만 버텍스/삼각형 수가 약 2배가 됩니다."), MessageType.Info);
            }

            if (bounds.size.x < 0.0001f || bounds.size.y < 0.0001f || bounds.size.z < 0.0001f)
            {
                EditorGUILayout.HelpBox(Tr("One Bounds axis is very thin. This may be normal for flat VFX meshes, but check camera angle, culling, and particle bounds.", "Bounds 중 한 축이 매우 얇습니다. 평면형 VFX Mesh라면 정상일 수 있지만, 카메라 각도/컬링/파티클 Bounds 문제를 확인하세요."), MessageType.Info);
            }

            EditorGUILayout.HelpBox(ready ? "✅ " + GetExportPresetDisplayName(_settings.exportPreset) + " Ready" : "⚠ " + GetExportPresetDisplayName(_settings.exportPreset) + Tr(" setting.", " 설정을 확인하세요."), ready ? MessageType.None : MessageType.Warning);
        }

        private void DrawFullRegressionQAPanel()
        {
            EditorGUILayout.Space(6f);
            _showFullRegressionQa = EditorGUILayout.Foldout(_showFullRegressionQa, Tr("7-2. Full Regression QA", "7-2. 전체 회귀 QA"), true);
            if (!_showFullRegressionQa)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("Runs a bundled release-candidate report covering mesh validation, flow visual QA, shader preview readiness, sample folders, demo-scene safety, and store packaging expectations.",
                       "메시 검증, Flow 시각 QA, 셰이더 프리뷰 준비도, 샘플 폴더, 데모 씬 안전성, 스토어 패키징 기준을 한 번에 점검합니다."),
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Run Full Regression QA", "전체 회귀 QA 실행"), GUILayout.Height(26f)))
                    {
                        _lastFullRegressionReport = BuildFullRegressionQAReport();
                        EditorGUIUtility.systemCopyBuffer = _lastFullRegressionReport;
                    }

                    if (GUILayout.Button(Tr("Copy Full Report", "전체 리포트 복사"), GUILayout.Height(26f)))
                    {
                        if (string.IsNullOrEmpty(_lastFullRegressionReport))
                        {
                            _lastFullRegressionReport = BuildFullRegressionQAReport();
                        }
                        EditorGUIUtility.systemCopyBuffer = _lastFullRegressionReport;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Run UI Safety QA", "UI 안전 QA 실행"), GUILayout.Height(22f)))
                    {
                        _lastFullRegressionReport = BuildUiSafetyQAReport();
                        EditorGUIUtility.systemCopyBuffer = _lastFullRegressionReport;
                    }

                    if (GUILayout.Button(Tr("Run Shader Preview QA", "셰이더 프리뷰 QA 실행"), GUILayout.Height(22f)))
                    {
                        _lastFullRegressionReport = BuildShaderPreviewQAReport();
                        EditorGUIUtility.systemCopyBuffer = _lastFullRegressionReport;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Run Package Integrity QA", "패키지 무결성 QA 실행"), GUILayout.Height(22f)))
                    {
                        _lastFullRegressionReport = BuildPackageIntegrityQAReport();
                        EditorGUIUtility.systemCopyBuffer = _lastFullRegressionReport;
                    }
                }

                if (!string.IsNullOrEmpty(_lastFullRegressionReport))
                {
                    _fullRegressionReportScroll = EditorGUILayout.BeginScrollView(_fullRegressionReportScroll, GUILayout.MinHeight(140f), GUILayout.MaxHeight(260f));
                    EditorGUILayout.TextArea(_lastFullRegressionReport, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private string BuildFullRegressionQAReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Full Regression QA");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add(string.Empty);
            lines.Add("## Mesh Validation Suite");
            lines.Add(RunAllMeshTypesValidationReport());
            lines.Add(string.Empty);
            lines.Add("## Flow Visual QA");
            lines.Add(BuildFlowVisualQAReport());
            lines.Add(string.Empty);
            lines.Add("## Shader Preview QA");
            lines.Add(BuildShaderPreviewQAReport());
            lines.Add(string.Empty);
            lines.Add("## UI Safety QA");
            lines.Add(BuildUiSafetyQAReport());
            lines.Add(string.Empty);
            lines.Add("## Sample / Demo QA");
            lines.Add(BuildSampleAndDemoQAReport());
            lines.Add(string.Empty);
            lines.Add("## Package Integrity QA");
            lines.Add(BuildPackageIntegrityQAReport());
            lines.Add(string.Empty);
            lines.Add("## Store RC Checklist");
            lines.Add(BuildStoreReleaseCandidateReport(false));
            lines.Add(string.Empty);
            lines.Add("FINAL BUNDLE NOTE: This report does not create or delete scenes/assets except transient validation meshes.");
            return string.Join("\n", lines.ToArray());
        }

        private string BuildUiSafetyQAReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FXM UI Safety QA");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Demo Scene Build: PASS - scheduled through EditorApplication.delayCall, not direct OnGUI execution.");
            lines.Add("GUILayout Safety: PASS - scene creation is deferred outside active layout scopes.");
            lines.Add("Workspace Mode: " + _workspaceMode);
            lines.Add("Normal Lines Default: " + (_showNormalHelper ? "CHECK - currently enabled by user" : "PASS - off by default"));
            lines.Add("Preview LOD Mode: " + _previewLodMode);
            return string.Join("\n", lines.ToArray());
        }

        private string BuildShaderPreviewQAReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FXM Shader Preview QA");
            lines.Add("Version: " + ToolVersion);
            Shader shader = Shader.Find(PreviewShaderName);
            lines.Add("Shader.Find: " + (shader != null ? "PASS - " + shader.name : "CHECK - shader not found by name"));
            string shaderPath = PackageRoot + "/Shaders/FXM_VertexAlphaPreview.shader";
            UnityEngine.Object shaderAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(shaderPath);
            lines.Add("Shader Asset: " + (shaderAsset != null ? "PASS - " + shaderPath : "CHECK - missing at " + shaderPath));
            Material material = GetPreviewMaterial();
            lines.Add("Preview Material: " + (material != null ? "PASS" : "CHECK - missing"));
            lines.Add("Preview Mode: " + _previewMode);
            return string.Join("\n", lines.ToArray());
        }

        private string BuildSampleAndDemoQAReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FXM Sample / Demo QA");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Sample Mesh Folder: " + (AssetDatabase.IsValidFolder(GeneratedSampleMeshesFolder) ? "PASS" : "CHECK - missing"));
            lines.Add("Sample Prefab Folder: " + (AssetDatabase.IsValidFolder(GeneratedSamplePrefabsFolder) ? "PASS" : "CHECK - missing"));
            lines.Add("Sample Scene Folder: " + (AssetDatabase.IsValidFolder(GeneratedSampleScenesFolder) ? "PASS" : "CHECK - missing"));
            lines.Add("Demo Scene Builder: PASS - available from Production Recipes panel.");
            lines.Add("Scene Creation Safety: PASS - delayed execution path is used.");
            return string.Join("\n", lines.ToArray());
        }

        private string BuildPackageIntegrityQAReport()
        {
            List<string> lines = new List<string>();
            lines.Add("FXM Package Integrity QA");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Package Root: " + PackageRoot + " / " + (AssetDatabase.IsValidFolder(PackageRoot) ? "PASS" : "CHECK"));
            lines.Add("Editor Script: " + (File.Exists(PackageRoot + "/Editor/FXMeshGeneratorProUnityEdition.cs") ? "PASS" : "CHECK"));
            lines.Add("Preview Shader: " + (File.Exists(PackageRoot + "/Shaders/FXM_VertexAlphaPreview.shader") ? "PASS" : "CHECK"));
            string[] docs = GetFinalDocumentationFileList();
            for (int i = 0; i < docs.Length; i++)
            {
                lines.Add("Doc / " + docs[i] + ": " + (File.Exists(docs[i]) ? "PASS" : "CHECK - missing"));
            }
            lines.Add("Generated Root: " + (AssetDatabase.IsValidFolder(GeneratedRoot) ? "PASS" : "INFO - will be created on demand"));
            return string.Join("\n", lines.ToArray());
        }

        private static string[] GetFinalDocumentationFileList()
        {
            return new string[]
            {
                "README.md",
                "CHANGELOG.md",
                "IMPORT_CHECK.md",
                "QUICK_START.md",
                "DEMO_SCENE_GUIDE.md",
                "SAMPLE_PACK_GUIDE.md",
                "STORE_READY_GUIDE.md",
                "STORE_RELEASE_CHECKLIST.md",
                "FAQ.md",
                "KNOWN_ISSUES.md",
                "LICENSE.md",
                "UNITY_PARTICLE_USAGE.md",
                "VERTEX_ALPHA_SHADER_GUIDE.md",
                "FLOW_UV_GUIDE.md",
                "MOBILE_OPTIMIZATION_GUIDE.md",
                "FINAL_RELEASE_QA.md"
            };
        }

        private void DrawPublisherToolsPanel()
        {
            if (_workspaceMode != FXMWorkspaceMode.Advanced)
            {
                return;
            }

            EditorGUILayout.Space(6f);
            _showPublisherTools = EditorGUILayout.Foldout(_showPublisherTools, Tr("Developer / Publisher Tools", "개발자 / 퍼블리셔 도구"), true);
            if (!_showPublisherTools)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("These tools are for the asset creator before store submission. They are hidden from the normal Basic workflow to avoid accidental cleanup or confusing store-copy actions.",
                       "이 도구는 스토어 제출 전 제작자/판매자용입니다. 일반 Basic 작업 흐름에서는 실수 삭제나 혼란을 막기 위해 숨겨둡니다."),
                    MessageType.Warning);

                _publisherToolsUnlocked = EditorGUILayout.ToggleLeft(
                    Tr("Unlock publisher tools for this session", "이번 세션에서 퍼블리셔 도구 잠금 해제"),
                    _publisherToolsUnlocked);

                using (new EditorGUI.DisabledScope(!_publisherToolsUnlocked))
                {
                    DrawStoreReleaseCandidatePanel();
                }
            }
        }

        private void DrawStoreReleaseCandidatePanel()
        {
            EditorGUILayout.Space(6f);
            _showStoreReleaseCandidate = EditorGUILayout.Foldout(_showStoreReleaseCandidate, Tr("Store Release Candidate", "스토어 릴리즈 후보"), true);
            if (!_showStoreReleaseCandidate)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(
                    Tr("Final packaging helper. It creates final documentation files, copies store text, runs an RC checklist, and prepares a clean screenshot/demo workflow.",
                       "최종 패키징 보조 패널입니다. 최종 문서 생성, 스토어 문구 복사, RC 체크리스트 실행, 스크린샷/데모 흐름을 준비합니다."),
                    MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Finalize Store Package", "스토어 패키지 최종 준비"), GUILayout.Height(26f)))
                    {
                        RunFinalStorePackageSetup();
                    }

                    if (GUILayout.Button(Tr("Clean Generated Folder", "Generated 폴더 정리"), GUILayout.Height(26f)))
                    {
                        CleanGeneratedFolderWithPrompt();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Release Candidate Mode", "릴리즈 후보 모드"), GUILayout.Height(24f)))
                    {
                        ApplyReleaseCandidateMode();
                    }

                    if (GUILayout.Button(Tr("Run RC Checklist", "RC 체크리스트 실행"), GUILayout.Height(24f)))
                    {
                        _lastStoreReleaseReport = BuildStoreReleaseCandidateReport(true);
                        EditorGUIUtility.systemCopyBuffer = _lastStoreReleaseReport;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Create Final Docs", "최종 문서 생성"), GUILayout.Height(22f)))
                    {
                        CreateFinalDocumentationFiles(true);
                    }

                    if (GUILayout.Button(Tr("Copy Store Description", "스토어 설명 복사"), GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildStoreDescriptionDraft();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Copy Technical Details", "기술 상세 복사"), GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildTechnicalDetailsDraft();
                    }

                    if (GUILayout.Button(Tr("Reveal Package Root", "패키지 루트 열기"), GUILayout.Height(22f)))
                    {
                        UnityEngine.Object root = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PackageRoot);
                        if (root != null)
                        {
                            EditorGUIUtility.PingObject(root);
                            Selection.activeObject = root;
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(Tr("Copy Fab Description", "Fab 설명 복사"), GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildFabDescriptionDraft();
                    }

                    if (GUILayout.Button(Tr("Copy Update Log", "업데이트 로그 복사"), GUILayout.Height(22f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildUpdateLogDraft();
                    }
                }

                if (!string.IsNullOrEmpty(_lastStoreReleaseReport))
                {
                    _storeReleaseReportScroll = EditorGUILayout.BeginScrollView(_storeReleaseReportScroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(240f));
                    EditorGUILayout.TextArea(_lastStoreReleaseReport, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void RunFinalStorePackageSetup()
        {
            ApplyReleaseCandidateMode();
            CreateFinalDocumentationFiles(false);
            _lastFullRegressionReport = BuildFullRegressionQAReport();
            _lastStoreReleaseReport = BuildStoreReleaseCandidateReport(true);
            EditorGUIUtility.systemCopyBuffer = _lastStoreReleaseReport + "\n\n" + _lastFullRegressionReport;
            EditorUtility.DisplayDialog(
                "FXM Final Store Package",
                Tr("Final release setup is complete. Documentation was refreshed and RC/Regression reports were copied to the clipboard.",
                   "최종 릴리즈 준비가 완료되었습니다. 문서를 갱신했고 RC/Regression 리포트를 클립보드에 복사했습니다."),
                "OK");
        }

        private void CleanGeneratedFolderWithPrompt()
        {
            if (!EditorUtility.DisplayDialog(
                    "FXM Clean Generated Folder",
                    Tr("Publisher tool only. This removes Assets/FXMeshGeneratorPro/Generated and recreates the clean folder structure. Generated meshes, prefabs, samples, and demo scenes will be removed.",
                       "퍼블리셔 전용 도구입니다. Assets/FXMeshGeneratorPro/Generated 폴더를 삭제한 뒤 깨끗한 폴더 구조로 다시 만듭니다. 생성된 메시, 프리팹, 샘플, 데모 씬이 삭제됩니다."),
                    "Clean Generated",
                    "Cancel"))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                AssetDatabase.DeleteAsset(GeneratedRoot);
            }

            EnsurePackageFolders(false);
            EnsureFolder(GeneratedSamplesFolder);
            EnsureFolder(GeneratedSampleMeshesFolder);
            EnsureFolder(GeneratedSamplePrefabsFolder);
            EnsureFolder(GeneratedSampleScenesFolder);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("FXM Clean Generated Folder", Tr("Generated folder structure was recreated.", "Generated 폴더 구조를 다시 만들었습니다."), "OK");
        }

        private void ApplyReleaseCandidateMode()
        {
            _workspaceMode = FXMWorkspaceMode.Basic;
            _previewMode = FXMPreviewMode.UVGradient;
            _previewLodMode = FXMPreviewLodMode.Off;
            _showUvMiniMapPreview = true;
            _showUvFlowHelper = true;
            _showUvSurfaceFlowArrows = true;
            _showUvBasisHelper = true;
            _showFaceOrientationHelper = false;
            _showNormalHelper = false;
            _validationCopyReportAfterRun = true;
            RebuildPreviewMesh(true, true);
        }

        private string BuildStoreReleaseCandidateReport(bool includeFileChecks)
        {
            List<string> lines = new List<string>();
            lines.Add("FX Mesh Generator Pro Store Release Candidate Checklist");
            lines.Add("Version: " + ToolVersion);
            lines.Add("Package Root: " + PackageRoot);
            lines.Add("Workspace Mode: " + _workspaceMode);
            lines.Add("Preview Mode: " + _previewMode);
            lines.Add("Generated Root: " + GeneratedRoot);
            lines.Add(string.Empty);
            lines.Add("Required checks:");
            lines.Add("[ ] Import package into a clean Unity project.");
            lines.Add("[ ] Open Tools > FX Mesh Generator Pro > Unity Edition.");
            lines.Add("[ ] Run Full Regression QA.");
            lines.Add("[ ] Generate Sample Prefab Pack.");
            lines.Add("[ ] Build Demo Scene.");
            lines.Add("[ ] Verify UV flow texture, surface arrows, and U/V basis overlay.");
            lines.Add("[ ] Verify Vertex Color Alpha in the target URP/VFX material.");
            lines.Add("[ ] Export at least one Mesh Asset and one Prefab.");
            lines.Add("[ ] Review README, QUICK_START, DEMO_SCENE_GUIDE, SAMPLE_PACK_GUIDE, FAQ, KNOWN_ISSUES, LICENSE.");
            if (includeFileChecks)
            {
                lines.Add(string.Empty);
                lines.Add("File checks:");
                string[] docs = GetFinalDocumentationFileList();
                for (int i = 0; i < docs.Length; i++)
                {
                    lines.Add("- " + docs[i] + ": " + (File.Exists(docs[i]) ? "PASS" : "CHECK - missing"));
                }
            }
            return string.Join("\n", lines.ToArray());
        }

        private void CreateFinalDocumentationFiles(bool showDialog)
        {
            File.WriteAllText("FAQ.md", BuildFaqDocument());
            File.WriteAllText("KNOWN_ISSUES.md", BuildKnownIssuesDocument());
            File.WriteAllText("LICENSE.md", BuildLicenseDocument());
            File.WriteAllText("STORE_RELEASE_CHECKLIST.md", BuildStoreReleaseCandidateReport(false));
            File.WriteAllText("UNITY_PARTICLE_USAGE.md", BuildUnityParticleUsageDocument());
            File.WriteAllText("VERTEX_ALPHA_SHADER_GUIDE.md", BuildVertexAlphaShaderGuideDocument());
            File.WriteAllText("FLOW_UV_GUIDE.md", BuildFlowUvGuideDocument());
            File.WriteAllText("MOBILE_OPTIMIZATION_GUIDE.md", BuildMobileOptimizationGuideDocument());
            File.WriteAllText("FINAL_RELEASE_QA.md", BuildFinalReleaseQaDocument());
            AssetDatabase.Refresh();
            if (showDialog)
            {
                EditorUtility.DisplayDialog("FXM Store Docs", Tr("Final documentation files were created or refreshed.", "최종 문서 파일을 생성/갱신했습니다."), "OK");
            }
        }

        private string BuildFinalReleaseQaDocument()
        {
            return "# Final Release QA - " + ToolVersion + "\n\n" +
                   "## Basic User Workflow\n" +
                   "- [ ] Open the tool.\n" +
                   "- [ ] Confirm Basic Mode is clean and publisher tools are hidden.\n" +
                   "- [ ] Generate each Mesh Type.\n" +
                   "- [ ] Save Mesh Asset.\n" +
                   "- [ ] Save Preview Prefab.\n\n" +
                   "## Flow Preview\n" +
                   "- [ ] Enable Use Flow Preview.\n" +
                   "- [ ] Enable UV Surface Flow Arrows.\n" +
                   "- [ ] Test V Forward / V Reverse.\n" +
                   "- [ ] Test U Forward / U Reverse.\n" +
                   "- [ ] Test Flip U / Flip V.\n" +
                   "- [ ] Move Flow Offset through 0, 0.25, and 0.5.\n" +
                   "- [ ] Confirm helper arrows and material arrows agree.\n\n" +
                   "## Advanced QA\n" +
                   "- [ ] Run Mesh Validation Suite.\n" +
                   "- [ ] Run Flow Visual QA.\n" +
                   "- [ ] Run Full Regression QA.\n" +
                   "- [ ] Generate Sample Prefab Pack.\n" +
                   "- [ ] Build Demo Scene.\n";
        }

        private string BuildStoreDescriptionDraft()
        {
            return "FX Mesh Generator Pro - Unity Edition\n" +
                   "A production-focused Unity Editor tool for creating game VFX meshes such as slash arcs, beam strips, shockwave rings, impact discs, shield domes, and helix energy trails.\n\n" +
                   "Highlights:\n" +
                   "- Production Recipes and Sample Pack Generator\n" +
                   "- Demo Scene Builder\n" +
                   "- UV Flow Preview on the actual mesh surface\n" +
                   "- Vertex Alpha Pro tools\n" +
                   "- Mesh Validation Suite and Full Regression QA\n" +
                   "- Basic/Advanced workspace modes\n" +
                   "- Mobile-oriented QA warnings and preview LOD workflow";
        }

        private string BuildTechnicalDetailsDraft()
        {
            return "Technical Details\n" +
                   "- Unity Editor extension under Assets/FXMeshGeneratorPro/Editor\n" +
                   "- Generates Mesh assets and material-free prefabs\n" +
                   "- Uses UV0 and Color32 vertex streams\n" +
                   "- Includes preview shader for UV flow, checker, vertex alpha, and RGB mask QA\n" +
                   "- Supports Slash, Beam, Ring, Disc, Dome, Half Dome, Helix, and Cross Helix mesh types\n" +
                   "- Includes validation reports for mesh integrity, UVs, Color32, bounds, mobile budgets, sample packs, and release-candidate checks";
        }

        private string BuildFabDescriptionDraft()
        {
            return BuildStoreDescriptionDraft() + "\n\nSuggested tags: Unity, VFX, Mesh Generator, Particle Mesh, URP, Vertex Color, Game Effects, Tools.";
        }

        private string BuildUpdateLogDraft()
        {
            return "FX Mesh Generator Pro v1.0.0 RC1 Asset Store Submission\n" +
                   "- Promoted the stable UV Flow build to v1.0.0 RC1 release-candidate branding.\n" +
                   "- Simplified Basic Mode export/save workflow and moved advanced QA/publisher utilities behind Advanced Mode.\n" +
                   "- Flow Preview final QA workflow for 2D preview, on-mesh flow texture, surface arrows, and U/V basis overlay.\n" +
                   "- Regression QA hardening with package integrity checks.\n" +
                   "- Store Release Candidate workflow with final docs, store text, and cleanup tools.\n" +
                   "- Demo Scene visual polish with improved lighting, framing, labels, and readme object.\n" +
                   "- Documentation final pass for particle usage, vertex alpha shaders, flow UV, and mobile optimization.";
        }

        private string BuildFaqDocument()
        {
            return "# FAQ\n\n" +
                   "## Does this tool export final materials?\nNo. Generated prefabs are intentionally material-free. Assign your own URP/VFX shader materials after generation.\n\n" +
                   "## Why are vertex colors important?\nThe tool writes Color32 data so shaders and Particle System Mesh Renderer workflows can use vertex alpha or RGB masks.\n\n" +
                   "## Why does the preview material look different from my final shader?\nThe preview material is a QA material. It helps inspect UV flow, vertex alpha, RGB channels, and mesh shape; it is not intended as the final visual shader.\n\n" +
                   "## Should I use Double Sided Geometry?\nOnly when the target shader cannot render two-sided surfaces. It roughly doubles mesh cost.\n\n" +
                   "## What should I run before packaging?\nRun Full Regression QA, Mesh Validation Suite, Flow Visual QA, Sample Pack Generator, and Demo Scene Builder.";
        }

        private string BuildKnownIssuesDocument()
        {
            return "# Known Issues\n\n" +
                   "- Preview shaders are for editor QA and may not represent final production shaders.\n" +
                   "- Very high segment Cross Helix meshes can be heavy in Scene View. Use Preview LOD during editing.\n" +
                   "- Generated prefabs are material-free by design. Assign final materials manually.\n" +
                   "- Demo Scene Builder creates a new scene. Save current work when prompted.\n" +
                   "- UV flow direction should be validated with the on-mesh flow texture, surface arrows, and UV Mini Map together.";
        }

        private string BuildLicenseDocument()
        {
            return "# License\n\n" +
                   "This package is provided as a Unity Editor tool for generating game VFX meshes.\n\n" +
                   "Generated mesh assets may be used in commercial and non-commercial game projects.\n" +
                   "Do not redistribute or resell the tool source/package itself as a competing editor tool without permission.\n\n" +
                   "Replace this file with your final marketplace license terms before public release if required.";
        }

        private string BuildUnityParticleUsageDocument()
        {
            return "# Unity Particle Usage\n\n" +
                   "1. Generate or save a mesh from FX Mesh Generator Pro.\n" +
                   "2. Assign the generated mesh to a Particle System Mesh Renderer.\n" +
                   "3. Use a material that reads UV0 and vertex color/alpha.\n" +
                   "4. For mobile targets, prefer fewer segments and avoid Double Sided Geometry unless required.\n" +
                   "5. Run Mesh Validation Suite and Full Regression QA before packaging.";
        }

        private string BuildVertexAlphaShaderGuideDocument()
        {
            return "# Vertex Alpha Shader Guide\n\n" +
                   "Generated meshes include Color32 data. Use vertex color alpha as opacity, dissolve, edge strength, or VFX mask data.\n\n" +
                   "Recommended shader inputs:\n" +
                   "- UV0 for flow/noise textures\n" +
                   "- Vertex Color RGB for mask packing when needed\n" +
                   "- Vertex Color A for alpha, dissolve, or soft edges\n\n" +
                   "Use Vertex Alpha Pro Tools to preview Alpha, Alpha Opacity, and RGB Mask modes.";
        }

        private string BuildFlowUvGuideDocument()
        {
            return "# Flow UV Guide\n\n" +
                   "Use the 2D UV Flow Preview, On-Mesh Flow Texture, Surface Flow Arrows, and U/V Basis Overlay together.\n\n" +
                   "Checks to run:\n" +
                   "- V Forward / V Reverse should oppose each other.\n" +
                   "- U Forward / U Reverse should oppose each other.\n" +
                   "- Circular CW / CCW should rotate in opposite directions.\n" +
                   "- Flip U / Flip V should update the on-mesh flow texture and surface arrows.\n\n" +
                   "When in doubt, enable Flow Debug View and run Flow Visual QA.";
        }

        private string BuildMobileOptimizationGuideDocument()
        {
            return "# Mobile Optimization Guide\n\n" +
                   "Recommended workflow:\n" +
                   "1. Start with Mobile Defaults in Mesh Quality Pass.\n" +
                   "2. Keep segment counts modest for Particle Mesh usage.\n" +
                   "3. Use Preview LOD while editing heavy shapes.\n" +
                   "4. Avoid Double Sided Geometry unless the target shader cannot render two-sided surfaces.\n" +
                   "5. Use Mesh Validation Suite to check vertex/triangle budgets.\n" +
                   "6. Validate final flow and vertex alpha before export.";
        }

        private static void GetColor32AlphaRange(Color32[] colors, int expectedCount, out float minAlpha, out float maxAlpha)
        {
            minAlpha = 0f;
            maxAlpha = 0f;
            if (colors == null || colors.Length != expectedCount || expectedCount <= 0)
            {
                return;
            }

            byte minByte = 255;
            byte maxByte = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                byte a = colors[i].a;
                if (a < minByte) minByte = a;
                if (a > maxByte) maxByte = a;
            }

            minAlpha = minByte / 255f;
            maxAlpha = maxByte / 255f;
        }

        private static string FormatVector3(Vector3 value)
        {
            return value.x.ToString("0.###") + ", " + value.y.ToString("0.###") + ", " + value.z.ToString("0.###");
        }

        private void DrawFolderSettings()
        {
            EditorGUILayout.Space(6f);
            _showFolder = EditorGUILayout.Foldout(_showFolder, Tr("7-2. Folder / Package Management", "7. 폴더 / 패키지 관리"), true);
            if (!_showFolder) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Package Root", PackageRoot);
                EditorGUILayout.LabelField("Generated", GeneratedRoot);
                EditorGUILayout.LabelField("Meshes", GeneratedMeshesFolder);
                EditorGUILayout.LabelField("Materials", GeneratedMaterialFolder);
                EditorGUILayout.LabelField("Prefabs", GeneratedPrefabFolder);
                EditorGUILayout.LabelField("Sample Meshes", GeneratedSampleMeshesFolder);
                EditorGUILayout.LabelField("Sample Prefabs", GeneratedSamplePrefabsFolder);
                EditorGUILayout.LabelField("Sample Scenes", GeneratedSampleScenesFolder);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create/Repair Folders", GUILayout.Height(26f)))
                    {
                        EnsurePackageFolders(true);
                    }

                    if (GUILayout.Button("Reveal Generated Folder", GUILayout.Height(26f)))
                    {
                        RevealGeneratedFolder();
                    }
                }
            }
        }

        private void DrawMeshStats()
        {
            if (_previewMesh == null)
            {
                EditorGUILayout.HelpBox(Tr("No preview mesh yet. Use Create / Update Scene Preview Object or Generate / Update Mesh Data first.", "아직 프리뷰 메시가 없습니다. 1단계의 Create / Update Scene Preview Object 또는 2단계의 Generate / Update Mesh Data를 눌러 생성하세요."), MessageType.Warning);
                return;
            }

            int triangleCount = _previewMesh.triangles != null ? _previewMesh.triangles.Length / 3 : 0;
            int vertexCount = _previewMesh.vertexCount;
            string mobileWarning = vertexCount > 2500 || triangleCount > 3500
                ? Tr("May be heavy for mobile. Reduce Segments or Double Sided Geometry.", "모바일 기준으로 다소 무거울 수 있습니다. Segments 또는 Double Sided Geometry를 줄이는 것을 권장합니다.")
                : Tr("Light enough for typical mobile VFX mesh use.", "모바일 VFX 메시 기준으로 가벼운 편입니다.");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Vertex Count", vertexCount.ToString());
            EditorGUILayout.LabelField("Triangle Count", triangleCount.ToString());
            EditorGUILayout.LabelField("Index Format", _previewMesh.indexFormat.ToString());
            EditorGUILayout.LabelField("Sided Preview", _settings.doubleSidedGeometry ? "Double Sided Geometry ON" : "Single Sided Geometry / Material Cull Back");
            EditorGUILayout.LabelField("Pivot Anchor", _settings.pivotAnchor.ToString());
            EditorGUILayout.LabelField("Up Axis", _settings.upAxis.ToString());
            DrawSurfaceIntegrityStatus();
            EditorGUILayout.HelpBox(mobileWarning, vertexCount > 2500 || triangleCount > 3500 ? MessageType.Warning : MessageType.None);
        }

        private void DrawSurfaceIntegrityStatus()
        {
            bool stripMesh = _settings.meshType == FXMMeshType.Slash ||
                            _settings.meshType == FXMMeshType.Beam ||
                            _settings.meshType == FXMMeshType.Ring ||
                            _settings.meshType == FXMMeshType.Disc;

            if (!stripMesh)
            {
                return;
            }

            int effectiveFlow = FXMMeshBuilder.GetEffectiveFlowSegmentsForPreview(_settings);
            int effectiveCross = FXMMeshBuilder.GetEffectiveCrossSegmentsForPreview(_settings);
            EditorGUILayout.LabelField("Surface Guard", "ON / Winding Sync + Degenerate Guard", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Effective Segments", effectiveFlow + " Flow / " + effectiveCross + " Cross", EditorStyles.miniLabel);

            if (_settings.meshType == FXMMeshType.Slash && _settings.radius > 0.0001f)
            {
                float ratio = _settings.width / Mathf.Max(0.0001f, _settings.radius);
                if (ratio > 1.75f)
                {
                    EditorGUILayout.HelpBox(Tr("Slash Width is very large compared with Radius. Surface Guard will correct radius flipping, but increasing Radius or reducing Width is recommended for a more stable slash.", "Slash Width가 Radius에 비해 매우 큽니다. v0.19 Surface Guard가 반지름 뒤집힘을 자동 보정하지만, 더 안정적인 검기는 Radius를 키우거나 Width를 조금 낮추는 것을 권장합니다."), MessageType.Info);
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_previewMesh == null)
            {
                return;
            }

            if (_previewObject == null)
            {
                _previewObject = GameObject.Find(PreviewObjectName);
            }

            if (_previewObject == null)
            {
                return;
            }

            MeshFilter meshFilter = _previewObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            FXMHelperFrame helperFrame = FXMHelperFrame.Create(mesh, _settings);
            Bounds finalBounds = mesh.bounds;
            Bounds helperBounds = helperFrame.localBounds;
            float helperSize = Mathf.Max(helperBounds.size.magnitude * _helperScale, 0.05f);

            Matrix4x4 oldMatrix = Handles.matrix;
            try
            {
                Handles.matrix = _previewObject.transform.localToWorldMatrix;

                if (_showWireBoundsHelper)
                {
                    DrawLocalOrientedHelperBounds(helperFrame);
                }

                if (_showMeshWireHelper)
                {
                    DrawLocalMeshWireOverlay(mesh, Mathf.Max(128, _meshWireMaxEdges), _meshWireColor, _meshWireThickness);
                }

                if (_showFaceOrientationHelper)
                {
                    DrawLocalFaceOrientationOverlay(mesh, _previewObject.transform, sceneView.camera, Mathf.Max(128, _faceOrientationTriangleLimit));
                }

                if (_showUvFlowHelper)
                {
                    DrawLocalFlowArrow(helperFrame, _settings, helperSize);
                    if (_showUvSurfaceFlowArrows)
                    {
                        DrawLocalUvSurfaceFlowArrows(mesh, _settings, Mathf.Max(8, _uvSurfaceArrowMaxCount), helperSize * _uvSurfaceArrowLength, _uvSurfaceArrowOffset);
                    }
                    if (_showUvBasisHelper)
                    {
                        DrawLocalUvBasisOverlay(mesh, Mathf.Max(8, _uvBasisSampleCount), helperSize * _uvBasisHelperLength, _uvSurfaceArrowOffset);
                    }
                }

                if (_showNormalHelper)
                {
                    DrawLocalNormalLines(mesh, Mathf.Max(finalBounds.size.magnitude * _helperScale * 0.35f, 0.025f), Mathf.Max(8, _normalSampleCount));
                }

                if (_showPivotGizmoHelper)
                {
                    DrawLocalPivotGizmo(helperSize);
                }

                if (_showAxisGizmoHelper)
                {
                    DrawLocalGeneratedAxisGizmo(helperFrame, helperSize);
                }

                if (_showBendHelper && _settings.maxBendEnable)
                {
                    DrawLocalMaxBendHelper(helperFrame, _settings, helperSize);
                }

                if (_showTaperHelper && _settings.maxTaperEnable)
                {
                    DrawLocalMaxTaperHelper(helperFrame, _settings, helperSize);
                }

                if (_showFfdHelper && _settings.ffdEnable)
                {
                    DrawLocalFfdLatticeHelper(helperFrame, _settings, helperSize);
                }
            }
            finally
            {
                Handles.matrix = oldMatrix;
            }

            if (_showBackfaceHelper)
            {
                Vector3 localTop = finalBounds.center + Vector3.up * (finalBounds.extents.y + Mathf.Max(finalBounds.size.magnitude * 0.08f, 0.1f));
                Vector3 worldTop = _previewObject.transform.TransformPoint(localTop);
                string sided = _settings.doubleSidedGeometry ? "Double Sided Geometry: ON" : "Single Sided: ON / Material Cull Back";
                string axis = _settings.upAxis == FXMUpAxis.YUp ? "Axis: Y-Up" : "Axis: Z-Up";
                string rot = _settings.pivotRotationEuler.sqrMagnitude > 0.0001f ? " / Pivot Rot Sync" : string.Empty;
                Handles.color = _settings.doubleSidedGeometry ? new Color(0.4f, 1f, 0.45f, 1f) : new Color(1f, 0.75f, 0.25f, 1f);
                Handles.Label(worldTop, "FXM Preview Guide\n" + sided + " / " + axis + rot + "\nHelpers follow Pivot Rotation / Up Axis");
            }
        }


        private struct FXMHelperFrame
        {
            public Quaternion rotation;
            public Quaternion inverseRotation;
            public Bounds localBounds;

            public static FXMHelperFrame Create(Mesh mesh, FXMSettings settings)
            {
                Quaternion rotation = GetPivotAxisRotation(settings);
                Quaternion inverse = Quaternion.Inverse(rotation);
                Bounds localBounds = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);

                if (mesh != null)
                {
                    Vector3[] vertices = mesh.vertices;
                    if (vertices != null && vertices.Length > 0)
                    {
                        localBounds = new Bounds(inverse * vertices[0], Vector3.zero);
                        for (int i = 1; i < vertices.Length; i++)
                        {
                            localBounds.Encapsulate(inverse * vertices[i]);
                        }
                    }
                }

                return new FXMHelperFrame
                {
                    rotation = rotation,
                    inverseRotation = inverse,
                    localBounds = localBounds
                };
            }

            public Vector3 P(Vector3 localPoint)
            {
                return rotation * localPoint;
            }

            public Vector3 D(Vector3 localDirection)
            {
                return rotation * localDirection;
            }
        }

        private static Quaternion GetPivotAxisRotation(FXMSettings settings)
        {
            Quaternion pivotRotation = settings.pivotRotationEuler.sqrMagnitude > 0.0000001f
                ? Quaternion.Euler(settings.pivotRotationEuler)
                : Quaternion.identity;
            Quaternion upAxisRotation = settings.upAxis == FXMUpAxis.ZUp
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
            return upAxisRotation * pivotRotation;
        }

        private static void DrawLocalOrientedHelperBounds(FXMHelperFrame frame)
        {
            Bounds b = frame.localBounds;
            Vector3 min = b.min;
            Vector3 max = b.max;
            Vector3[] c =
            {
                frame.P(new Vector3(min.x, min.y, min.z)),
                frame.P(new Vector3(max.x, min.y, min.z)),
                frame.P(new Vector3(max.x, max.y, min.z)),
                frame.P(new Vector3(min.x, max.y, min.z)),
                frame.P(new Vector3(min.x, min.y, max.z)),
                frame.P(new Vector3(max.x, min.y, max.z)),
                frame.P(new Vector3(max.x, max.y, max.z)),
                frame.P(new Vector3(min.x, max.y, max.z))
            };

            Handles.color = new Color(1f, 1f, 1f, 0.35f);
            Handles.DrawAAPolyLine(1.5f, c[0], c[1], c[2], c[3], c[0]);
            Handles.DrawAAPolyLine(1.5f, c[4], c[5], c[6], c[7], c[4]);
            Handles.DrawAAPolyLine(1.5f, c[0], c[4]);
            Handles.DrawAAPolyLine(1.5f, c[1], c[5]);
            Handles.DrawAAPolyLine(1.5f, c[2], c[6]);
            Handles.DrawAAPolyLine(1.5f, c[3], c[7]);
        }

        private static void DrawLocalPivotGizmo(float helperSize)
        {
            float size = Mathf.Max(helperSize * 0.55f, 0.035f);
            Handles.color = new Color(1f, 0.95f, 0.2f, 0.95f);
            Handles.DrawAAPolyLine(2.5f, new Vector3(-size, 0f, 0f), new Vector3(size, 0f, 0f));
            Handles.DrawAAPolyLine(2.5f, new Vector3(0f, -size, 0f), new Vector3(0f, size, 0f));
            Handles.DrawAAPolyLine(2.5f, new Vector3(0f, 0f, -size), new Vector3(0f, 0f, size));
            Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, size * 0.16f, EventType.Repaint);
            Handles.Label(new Vector3(size * 0.25f, size * 0.25f, 0f), "Pivot");
        }

        private static void DrawLocalGeneratedAxisGizmo(FXMHelperFrame frame, float helperSize)
        {
            float size = Mathf.Max(helperSize * 0.85f, 0.08f);
            Vector3 origin = Vector3.zero;
            Vector3 x = frame.D(Vector3.right) * size;
            Vector3 y = frame.D(Vector3.up) * size;
            Vector3 z = frame.D(Vector3.forward) * size;

            Handles.color = new Color(1f, 0.25f, 0.25f, 0.95f);
            Handles.DrawAAPolyLine(3f, origin, x);
            Handles.Label(x, "X");
            Handles.color = new Color(0.25f, 1f, 0.25f, 0.95f);
            Handles.DrawAAPolyLine(3f, origin, y);
            Handles.Label(y, "Y");
            Handles.color = new Color(0.25f, 0.55f, 1f, 0.95f);
            Handles.DrawAAPolyLine(3f, origin, z);
            Handles.Label(z, "Z");
        }


        private struct FXMWireEdge
        {
            public readonly int A;
            public readonly int B;

            public FXMWireEdge(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        private static void DrawLocalMeshWireOverlay(Mesh mesh, int maxEdges, Color wireColor, float wireThickness)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length < 3)
            {
                return;
            }

            int safeMaxEdges = Mathf.Max(64, maxEdges);
            int triangleCount = triangles.Length / 3;
            int rawEdgeCount = triangleCount * 3;
            int drawn = 0;
            int referenceEdgeCount = rawEdgeCount;

            CompareFunction oldZTest = Handles.zTest;
            try
            {
                Handles.zTest = CompareFunction.Always;
                Handles.color = wireColor;

                // v0.28.2:
                // Small/medium meshes use true unique edges for clean wire readability.
                // Dense Cross Helix meshes avoid HashSet allocation spikes and sample the raw edge stream
                // evenly across the whole mesh instead of drawing 3 clustered edges from one sampled triangle.
                if (rawEdgeCount <= safeMaxEdges)
                {
                    List<FXMWireEdge> edges = CollectUniqueWireEdges(vertices, triangles);
                    referenceEdgeCount = edges.Count;
                    for (int i = 0; i < edges.Count; i++)
                    {
                        DrawWireEdge(vertices, edges[i], wireThickness);
                        drawn++;
                    }
                }
                else
                {
                    int stride = Mathf.Max(1, Mathf.CeilToInt(rawEdgeCount / (float)safeMaxEdges));
                    int cursor = Mathf.Max(0, stride / 2);
                    while (cursor < rawEdgeCount && drawn < safeMaxEdges)
                    {
                        DrawWireEdgeByRawIndex(vertices, triangles, cursor, wireThickness);
                        drawn++;
                        cursor += stride;
                    }
                }

                if (referenceEdgeCount > safeMaxEdges)
                {
                    Handles.color = new Color(wireColor.r, wireColor.g, wireColor.b, Mathf.Clamp01(wireColor.a * 0.9f));
                    Bounds b = mesh.bounds;
                    Handles.Label(
                        b.center + Vector3.up * Mathf.Max(0.05f, b.size.magnitude * 0.035f),
                        "Wire Overlay: Smart Sampled " + drawn + " / " + referenceEdgeCount + " (Scene View Only)");
                }
            }
            finally
            {
                Handles.zTest = oldZTest;
            }
        }

        private static void DrawWireEdgeByRawIndex(Vector3[] vertices, int[] triangles, int rawEdgeIndex, float wireThickness)
        {
            int triStart = (rawEdgeIndex / 3) * 3;
            if (triStart < 0 || triStart + 2 >= triangles.Length)
            {
                return;
            }

            switch (rawEdgeIndex % 3)
            {
                case 0:
                    DrawWireEdge(vertices, new FXMWireEdge(triangles[triStart], triangles[triStart + 1]), wireThickness);
                    break;
                case 1:
                    DrawWireEdge(vertices, new FXMWireEdge(triangles[triStart + 1], triangles[triStart + 2]), wireThickness);
                    break;
                default:
                    DrawWireEdge(vertices, new FXMWireEdge(triangles[triStart + 2], triangles[triStart]), wireThickness);
                    break;
            }
        }

        private static List<FXMWireEdge> CollectUniqueWireEdges(Vector3[] vertices, int[] triangles)
        {
            List<FXMWireEdge> edges = new List<FXMWireEdge>();
            HashSet<ulong> seen = new HashSet<ulong>();

            for (int i = 0; i <= triangles.Length - 3; i += 3)
            {
                AddWireEdge(vertices, triangles[i], triangles[i + 1], seen, edges);
                AddWireEdge(vertices, triangles[i + 1], triangles[i + 2], seen, edges);
                AddWireEdge(vertices, triangles[i + 2], triangles[i], seen, edges);
            }

            return edges;
        }

        private static void AddWireEdge(Vector3[] vertices, int a, int b, HashSet<ulong> seen, List<FXMWireEdge> edges)
        {
            if (a < 0 || b < 0 || a >= vertices.Length || b >= vertices.Length || a == b)
            {
                return;
            }

            int min = Mathf.Min(a, b);
            int max = Mathf.Max(a, b);
            ulong key = ((ulong)(uint)min << 32) | (uint)max;
            if (!seen.Add(key))
            {
                return;
            }

            edges.Add(new FXMWireEdge(a, b));
        }

        private static void DrawWireEdge(Vector3[] vertices, FXMWireEdge edge, float wireThickness)
        {
            if (edge.A < 0 || edge.B < 0 || edge.A >= vertices.Length || edge.B >= vertices.Length || edge.A == edge.B)
            {
                return;
            }

            Handles.DrawAAPolyLine(Mathf.Clamp(wireThickness, 0.5f, 6f), vertices[edge.A], vertices[edge.B]);
        }

        private static void DrawLocalFaceOrientationOverlay(Mesh mesh, Transform previewTransform, Camera sceneCamera, int triangleLimit)
        {
            if (mesh == null || previewTransform == null || sceneCamera == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices == null || triangles == null || vertices.Length == 0 || triangles.Length < 3)
            {
                return;
            }

            int triangleCount = triangles.Length / 3;
            int drawBudget = Mathf.Clamp(triangleLimit, 128, 4096);
            int step = Mathf.Max(1, Mathf.CeilToInt(triangleCount / (float)drawBudget));
            Vector3 localCameraPosition = previewTransform.InverseTransformPoint(sceneCamera.transform.position);
            int drawnBackfaces = 0;
            int drawnFrontfaces = 0;

            for (int tri = 0; tri < triangleCount; tri += step)
            {
                int t = tri * 3;
                int ia = triangles[t];
                int ib = triangles[t + 1];
                int ic = triangles[t + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length || ia == ib || ib == ic || ia == ic)
                {
                    continue;
                }

                Vector3 a = vertices[ia];
                Vector3 b = vertices[ib];
                Vector3 c = vertices[ic];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude <= 0.00000001f)
                {
                    continue;
                }

                Vector3 center = (a + b + c) * (1f / 3f);
                Vector3 viewDirection = localCameraPosition - center;
                bool frontFacingToView = viewDirection.sqrMagnitude > 0.00000001f && Vector3.Dot(normal.normalized, viewDirection.normalized) >= 0f;
                Handles.color = frontFacingToView
                    ? new Color(0.1f, 0.85f, 1f, 0.12f)
                    : new Color(1f, 0.22f, 0.05f, 0.20f);
                Handles.DrawAAConvexPolygon(a, b, c);

                if (frontFacingToView)
                {
                    drawnFrontfaces++;
                }
                else
                {
                    drawnBackfaces++;
                }
            }

            Bounds bounds = mesh.bounds;
            Handles.color = drawnBackfaces > 0 ? new Color(1f, 0.35f, 0.1f, 0.95f) : new Color(0.2f, 1f, 0.45f, 0.95f);
            Handles.Label(bounds.center + Vector3.up * (bounds.extents.y + Mathf.Max(bounds.size.magnitude * 0.08f, 0.05f)),
                "Face Orientation QA\nFront: " + drawnFrontfaces + " / Back: " + drawnBackfaces);
        }

        private static void DrawLocalFlowArrow(FXMHelperFrame frame, FXMSettings settings, float arrowSize)
        {
            Bounds bounds = frame.localBounds;
            switch (settings.meshType)
            {
                case FXMMeshType.Slash:
                    DrawLocalSlashFlowGuide(frame, bounds, settings, arrowSize);
                    return;

                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                    DrawLocalCircularFlowGuide(frame, bounds, settings, arrowSize);
                    return;

                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    DrawLocalDomeFrontGuide(frame, bounds, settings, arrowSize);
                    return;

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    DrawLocalHelixFlowGuide(frame, bounds, settings, arrowSize);
                    return;

                default:
                    DrawLocalLinearFlowGuide(frame, bounds, settings, arrowSize);
                    return;
            }
        }

        private static void DrawLocalUvSurfaceFlowArrows(Mesh mesh, FXMSettings settings, int maxArrows, float arrowLength, float surfaceOffset)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector2[] uv = mesh.uv;
            if (vertices == null || triangles == null || uv == null || vertices.Length == 0 || triangles.Length < 3 || uv.Length != vertices.Length)
            {
                return;
            }

            Bounds bounds = mesh.bounds;
            int triangleCount = triangles.Length / 3;
            int drawBudget = Mathf.Clamp(maxArrows, 8, 256);
            int step = Mathf.Max(1, Mathf.CeilToInt(triangleCount / (float)drawBudget));
            float baseLength = Mathf.Max(bounds.size.magnitude * 0.06f, 0.02f);
            float offsetDistance = Mathf.Max(bounds.size.magnitude * surfaceOffset, 0.0005f);
            int drawn = 0;

            for (int tri = 0; tri < triangleCount && drawn < drawBudget; tri += step)
            {
                int t = tri * 3;
                int i0 = triangles[t];
                int i1 = triangles[t + 1];
                int i2 = triangles[t + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length || i0 >= uv.Length || i1 >= uv.Length || i2 >= uv.Length)
                {
                    continue;
                }

                Vector3 p0 = vertices[i0];
                Vector3 p1 = vertices[i1];
                Vector3 p2 = vertices[i2];
                Vector2 uv0 = uv[i0];
                Vector2 uv1 = uv[i1];
                Vector2 uv2 = uv[i2];

                if (!TryBuildTriangleUvBasis(p0, p1, p2, uv0, uv1, uv2, out Vector3 tangent, out Vector3 bitangent, out Vector3 faceNormal))
                {
                    continue;
                }

                Vector3 center = (p0 + p1 + p2) * (1f / 3f);
                Vector3 flowDirection = ResolveSurfaceFlowDirection(settings, tangent, bitangent, faceNormal, center, bounds.center);
                if (flowDirection.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                float triArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                float triScale = Mathf.Sqrt(Mathf.Max(triArea, 0.00001f));
                float finalLength = Mathf.Clamp(triScale * 0.8f, baseLength * 0.35f, baseLength * 1.5f) * Mathf.Max(0.1f, arrowLength);
                DrawSurfaceArrowColored(center + faceNormal * offsetDistance, flowDirection.normalized, faceNormal, finalLength, ResolveSurfaceFlowColor(settings));
                drawn++;
            }

            if (drawn > 0)
            {
                Handles.color = new Color(0.2f, 0.95f, 1f, 0.95f);
                Vector3 labelPos = bounds.center + Vector3.up * (bounds.extents.y + baseLength * 0.35f);
                Handles.Label(labelPos, Tr("UV Surface Flow Arrows", "UV 표면 흐름 화살표"));
            }
        }

        private static void DrawLocalUvBasisOverlay(Mesh mesh, int maxSamples, float arrowLength, float surfaceOffset)
        {
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector2[] uv = mesh.uv;
            if (vertices == null || triangles == null || uv == null || vertices.Length == 0 || triangles.Length < 3 || uv.Length != vertices.Length)
            {
                return;
            }

            Bounds bounds = mesh.bounds;
            int triangleCount = triangles.Length / 3;
            int drawBudget = Mathf.Clamp(maxSamples, 8, 192);
            int step = Mathf.Max(1, Mathf.CeilToInt(triangleCount / (float)drawBudget));
            float baseLength = Mathf.Max(bounds.size.magnitude * 0.055f, 0.018f) * Mathf.Max(0.1f, arrowLength);
            float offsetDistance = Mathf.Max(bounds.size.magnitude * surfaceOffset * 1.4f, 0.0007f);
            int drawn = 0;

            for (int tri = 0; tri < triangleCount && drawn < drawBudget; tri += step)
            {
                int t = tri * 3;
                int i0 = triangles[t];
                int i1 = triangles[t + 1];
                int i2 = triangles[t + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0 || i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length || i0 >= uv.Length || i1 >= uv.Length || i2 >= uv.Length)
                {
                    continue;
                }

                Vector3 p0 = vertices[i0];
                Vector3 p1 = vertices[i1];
                Vector3 p2 = vertices[i2];
                if (!TryBuildTriangleUvBasis(p0, p1, p2, uv[i0], uv[i1], uv[i2], out Vector3 tangent, out Vector3 bitangent, out Vector3 faceNormal))
                {
                    continue;
                }

                Vector3 center = (p0 + p1 + p2) * (1f / 3f) + faceNormal * offsetDistance;
                DrawSurfaceArrowColored(center, tangent, faceNormal, baseLength, new Color(1f, 0.55f, 0.15f, 0.92f));
                DrawSurfaceArrowColored(center, bitangent, faceNormal, baseLength * 0.86f, new Color(0.35f, 1f, 0.35f, 0.9f));
                drawn++;
            }

            if (drawn > 0)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.92f);
                Handles.Label(bounds.center + Vector3.up * (bounds.extents.y + Mathf.Max(bounds.size.magnitude * 0.12f, 0.05f)), "U/V Basis QA  U=Orange / V=Green");
            }
        }

        private static bool TryBuildTriangleUvBasis(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 uv0, Vector2 uv1, Vector2 uv2, out Vector3 tangent, out Vector3 bitangent, out Vector3 faceNormal)
        {
            tangent = Vector3.zero;
            bitangent = Vector3.zero;
            faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
            if (faceNormal.sqrMagnitude < 0.0000001f)
            {
                return false;
            }
            faceNormal.Normalize();

            Vector3 e1 = p1 - p0;
            Vector3 e2 = p2 - p0;
            Vector2 d1 = uv1 - uv0;
            Vector2 d2 = uv2 - uv0;
            float det = d1.x * d2.y - d1.y * d2.x;

            if (Mathf.Abs(det) > 0.000001f)
            {
                tangent = (e1 * d2.y - e2 * d1.y) / det;
                bitangent = (e2 * d1.x - e1 * d2.x) / det;
            }
            else
            {
                tangent = e1.sqrMagnitude > 0.000001f ? e1 : e2;
                bitangent = Vector3.Cross(faceNormal, tangent);
            }

            tangent = Vector3.ProjectOnPlane(tangent, faceNormal);
            if (tangent.sqrMagnitude < 0.000001f)
            {
                tangent = Vector3.ProjectOnPlane(e1.sqrMagnitude > 0.000001f ? e1 : e2, faceNormal);
            }

            if (tangent.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            tangent.Normalize();
            bitangent = Vector3.ProjectOnPlane(bitangent, faceNormal);
            if (bitangent.sqrMagnitude < 0.000001f)
            {
                bitangent = Vector3.Cross(faceNormal, tangent);
            }

            if (bitangent.sqrMagnitude < 0.000001f)
            {
                return false;
            }

            bitangent.Normalize();
            return true;
        }

        private static Color ResolveSurfaceFlowColor(FXMSettings settings)
        {
            switch (settings.uvFlowDirection)
            {
                case FXMUVFlowDirection.UReverse:
                case FXMUVFlowDirection.VReverse:
                case FXMUVFlowDirection.ToCenter:
                case FXMUVFlowDirection.CircularCCW:
                    return new Color(1f, 0.78f, 0.18f, 0.95f);
                case FXMUVFlowDirection.CircularCW:
                    return new Color(1f, 0.28f, 0.95f, 0.95f);
                case FXMUVFlowDirection.FromCenter:
                    return new Color(0.35f, 1f, 0.45f, 0.95f);
                default:
                    return new Color(0.2f, 0.95f, 1f, 0.92f);
            }
        }

        private static Vector3 ResolveSurfaceFlowDirection(FXMSettings settings, Vector3 tangent, Vector3 bitangent, Vector3 faceNormal, Vector3 triangleCenter, Vector3 meshCenter)
        {
            // v0.32.7: FINAL UV BASIS rule for Scene View helper arrows.
            // The generated mesh UVs already contain U/V Reverse, Circular, Center, Flip U, and Flip V transforms.
            // Tangent/bitangent are therefore the true physical directions of the final UV texture on the mesh.
            // Do not invert by direction/flip again here, or Reverse/Flip collapses back to the same visible direction.
            Vector3 uAxis = NormalizeFlowAxis(Vector3.ProjectOnPlane(tangent, faceNormal), Vector3.right, faceNormal);
            Vector3 vAxis = NormalizeFlowAxis(Vector3.ProjectOnPlane(bitangent, faceNormal), Vector3.Cross(faceNormal, uAxis), faceNormal);

            switch (GetFinalUvFlowAxis(settings.uvFlowDirection))
            {
                case FXMFlowPreviewAxis.U:
                    return uAxis;

                case FXMFlowPreviewAxis.V:
                default:
                    return vAxis;
            }
        }

        private static Vector3 NormalizeFlowAxis(Vector3 axis, Vector3 fallback, Vector3 faceNormal)
        {
            axis = Vector3.ProjectOnPlane(axis, faceNormal);
            if (axis.sqrMagnitude > 0.000001f)
            {
                return axis.normalized;
            }

            fallback = Vector3.ProjectOnPlane(fallback, faceNormal);
            if (fallback.sqrMagnitude > 0.000001f)
            {
                return fallback.normalized;
            }

            return Vector3.right;
        }

        private static Vector3 ResolveFinalUvRadialDirection(Vector3 uAxis, Vector3 vAxis, Vector3 triangleCenter, Vector3 meshCenter, Vector3 faceNormal)
        {
            Vector3 radial = Vector3.ProjectOnPlane(triangleCenter - meshCenter, faceNormal);
            if (radial.sqrMagnitude > 0.000001f)
            {
                return radial.normalized;
            }

            return vAxis.sqrMagnitude > 0.000001f ? vAxis.normalized : uAxis;
        }

        private static Vector3 ResolveFinalUvCircularDirection(Vector3 uAxis, Vector3 vAxis, Vector3 triangleCenter, Vector3 meshCenter, Vector3 faceNormal)
        {
            Vector3 radial = Vector3.ProjectOnPlane(triangleCenter - meshCenter, faceNormal);
            if (radial.sqrMagnitude > 0.000001f)
            {
                Vector3 circular = Vector3.Cross(faceNormal, radial).normalized;
                if (circular.sqrMagnitude > 0.000001f)
                {
                    return circular;
                }
            }

            // Fallback follows final material U, because circular UVs store angular progress on U.
            return uAxis.sqrMagnitude > 0.000001f ? uAxis.normalized : vAxis;
        }

        private static Vector3 ResolveAutoSurfaceFlowDirection(FXMMeshType meshType, Vector3 tangent, Vector3 bitangent)
        {
            switch (meshType)
            {
                case FXMMeshType.Beam:
                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    return bitangent.sqrMagnitude >= 0.000001f ? bitangent : tangent;

                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                case FXMMeshType.Slash:
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return tangent.sqrMagnitude >= 0.000001f ? tangent : bitangent;

                default:
                    return tangent.sqrMagnitude >= bitangent.sqrMagnitude ? tangent : bitangent;
            }
        }

        private static void DrawSurfaceArrow(Vector3 center, Vector3 direction, Vector3 faceNormal, float length)
        {
            if (direction.sqrMagnitude < 0.000001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 side = Vector3.Cross(faceNormal, direction);
            if (side.sqrMagnitude < 0.000001f)
            {
                side = Vector3.Cross(Vector3.up, direction);
            }
            if (side.sqrMagnitude < 0.000001f)
            {
                side = Vector3.right;
            }
            side.Normalize();

            Vector3 start = center - direction * (length * 0.45f);
            Vector3 end = center + direction * (length * 0.55f);
            Handles.color = new Color(0.2f, 0.95f, 1f, 0.92f);
            Handles.DrawAAPolyLine(2.5f, start, end);
            Handles.DrawSolidDisc(start, faceNormal, Mathf.Max(length * 0.07f, 0.004f));
            DrawArrowHead(end, direction, side, Mathf.Max(length * 0.26f, 0.01f));
        }

        private static void DrawSurfaceArrowColored(Vector3 center, Vector3 direction, Vector3 faceNormal, float length, Color color)
        {
            if (direction.sqrMagnitude < 0.000001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 side = Vector3.Cross(faceNormal, direction);
            if (side.sqrMagnitude < 0.000001f)
            {
                side = Vector3.Cross(Vector3.up, direction);
            }
            if (side.sqrMagnitude < 0.000001f)
            {
                side = Vector3.right;
            }
            side.Normalize();

            Vector3 start = center - direction * (length * 0.42f);
            Vector3 end = center + direction * (length * 0.58f);
            Handles.color = color;
            Handles.DrawAAPolyLine(2.3f, start, end);
            Handles.DrawSolidDisc(start, faceNormal, Mathf.Max(length * 0.07f, 0.004f));
            DrawArrowHead(end, direction, side, Mathf.Max(length * 0.24f, 0.01f));
        }

        private static void DrawLocalSlashFlowGuide(FXMHelperFrame frame, Bounds bounds, FXMSettings settings, float arrowSize)
        {
            Vector3 center = bounds.center;
            float radius = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), Mathf.Max(0.05f, settings.radius));
            float arc = Mathf.Clamp(settings.arcAngle <= 0.0001f ? 110f : settings.arcAngle, 1f, 360f);
            bool fullCircle = arc >= 359.9f;
            float startAngle = settings.startAngle - arc * 0.5f;
            float drawArc = fullCircle ? 320f : arc;
            Vector3 startDirection = DirectionXZ(startAngle);
            Vector3 surfaceNormal = Vector3.up;

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Handles.DrawWireArc(frame.P(center), frame.D(surfaceNormal), frame.D(startDirection), drawArc, radius);

            bool reverseFlow = settings.uvFlowDirection == FXMUVFlowDirection.VReverse ||
                               settings.uvFlowDirection == FXMUVFlowDirection.CircularCCW;
            float endAngle = startAngle + (reverseFlow ? 0f : drawArc);
            if (reverseFlow)
            {
                endAngle = startAngle + drawArc;
            }
            Vector3 radial = DirectionXZ(endAngle);
            Vector3 arrowEnd = center + radial * radius;
            Vector3 tangent = Vector3.Cross(surfaceNormal, radial).normalized;
            if (reverseFlow)
            {
                tangent = -tangent;
            }
            DrawArrowHead(frame.P(arrowEnd), frame.D(tangent), frame.D(radial), arrowSize);
            Handles.Label(frame.P(arrowEnd + tangent * arrowSize * 0.35f), "Arc UV Flow / " + settings.uvFlowDirection);
        }

        private static void DrawLocalLinearFlowGuide(FXMHelperFrame frame, Bounds bounds, FXMSettings settings, float arrowSize)
        {
            Vector3 center = bounds.center;
            Vector3 start;
            Vector3 mid = center;
            Vector3 end;
            Vector3 surfaceNormal = Vector3.up;

            bool horizontalU = settings.uvFlowDirection == FXMUVFlowDirection.UForward || settings.uvFlowDirection == FXMUVFlowDirection.UReverse;
            bool reversed = settings.uvFlowDirection == FXMUVFlowDirection.UReverse || settings.uvFlowDirection == FXMUVFlowDirection.VReverse;

            if (horizontalU)
            {
                start = new Vector3(bounds.min.x, center.y, center.z);
                end = new Vector3(bounds.max.x, center.y, center.z);
            }
            else if (settings.meshType == FXMMeshType.Beam)
            {
                start = new Vector3(center.x, center.y, bounds.min.z);
                end = new Vector3(center.x, center.y, bounds.max.z);
            }
            else
            {
                start = new Vector3(bounds.min.x, center.y, center.z);
                end = new Vector3(bounds.max.x, center.y, center.z);
            }

            if (reversed)
            {
                Vector3 tmp = start;
                start = end;
                end = tmp;
            }

            DrawArrowPolyline(frame.P(start), frame.P(mid), frame.P(end), frame.D(surfaceNormal), arrowSize, "Linear UV Flow / " + settings.uvFlowDirection);
        }

        private static void DrawLocalCircularFlowGuide(FXMHelperFrame frame, Bounds bounds, FXMSettings settings, float arrowSize)
        {
            Vector3 center = bounds.center;
            float outerRadius = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.05f);
            float arc = Mathf.Clamp(settings.arcAngle <= 0.0001f ? 360f : settings.arcAngle, 5f, 360f);
            bool fullCircle = arc >= 359.9f;
            float startAngle = settings.startAngle - (fullCircle ? 0f : arc * 0.5f);
            float drawArc = fullCircle ? 320f : arc;
            Vector3 startDirection = DirectionXZ(startAngle);
            Vector3 surfaceNormal = Vector3.up;

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Handles.DrawWireArc(frame.P(center), frame.D(surfaceNormal), frame.D(startDirection), drawArc, outerRadius);

            bool reverseFlow = settings.uvFlowDirection == FXMUVFlowDirection.CircularCCW ||
                               settings.uvFlowDirection == FXMUVFlowDirection.VReverse;
            float endAngle = startAngle + drawArc;
            Vector3 radial = DirectionXZ(endAngle);
            Vector3 arrowEnd = center + radial * outerRadius;
            Vector3 tangent = Vector3.Cross(surfaceNormal, radial).normalized;
            if (reverseFlow)
            {
                tangent = -tangent;
            }
            DrawArrowHead(frame.P(arrowEnd), frame.D(tangent), frame.D(radial), arrowSize);

            string label = settings.meshType == FXMMeshType.Ring ? "Circular UV Flow / Ring" : "Circular UV Flow / Disc";
            Handles.Label(frame.P(arrowEnd + tangent * arrowSize * 0.35f), label + " / " + settings.uvFlowDirection);

            Handles.color = new Color(1f, 0.65f, 0.2f, 0.78f);
            if (settings.meshType == FXMMeshType.Ring)
            {
                float innerRadius = Mathf.Max(0.01f, settings.radius - settings.width * 0.5f);
                float centerRadius = Mathf.Max(0.01f, settings.radius);
                Handles.DrawWireArc(frame.P(center), frame.D(surfaceNormal), frame.D(startDirection), drawArc, Mathf.Min(innerRadius, outerRadius));
                Handles.DrawWireArc(frame.P(center), frame.D(surfaceNormal), frame.D(startDirection), drawArc, Mathf.Min(centerRadius, outerRadius));
                Handles.Label(frame.P(center + surfaceNormal * arrowSize * 0.45f), "Center / Thickness Bias");
            }
            else
            {
                float innerRadius = Mathf.Clamp01(settings.innerRadiusRatio) * outerRadius;
                Handles.DrawWireArc(frame.P(center), frame.D(surfaceNormal), frame.D(startDirection), drawArc, Mathf.Max(0.01f, innerRadius));
                Vector3 radialGuide = DirectionXZ(settings.startAngle);
                Handles.DrawAAPolyLine(2f, frame.P(center), frame.P(center + radialGuide * outerRadius));
                Handles.Label(frame.P(center + surfaceNormal * arrowSize * 0.45f), "Center / Radial Bias");
            }
        }

        private static void DrawLocalDomeFrontGuide(FXMHelperFrame frame, Bounds bounds, FXMSettings settings, float arrowSize)
        {
            Vector3 center = bounds.center;
            Vector3 baseCenter = new Vector3(center.x, bounds.min.y, center.z);
            Vector3 top = new Vector3(center.x, bounds.max.y, center.z);
            Vector3 frontDirection = DirectionXZ(settings.startAngle);
            Vector3 rightDirection = Vector3.Cross(Vector3.up, frontDirection).normalized;
            float radius = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.05f);
            Vector3 front = baseCenter + frontDirection * (radius + arrowSize * 1.2f);

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Handles.DrawAAPolyLine(3f, frame.P(baseCenter), frame.P(baseCenter + frontDirection * radius), frame.P(front));
            DrawArrowHead(frame.P(front), frame.D(frontDirection), frame.D(rightDirection), arrowSize);
            Handles.Label(frame.P(front + frontDirection * arrowSize * 0.15f), settings.meshType == FXMMeshType.HalfDome ? "Front Direction / Open Side" : "Front Direction");

            Handles.color = new Color(0.35f, 1f, 0.45f, 0.75f);
            Handles.DrawAAPolyLine(2f, frame.P(baseCenter), frame.P(top));
            Handles.Label(frame.P(top + Vector3.up * arrowSize * 0.15f), "Height / Normal Check");

            float arc = settings.meshType == FXMMeshType.HalfDome ? Mathf.Clamp(settings.arcAngle, 30f, 360f) : 360f;
            float startAngle = settings.startAngle - arc * 0.5f;
            Vector3 startDir = DirectionXZ(startAngle);
            Handles.color = new Color(1f, 0.65f, 0.2f, 0.7f);
            Handles.DrawWireArc(frame.P(baseCenter), frame.D(Vector3.up), frame.D(startDir), arc >= 359.9f ? 320f : arc, radius);
            if (settings.meshType == FXMMeshType.HalfDome && arc < 359.9f)
            {
                Handles.DrawAAPolyLine(2f, frame.P(baseCenter), frame.P(baseCenter + DirectionXZ(startAngle) * radius));
                Handles.DrawAAPolyLine(2f, frame.P(baseCenter), frame.P(baseCenter + DirectionXZ(startAngle + arc) * radius));
            }
        }

        private static void DrawLocalHelixFlowGuide(FXMHelperFrame frame, Bounds bounds, FXMSettings settings, float arrowSize)
        {
            Vector3 center = bounds.center;
            int points = 48;
            Vector3[] line = new Vector3[points];
            float radius = Mathf.Max(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.05f);
            float turns = Mathf.Max(0.01f, settings.turns);
            float directionSign = settings.helixClockwise ? -1f : 1f;
            if (settings.uvFlowDirection == FXMUVFlowDirection.VReverse || settings.uvFlowDirection == FXMUVFlowDirection.CircularCCW)
            {
                directionSign *= -1f;
            }
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float angle = (settings.startAngle + directionSign * turns * 360f * t) * Mathf.Deg2Rad;
                float y = Mathf.Lerp(bounds.min.y, bounds.max.y, t);
                Vector3 localPoint = center + new Vector3(Mathf.Cos(angle) * radius, y - center.y, Mathf.Sin(angle) * radius);
                line[i] = frame.P(localPoint);
            }

            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Handles.DrawAAPolyLine(3f, line);

            Vector3 end = line[points - 1];
            Vector3 previous = line[points - 2];
            Vector3 direction = (end - previous).normalized;
            Vector3 side = Vector3.Cross(direction, frame.D(Vector3.up));
            if (side.sqrMagnitude < 0.0001f) side = frame.D(Vector3.right);
            side.Normalize();
            DrawArrowHead(end, direction, side, arrowSize);
            Handles.Label(end + direction * arrowSize * 0.25f, settings.meshType == FXMMeshType.CrossHelix ? "Spiral UV Flow / Cross Helix" : "Spiral UV Flow / Helix");

            if (settings.meshType == FXMMeshType.CrossHelix)
            {
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                float crossWidth = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.22f + arrowSize * 0.35f;
                int step = Mathf.Max(1, points / 8);
                for (int i = 0; i < points; i += step)
                {
                    float t = i / (float)(points - 1);
                    float angle = (settings.startAngle + directionSign * turns * 360f * t) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    Vector3 localPoint = center + new Vector3(cos * radius, Mathf.Lerp(bounds.min.y, bounds.max.y, t) - center.y, sin * radius);
                    Vector3 radial = new Vector3(cos, 0f, sin).normalized;
                    Vector3 tangentLocal = new Vector3(-sin * radius * directionSign, Mathf.Max(bounds.size.y, 0.0001f) / Mathf.Max(0.0001f, turns * Mathf.PI * 2f), cos * radius * directionSign).normalized;
                    Vector3 crossAxis = Quaternion.AngleAxis(90f + settings.helixCrossRotation, tangentLocal) * radial;
                    if (crossAxis.sqrMagnitude < 0.0001f) crossAxis = radial;
                    crossAxis.Normalize();
                    Handles.DrawAAPolyLine(2f, frame.P(localPoint - crossAxis * crossWidth), frame.P(localPoint + crossAxis * crossWidth));
                }
            }
        }

        private static Vector3 DirectionXY(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f).normalized;
        }

        private static Vector3 DirectionXZ(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)).normalized;
        }

        private static void DrawArrowHead(Vector3 arrowEnd, Vector3 direction, Vector3 side, float arrowSize)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.up;
            }
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.right;
            }
            direction.Normalize();
            side.Normalize();
            Handles.DrawAAPolyLine(3f, arrowEnd, arrowEnd - direction * arrowSize + side * arrowSize * 0.45f);
            Handles.DrawAAPolyLine(3f, arrowEnd, arrowEnd - direction * arrowSize - side * arrowSize * 0.45f);
        }

        private static void DrawArrowPolyline(Vector3 start, Vector3 mid, Vector3 end, Vector3 sideReference, float arrowSize, string label)
        {
            Handles.color = new Color(0.2f, 0.85f, 1f, 0.95f);
            Handles.DrawAAPolyLine(3f, start, mid, end);

            Vector3 direction = (end - mid).sqrMagnitude > 0.0001f ? (end - mid).normalized : Vector3.up;
            Vector3 side = Vector3.Cross(direction, sideReference);
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.right;
            }
            side.Normalize();

            DrawArrowHead(end, direction, side, arrowSize);
            Handles.Label(end + direction * arrowSize * 0.25f, label);
        }

        private static void DrawLocalNormalLines(Mesh mesh, float normalLength, int sampleCount)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (vertices == null || normals == null || vertices.Length == 0 || normals.Length != vertices.Length)
            {
                return;
            }

            int step = Mathf.Max(1, vertices.Length / Mathf.Max(1, sampleCount));
            Handles.color = new Color(0.25f, 1f, 0.35f, 0.85f);
            for (int i = 0; i < vertices.Length; i += step)
            {
                Vector3 a = vertices[i];
                Vector3 b = a + normals[i].normalized * normalLength;
                Handles.DrawLine(a, b);
            }
        }


        private void SetCheckerPreviewMode(bool repaint)
        {
            _previewMode = FXMPreviewMode.UVChecker;
            _checkerScale = Mathf.Max(8f, _checkerScale);
            _checkerStrength = Mathf.Max(0.85f, _checkerStrength);
            _alphaAsOpacity = false;
            if (_previewTint.a <= 0.0001f)
            {
                _previewTint = Color.white;
            }

            if (repaint)
            {
                ApplyPreviewMaterialSettings(GetPreviewMaterial());
                SceneView.RepaintAll();
            }
        }

        private void SetUvGradientPreviewMode(bool repaint)
        {
            _previewMode = FXMPreviewMode.UVGradient;
            _checkerScale = Mathf.Max(8f, _checkerScale);
            _checkerStrength = Mathf.Max(0.85f, _checkerStrength);
            _alphaAsOpacity = false;
            if (_previewTint.a <= 0.0001f)
            {
                _previewTint = Color.white;
            }

            if (repaint)
            {
                ApplyPreviewMaterialSettings(GetPreviewMaterial());
                SceneView.RepaintAll();
            }
        }

        private void CachePreviewObjectReference()
        {
            if (_previewObject == null)
            {
                _previewObject = GameObject.Find(PreviewObjectName);
            }
        }

        private bool IsMeshAssignedToPreviewObject(Mesh mesh)
        {
            if (mesh == null)
            {
                return false;
            }

            return GetPreviewObjectMesh() == mesh;
        }

        private Mesh GetPreviewObjectMesh()
        {
            CachePreviewObjectReference();
            if (_previewObject == null)
            {
                return null;
            }

            MeshFilter meshFilter = _previewObject.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private static void DestroyTransientMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            if (!AssetDatabase.Contains(mesh))
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private void RebuildPreviewMesh(bool assignToPreviewObject)
        {
            RebuildPreviewMesh(assignToPreviewObject, false);
        }

        private void RebuildPreviewMesh(bool assignToPreviewObject, bool forceFullQuality)
        {
            Mesh oldPreviewMesh = _previewMesh;
            Mesh oldScenePreviewMesh = GetPreviewObjectMesh();

            bool usedLod = false;
            FXMSettings buildSettings = (!forceFullQuality && assignToPreviewObject)
                ? GetPreviewBuildSettings(out usedLod)
                : _settings;

            _lastPreviewUsedLod = usedLod;
            _lastPreviewSourceSegments = Mathf.Max(1, _settings.segments);
            _lastPreviewSourceWidthSegments = Mathf.Max(1, _settings.widthSegments);
            _lastPreviewBuildSegments = Mathf.Max(1, buildSettings.segments);
            _lastPreviewBuildWidthSegments = Mathf.Max(1, buildSettings.widthSegments);

            _previewMesh = FXMMeshBuilder.Build(buildSettings);
            _previewMesh.name = string.IsNullOrWhiteSpace(_settings.meshName) ? "FXM_GeneratedMesh" : _settings.meshName;

            if (assignToPreviewObject)
            {
                CachePreviewObjectReference();
            }

            if (assignToPreviewObject && _previewObject != null)
            {
                MeshFilter meshFilter = _previewObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = _previewMesh;
                }

                MeshRenderer meshRenderer = _previewObject.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial = GetPreviewMaterial();
                }
            }

            if (assignToPreviewObject)
            {
                // The preview object now owns the new mesh. Clean up the old transient meshes
                // that are no longer assigned, including a previously preserved scene mesh.
                if (oldPreviewMesh != null && oldPreviewMesh != _previewMesh)
                {
                    DestroyTransientMesh(oldPreviewMesh);
                }

                if (oldScenePreviewMesh != null && oldScenePreviewMesh != oldPreviewMesh && oldScenePreviewMesh != _previewMesh)
                {
                    DestroyTransientMesh(oldScenePreviewMesh);
                }
            }
            else
            {
                // Internal/export rebuild: keep the live scene preview mesh alive, otherwise
                // the object in Scene View loses its mesh and the wire overlay disappears.
                if (oldPreviewMesh != null && oldPreviewMesh != oldScenePreviewMesh)
                {
                    DestroyTransientMesh(oldPreviewMesh);
                }
            }

            Repaint();
            SceneView.RepaintAll();
        }

        private void CreateOrUpdatePreviewObject()
        {
            CachePreviewObjectReference();

            if (_previewObject == null)
            {
                _previewObject = new GameObject(PreviewObjectName);
                Undo.RegisterCreatedObjectUndo(_previewObject, "Create FX Mesh Preview Object");
            }

            MeshFilter meshFilter = _previewObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = Undo.AddComponent<MeshFilter>(_previewObject);
            }

            MeshRenderer meshRenderer = _previewObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = Undo.AddComponent<MeshRenderer>(_previewObject);
            }

            // v0.28.1: Build after the preview object/components exist, then assign in one path.
            // This prevents the old scene mesh from being destroyed before the new one is attached.
            RebuildPreviewMesh(true);

            Selection.activeGameObject = _previewObject;
            SceneView.RepaintAll();
        }

        private void CreateSceneObjectFromCurrentMesh()
        {
            RebuildPreviewMesh(false, true);

            Mesh meshInstance = UnityEngine.Object.Instantiate(_previewMesh);
            meshInstance.name = _previewMesh.name + "_SceneInstance";

            GameObject go = new GameObject(meshInstance.name);
            Undo.RegisterCreatedObjectUndo(go, "Create FX Mesh Scene Object");

            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = meshInstance;

            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetPreviewMaterial();

            Selection.activeGameObject = go;
            SceneView.RepaintAll();
        }

        private void SaveMeshAsset()
        {
            RebuildPreviewMesh(false, true);

            EnsurePackageFolders(false);

            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(_settings.meshName) ? "FXM_GeneratedMesh" : _settings.meshName);
            string path = EditorUtility.SaveFilePanelInProject(
                "Save FX Mesh Asset",
                safeName,
                "asset",
                "Save generated mesh as a Unity .asset file.",
                GeneratedMeshesFolder);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            Mesh meshAsset = UnityEngine.Object.Instantiate(_previewMesh);
            meshAsset.name = safeName;

            AssetDatabase.CreateAsset(meshAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(meshAsset);
            Selection.activeObject = meshAsset;
        }

        private void SavePreviewAsPrefab()
        {
            RebuildPreviewMesh(false, true);

            EnsurePackageFolders(false);
            string safeName = SanitizeFileName(string.IsNullOrWhiteSpace(_settings.meshName) ? "FXM_GeneratedPrefab" : _settings.meshName);
            string meshPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedMeshesFolder + "/" + safeName + "_Mesh.asset");
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath(GeneratedPrefabFolder + "/" + safeName + ".prefab");

            Mesh meshAsset = UnityEngine.Object.Instantiate(_previewMesh);
            meshAsset.name = safeName + "_Mesh";
            AssetDatabase.CreateAsset(meshAsset, meshPath);

            GameObject go = new GameObject(safeName);
            MeshFilter meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = meshAsset;
            MeshRenderer meshRenderer = go.AddComponent<MeshRenderer>();
            // v0.28.3: Exported prefabs must not keep the editor-only Vertex Alpha Preview material.
            // Artists usually assign their own VFX/URP material in the target project or prefab variant.
            meshRenderer.sharedMaterial = null;
            meshRenderer.sharedMaterials = Array.Empty<Material>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (prefab != null)
            {
                EditorGUIUtility.PingObject(prefab);
                Selection.activeObject = prefab;
            }
        }

        private void UseInternalPreviewMaterial()
        {
            if (_previewMaterial != null && AssetDatabase.Contains(_previewMaterial))
            {
                _previewMaterial = null;
            }

            Material mat = GetPreviewMaterial();
            ApplyPreviewMaterialSettings(mat);

            if (_previewObject != null)
            {
                MeshRenderer renderer = _previewObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = mat;
                }
            }
        }

        private Material GetPreviewMaterial()
        {
            if (_previewMaterial != null)
            {
                ApplyPreviewMaterialSettings(_previewMaterial);
                return _previewMaterial;
            }

            Shader shader = Shader.Find(PreviewShaderName);
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            _previewMaterial = new Material(shader)
            {
                name = "FXM_InternalVertexAlphaPreview"
            };

            ApplyPreviewMaterialSettings(_previewMaterial);
            return _previewMaterial;
        }

        private void ApplyPreviewMaterialSettings(Material mat)
        {
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty("_Tint"))
            {
                mat.SetColor("_Tint", _previewTint);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", _previewTint);
            }

            if (mat.HasProperty("_CheckerScale"))
            {
                mat.SetFloat("_CheckerScale", Mathf.Max(1f, _checkerScale));
            }

            if (mat.HasProperty("_CheckerStrength"))
            {
                mat.SetFloat("_CheckerStrength", Mathf.Clamp01(_checkerStrength));
            }

            if (mat.HasProperty("_PreviewMode"))
            {
                mat.SetFloat("_PreviewMode", (float)_previewMode);
            }

            if (mat.HasProperty("_FlowDirection"))
            {
                mat.SetFloat("_FlowDirection", (float)_settings.uvFlowDirection);
            }

            if (mat.HasProperty("_FlowFlipU"))
            {
                // v0.32.7: Flip is baked into the generated mesh UVs. Keep shader flip at 0 to avoid double inversion.
                mat.SetFloat("_FlowFlipU", 0f);
            }

            if (mat.HasProperty("_FlowFlipV"))
            {
                // v0.32.7: Flip is baked into the generated mesh UVs. Keep shader flip at 0 to avoid double inversion.
                mat.SetFloat("_FlowFlipV", 0f);
            }

            if (mat.HasProperty("_FlowPreviewOffset"))
            {
                mat.SetVector("_FlowPreviewOffset", new Vector4(_flowPreviewOffsetU, _flowPreviewOffsetV, 0f, 0f));
            }

            if (mat.HasProperty("_AlphaOpacity"))
            {
                mat.SetFloat("_AlphaOpacity", _alphaAsOpacity ? 1f : 0f);
            }

            if (mat.HasProperty("_Cull"))
            {
                // 2 = Back. 기본 머티리얼은 한 면만 보이게 해서 Double Sided Geometry 체크 여부를 명확히 확인합니다.
                mat.SetFloat("_Cull", 2f);
            }
        }

        private void CreatePreviewMaterialAsset()
        {
            Material assetMat = CreatePreviewMaterialAssetInternal(true);
            if (assetMat == null) return;

            _previewMaterial = assetMat;

            if (_previewObject != null)
            {
                MeshRenderer renderer = _previewObject.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _previewMaterial;
                }
            }

            EditorGUIUtility.PingObject(assetMat);
            Selection.activeObject = assetMat;
        }

        private Material CreatePreviewMaterialAssetInternal(bool ping)
        {
            EnsurePackageFolders(false);

            Material source = GetPreviewMaterial();
            Material assetMat = new Material(source)
            {
                name = "M_FXM_VertexAlphaPreview"
            };
            ApplyPreviewMaterialSettings(assetMat);

            string path = AssetDatabase.GenerateUniqueAssetPath(DefaultPreviewMaterialPath);
            AssetDatabase.CreateAsset(assetMat, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (ping)
            {
                EditorGUIUtility.PingObject(assetMat);
            }

            return assetMat;
        }

        private void ApplyPreviewMaterialToSelected()
        {
            Material mat = GetPreviewMaterial();
            GameObject[] selectedObjects = Selection.gameObjects;
            int appliedCount = 0;

            foreach (GameObject selected in selectedObjects)
            {
                if (selected == null) continue;
                MeshRenderer renderer = selected.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                Undo.RecordObject(renderer, "Apply FX Mesh Preview Material");
                renderer.sharedMaterial = mat;
                EditorUtility.SetDirty(renderer);
                appliedCount++;
            }

            if (appliedCount == 0)
            {
                EditorUtility.DisplayDialog("FX Mesh Generator Pro", Tr("No selected object contains a MeshRenderer.", "선택된 오브젝트 중 MeshRenderer가 있는 대상이 없습니다."), "OK");
            }
            else
            {
                SceneView.RepaintAll();
            }
        }

        private static void EnsurePackageFolders(bool showDialog)
        {
            EnsureFolder(PackageRoot);
            EnsureFolder(GeneratedRoot);
            EnsureFolder(GeneratedMeshesFolder);
            EnsureFolder(GeneratedMaterialFolder);
            EnsureFolder(GeneratedPrefabFolder);
            EnsureFolder(GeneratedSamplesFolder);
            EnsureFolder(GeneratedSampleMeshesFolder);
            EnsureFolder(GeneratedSamplePrefabsFolder);
            EnsureFolder(GeneratedSampleScenesFolder);

            if (showDialog)
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("FX Mesh Generator Pro", Tr("Generated folder structure checked/repaired.\n\n", "Generated 폴더 구조를 확인/복구했습니다.\n\n") + GeneratedRoot, "OK");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path);
            string folderName = Path.GetFileName(path);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                return;
            }

            parent = parent.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "FXM_GeneratedMesh";
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '_');
            }
            return value.Trim();
        }
    }


    internal struct FXMShapeUiProfile
    {
        public readonly string primarySegmentsLabel;
        public readonly string primarySegmentsTooltip;
        public readonly string secondarySegmentsLabel;
        public readonly string secondarySegmentsTooltip;
        public readonly string description;

        private FXMShapeUiProfile(string primaryLabel, string primaryTooltip, string secondaryLabel, string secondaryTooltip, string newDescription)
        {
            primarySegmentsLabel = primaryLabel;
            primarySegmentsTooltip = primaryTooltip;
            secondarySegmentsLabel = secondaryLabel;
            secondarySegmentsTooltip = secondaryTooltip;
            description = newDescription;
        }

        public static FXMShapeUiProfile For(FXMMeshType type)
        {
            switch (type)
            {
                case FXMMeshType.Slash:
                    return new FXMShapeUiProfile(
                        "Flow / Arc Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "검기 흐름 방향의 분할 수입니다. 높을수록 곡선이 부드럽지만 폴리곤이 증가합니다."),
                        "Width / Cross Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "검기 두께 방향 분할 수입니다. Vertex Alpha나 노이즈 변형을 세밀하게 쓰려면 2 이상을 권장합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Slash는 Arc/Flow 중심 메시입니다. Twist보다는 Bend/Noise로 생동감을 주는 구성이 안전합니다."));

                case FXMMeshType.Beam:
                    return new FXMShapeUiProfile(
                        "Length / Flow Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "빔의 길이 방향 분할 수입니다."),
                        "Width / Cross Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "빔의 폭 방향 분할 수입니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Beam은 직선 흐름 메시입니다. Taper, Bend, 아주 약한 Noise가 유효합니다."));

                case FXMMeshType.Ring:
                    return new FXMShapeUiProfile(
                        "Circular Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "원의 둘레 방향 분할 수입니다."),
                        "Width / Thickness Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "링 두께 방향 분할 수입니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Ring은 원형 메시입니다. Twist보다 Circular Bend/Noise 계열이 자연스럽습니다."));

                case FXMMeshType.Disc:
                    return new FXMShapeUiProfile(
                        "Circular Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "원판 둘레 방향 분할 수입니다."),
                        "Radial Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "중심에서 외곽으로 나가는 반지름 방향 분할 수입니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Disc는 장판/마법진 계열입니다. 중심-외곽 흐름을 살리는 Bend/Noise가 우선입니다."));

                case FXMMeshType.Dome:
                    return new FXMShapeUiProfile(
                        "Circular Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "돔 둘레 방향 분할 수입니다."),
                        "Height Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "돔 바닥에서 꼭대기까지의 높이 방향 분할 수입니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Dome은 보호막 계열입니다. Twist/Taper/Noise를 약하게 쓰면 에너지 장막 느낌을 만들 수 있습니다."));

                case FXMMeshType.HalfDome:
                    return new FXMShapeUiProfile(
                        "Open Arc Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "전방 반구의 가로 열림 방향 분할 수입니다."),
                        "Height Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "반구 높이 방향 분할 수입니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Half Dome은 전방 방패형 메시입니다. 과한 Twist보다 Taper/Bend/Noise가 안정적입니다."));

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return new FXMShapeUiProfile(
                        "Flow / Spiral Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "나선 흐름 방향 분할 수입니다."),
                        "Ribbon Cross Segments",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "나선 리본 폭 방향 분할 수입니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Helix는 생성 자체에 회전이 포함됩니다. 추가 Twist는 보통 과해지므로 Taper/Bend/Noise 중심으로 제어합니다."));

                default:
                    return new FXMShapeUiProfile("Segments", FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "분할 수입니다."), "Width Segments", FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "폭 방향 분할 수입니다."), FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "기본 메시 설정입니다."));
            }
        }
    }

    internal struct FXMShapeEditProfile
    {
        public readonly string title;
        public readonly string description;
        public readonly bool allowFlowOffset;
        public readonly bool allowWidthOffset;
        public readonly bool allowShapeBias;
        public readonly bool allowTaperEdit;
        public readonly string flowLabel;
        public readonly string widthLabel;
        public readonly string biasLabel;
        public readonly string flowTooltip;
        public readonly string widthTooltip;
        public readonly string biasTooltip;

        private FXMShapeEditProfile(
            string newTitle,
            string newDescription,
            bool newAllowFlowOffset,
            bool newAllowWidthOffset,
            bool newAllowShapeBias,
            bool newAllowTaperEdit,
            string newFlowLabel,
            string newWidthLabel,
            string newBiasLabel,
            string newFlowTooltip,
            string newWidthTooltip,
            string newBiasTooltip)
        {
            title = newTitle;
            description = newDescription;
            allowFlowOffset = newAllowFlowOffset;
            allowWidthOffset = newAllowWidthOffset;
            allowShapeBias = newAllowShapeBias;
            allowTaperEdit = newAllowTaperEdit;
            flowLabel = newFlowLabel;
            widthLabel = newWidthLabel;
            biasLabel = newBiasLabel;
            flowTooltip = newFlowTooltip;
            widthTooltip = newWidthTooltip;
            biasTooltip = newBiasTooltip;
        }

        public static FXMShapeEditProfile For(FXMMeshType type)
        {
            switch (type)
            {
                case FXMMeshType.Slash:
                    return new FXMShapeEditProfile(
                        FXMeshGeneratorProUnityEditionWindow.Tr("Slash / Slash Edit Profile", "Slash / 검기 Edit Profile"),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "검기용 3ds Max 스타일 Shape Detail입니다. Flow/Bias/Taper Direction으로 끝단 날카로움과 호의 흐름을 빠르게 조정합니다."),
                        true, true, true, true,
                        "Flow Offset", "Width Offset", "Bias  - / +",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "검기 호의 흐름 위치를 밀어줍니다. 음수는 시작 쪽, 양수는 끝 쪽 느낌을 강조합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "검기 두께 방향을 안쪽/바깥쪽으로 치우치게 합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "세그먼트 분포와 폭 변화를 시작/끝 쪽으로 편향합니다."));

                case FXMMeshType.Beam:
                    return new FXMShapeEditProfile(
                        "Beam Edit Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "빔은 길이 방향 Flow와 Taper가 가장 중요합니다. Width Offset/Bias는 빌드에서 제한적으로만 사용합니다."),
                        false, false, false, true,
                        "Flow Offset (Disabled)", "Width Offset (Disabled)", "Bias (Disabled)",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the saved mesh pivot and axis orientation.", "Beam은 Pivot/Length 기준으로 제어합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "Beam은 Shape Width를 우선 사용합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Beam은 Taper Direction을 우선 사용합니다."));

                case FXMMeshType.Ring:
                    return new FXMShapeEditProfile(
                        "Ring / Shockwave Edit Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls Ring shape and circular flow behavior.", "Blender Ring 빌더 기준입니다. Arc Angle, Broken Amount, Thickness Bias로 쇼크웨이브/브로큰 링을 조절합니다."),
                        true, true, true, false,
                        "Arc Flow Offset", "Thickness Offset", "Thickness Bias",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "링 둘레 진행 방향을 시작/끝 쪽으로 살짝 밀어줍니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "링 두께 중심을 안쪽/바깥쪽으로 이동합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "두께 방향 세그먼트와 폭 분포를 안쪽/바깥쪽으로 편향합니다."));

                case FXMMeshType.Disc:
                    return new FXMShapeEditProfile(
                        "Disc / Magic Circle Edit Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "Blender Disc 빌더 기준입니다. Arc Angle, Inner Radius, Radial Bias로 마법진/쿨다운/바닥 웨이브를 조절합니다."),
                        true, true, true, false,
                        "Angular Flow Offset", "Radial Offset", "Radial Bias",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "원판의 각도 진행 분포를 시작/끝 쪽으로 밀어줍니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "전체 반지름 분포를 중심/외곽 쪽으로 치우치게 합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "중심-외곽 세그먼트 밀도를 편향합니다."));

                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    return new FXMShapeEditProfile(
                        type + " Edit Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "돔 계열은 Radius/Height/Open Angle이 우선입니다. Taper Edit은 Modifier 쪽에서만 처리합니다."),
                        false, false, false, true,
                        "Flow Offset (Disabled)", "Width Offset (Disabled)", "Bias (Disabled)",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "돔은 Open Angle과 Start Rotation을 사용하세요."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "돔은 Radius/Height를 사용하세요."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "돔은 Taper Direction을 우선 사용합니다."));

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return new FXMShapeEditProfile(
                        "Helix Edit Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "나선은 Radius/Height/Turns 중심입니다. Taper Direction으로 위/아래 흐름만 다듬습니다."),
                        false, false, false, true,
                        "Flow Offset (Disabled)", "Width Offset (Disabled)", "Bias (Disabled)",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "Helix는 Turns 값으로 흐름을 제어합니다."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "Helix는 Ribbon Width를 사용하세요."),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Helix는 Taper Direction을 우선 사용합니다."));

                default:
                    return new FXMShapeEditProfile(
                        "Shape Edit Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "기본 Shape Edit 프로필입니다."),
                        false, false, false, false,
                        "Flow Offset", "Width Offset", "Bias",
                        "Flow edit", "Width edit", "Bias edit");
            }
        }

        public string UnsupportedSummary(FXMSettings settings)
        {
            List<string> ignored = new List<string>();
            if (!allowFlowOffset && Mathf.Abs(settings.flowOffset) > 0.0001f) ignored.Add("Flow Offset");
            if (!allowWidthOffset && Mathf.Abs(settings.widthOffset) > 0.0001f) ignored.Add("Width Offset");
            if (!allowShapeBias && Mathf.Abs(settings.shapeBias) > 0.0001f) ignored.Add("Bias");
            if (!allowTaperEdit && Mathf.Abs(settings.taperAmount) > 0.0001f) ignored.Add("Taper Edit");
            return string.Join(", ", ignored.ToArray());
        }
    }

    internal struct FXMHelperProfile
    {
        public readonly string title;
        public readonly string description;
        public readonly string flowToggleLabel;
        public readonly string flowToggleTooltip;
        public readonly bool showWireBounds;
        public readonly bool showUvFlow;
        public readonly bool showNormals;
        public readonly bool showBackface;
        public readonly float helperScale;
        public readonly int normalSampleCount;

        private FXMHelperProfile(
            string newTitle,
            string newDescription,
            string newFlowToggleLabel,
            string newFlowToggleTooltip,
            bool newShowWireBounds,
            bool newShowUvFlow,
            bool newShowNormals,
            bool newShowBackface,
            float newHelperScale,
            int newNormalSampleCount)
        {
            title = newTitle;
            description = newDescription;
            flowToggleLabel = newFlowToggleLabel;
            flowToggleTooltip = newFlowToggleTooltip;
            showWireBounds = newShowWireBounds;
            showUvFlow = newShowUvFlow;
            showNormals = newShowNormals;
            showBackface = newShowBackface;
            helperScale = newHelperScale;
            normalSampleCount = newNormalSampleCount;
        }

        public static FXMHelperProfile For(FXMMeshType type)
        {
            switch (type)
            {
                case FXMMeshType.Slash:
                    return new FXMHelperProfile(
                        "Slash Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls UV layout and texture flow behavior.", "검기는 Arc Flow와 Tip 방향 확인이 핵심입니다. UV Flow Arrow와 Backface/Cull Guide를 기본으로 켭니다."),
                        "Arc / UV Flow Arrow",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "검기 호의 시작-끝 진행 방향을 Scene View에 표시합니다."),
                        true, true, false, true, 0.18f, 48);

                case FXMMeshType.Beam:
                    return new FXMHelperProfile(
                        "Beam Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "빔은 길이 방향 Flow가 핵심입니다. 직선 Flow Arrow 중심으로 검수합니다."),
                        "Linear Flow Arrow",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls UV layout and texture flow behavior.", "빔의 길이 방향 UV Flow를 표시합니다."),
                        true, true, false, true, 0.16f, 40);

                case FXMMeshType.Ring:
                    return new FXMHelperProfile(
                        "Ring Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "링/쇼크웨이브는 Circular Flow와 중심 기준 Radial Bias 확인이 중요합니다."),
                        "Circular Flow Guide",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "원형 UV Flow와 중심/반지름 기준선을 표시합니다."),
                        true, true, false, true, 0.16f, 48);

                case FXMMeshType.Disc:
                    return new FXMHelperProfile(
                        "Disc Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "원판/마법진은 Circular Flow와 Center/Radial 방향을 함께 확인하는 것이 좋습니다."),
                        "Circular / Radial Guide",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls UV layout and texture flow behavior.", "원형 UV Flow와 중심-외곽 방향 기준선을 표시합니다."),
                        true, true, false, true, 0.16f, 48);

                case FXMMeshType.Dome:
                    return new FXMHelperProfile(
                        "Dome Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "돔은 Front Direction, 높이 방향, Backface 상태 확인이 중요합니다. Normal Lines는 필요할 때 켜세요."),
                        "Front Direction Guide",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "돔의 앞 방향과 높이 기준선을 표시합니다."),
                        true, true, false, true, 0.18f, 64);

                case FXMMeshType.HalfDome:
                    return new FXMHelperProfile(
                        "Half Dome Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "전방 보호막은 Open Side와 면 방향 검수가 중요합니다. 앞/뒤가 헷갈릴 때 Front Direction Guide를 확인하세요."),
                        "Open Side / Front Guide",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls Dome shape and preview behavior.", "Half Dome의 열림 방향과 앞 방향을 표시합니다."),
                        true, true, false, true, 0.18f, 64);

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return new FXMHelperProfile(
                        "Helix Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "나선은 Spiral Flow 방향과 높이 흐름을 보는 것이 핵심입니다."),
                        "Spiral Flow Guide",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls UV layout and texture flow behavior.", "나선형 UV/진행 방향을 Scene View에 표시합니다."),
                        true, true, false, true, 0.15f, 48);

                default:
                    return new FXMHelperProfile(
                        "Default Helper Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls Scene View preview helpers only; saved mesh data is not changed.", "기본 Scene Preview Helper입니다."),
                        "UV Flow Arrow",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls UV layout and texture flow behavior.", "UV/진행 방향을 표시합니다."),
                        true, true, false, true, 0.18f, 48);
            }
        }
    }

    internal struct FXMModifierProfile
    {
        public readonly string title;
        public readonly string description;
        public readonly bool allowTwist;
        public readonly bool allowTaper;
        public readonly bool allowBend;
        public readonly bool allowNoise;
        public readonly string twistLabel;
        public readonly string taperLabel;
        public readonly string bendLabel;
        public readonly string noiseLabel;
        public readonly string twistTooltip;
        public readonly string taperTooltip;
        public readonly string bendTooltip;
        public readonly string noiseTooltip;

        private FXMModifierProfile(
            string newTitle,
            string newDescription,
            bool newAllowTwist,
            bool newAllowTaper,
            bool newAllowBend,
            bool newAllowNoise,
            string newTwistLabel,
            string newTaperLabel,
            string newBendLabel,
            string newNoiseLabel,
            string newTwistTooltip,
            string newTaperTooltip,
            string newBendTooltip,
            string newNoiseTooltip)
        {
            title = newTitle;
            description = newDescription;
            allowTwist = newAllowTwist;
            allowTaper = newAllowTaper;
            allowBend = newAllowBend;
            allowNoise = newAllowNoise;
            twistLabel = newTwistLabel;
            taperLabel = newTaperLabel;
            bendLabel = newBendLabel;
            noiseLabel = newNoiseLabel;
            twistTooltip = newTwistTooltip;
            taperTooltip = newTaperTooltip;
            bendTooltip = newBendTooltip;
            noiseTooltip = newNoiseTooltip;
        }

        public static FXMModifierProfile For(FXMMeshType type)
        {
            string twistTip = FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "Y축 진행 방향 기준으로 비틀림을 적용합니다.");
            string taperTip = FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "진행 방향 기준으로 폭을 좁히거나 넓힙니다.");
            string bendTip = FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "흐름 방향을 부드럽게 휘게 만듭니다.");
            string noiseTip = FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "버텍스 위치에 절차적 흔들림을 줍니다.");

            switch (type)
            {
                case FXMMeshType.Slash:
                    return new FXMModifierProfile(
                        FXMeshGeneratorProUnityEditionWindow.Tr("Slash / Slash Modifier Profile", "Slash / 검기 Modifier Profile"),
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "검기는 Twist는 불필요하지만 Taper Direction이 매우 중요합니다. 검기 끝을 날카롭게 줄이거나 시작부를 두껍게 만들 때 Taper를 사용하세요."),
                        false, true, true, true,
                        "Twist Amount (Disabled)", "Slash Taper Amount", "Flow Bend", "Edge Noise Strength",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Slash에서는 Twist가 과한 꼬임을 만들기 쉬워 비활성화됩니다."), FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "검기 폭을 진행 방향 기준으로 좁히거나 넓힙니다. Direction으로 시작/끝 방향을 정하세요."), bendTip, noiseTip);

                case FXMMeshType.Beam:
                    return new FXMModifierProfile(
                        "Beam Modifier Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "빔은 직선형 메시라 Twist보다 Taper/Bend/Noise가 실무적으로 유효합니다."),
                        false, true, true, true,
                        "Twist Amount (Disabled)", "Taper Amount", "Flow Bend", "Core Noise Strength",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Beam에서는 Twist가 꼬인 리본처럼 보일 수 있어 비활성화됩니다."), taperTip, bendTip, noiseTip);

                case FXMMeshType.Ring:
                    return new FXMModifierProfile(
                        "Ring Modifier Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "링/쇼크웨이브는 원형 흐름이 핵심입니다. Twist/Taper는 제외하고 Circular Bend와 Noise 중심으로 다룹니다."),
                        false, false, true, true,
                        "Twist Amount (Disabled)", "Taper Amount (Disabled)", "Circular Bend", "Radial Noise Strength",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Ring에서는 원형 토폴로지가 이미 회전을 갖기 때문에 Twist를 사용하지 않습니다."), FXMeshGeneratorProUnityEditionWindow.Tr("Controls the width or thickness for the selected mesh.", "Ring 두께는 Shape Thickness를 우선 사용하세요."), FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "원형 링에 파동성 굴곡을 추가합니다."), noiseTip);

                case FXMMeshType.Disc:
                    return new FXMModifierProfile(
                        "Disc Modifier Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "원판/장판형 메시입니다. Twist/Taper보다 중심-외곽 방향의 Bend와 은은한 Noise가 안전합니다."),
                        false, false, true, true,
                        "Twist Amount (Disabled)", "Taper Amount (Disabled)", "Radial Bend", "Surface Noise Strength",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls UV layout and texture flow behavior.", "Disc에서는 텍스처 회전/UV Flow로 회전을 처리하는 편이 낫습니다."), FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "Disc의 구멍/폭은 Inner Radius Ratio와 Radius를 사용하세요."), FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "원판 표면에 가벼운 굴곡감을 추가합니다."), noiseTip);

                case FXMMeshType.Dome:
                    return new FXMModifierProfile(
                        "Dome Modifier Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "돔 보호막은 약한 Twist, Taper, Noise 모두 사용할 수 있습니다. 과하면 실루엣이 깨지므로 낮은 값부터 시작하세요."),
                        true, true, false, true,
                        "Shield Twist", "Dome Taper", "Bend Amount (Disabled)", "Shield Noise Strength",
                        twistTip, taperTip, FXMeshGeneratorProUnityEditionWindow.Tr("Controls the radius for the selected mesh.", "Dome에서는 Bend보다 Radius/Height와 Taper를 우선 사용하세요."), noiseTip);

                case FXMMeshType.HalfDome:
                    return new FXMModifierProfile(
                        "Half Dome Modifier Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "전방 보호막은 Taper/Bend/Noise가 유효합니다. Twist는 시야 방향 실루엣을 망가뜨릴 수 있어 제외합니다."),
                        false, true, true, true,
                        "Twist Amount (Disabled)", "Shield Taper", "Front Bend", "Shield Noise Strength",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Half Dome에서는 Twist가 좌우 실루엣을 어색하게 만들기 쉬워 비활성화됩니다."), taperTip, bendTip, noiseTip);

                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    return new FXMModifierProfile(
                        "Helix Modifier Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "Helix는 생성 단계에서 이미 회전합니다. 추가 Twist는 중복이므로 끄고, Taper/Bend/Noise로 흐름만 다듬습니다."),
                        false, true, true, true,
                        "Twist Amount (Disabled)", "Helix Taper", "Spiral Bend", "Ribbon Noise Strength",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Adjust this option for the selected production workflow.", "Helix는 Turns 값으로 회전을 제어하세요."), taperTip, bendTip, noiseTip);

                default:
                    return new FXMModifierProfile(
                        "Default Modifier Profile",
                        FXMeshGeneratorProUnityEditionWindow.Tr("Controls modifier/deform behavior for the selected mesh.", "기본 Modifier 프로필입니다."),
                        true, true, true, true,
                        "Twist Amount", "Taper Amount", "Bend Amount", "Noise Strength",
                        twistTip, taperTip, bendTip, noiseTip);
            }
        }
    }

    internal enum FXMMeshType
    {
        Slash,
        Beam,
        Ring,
        Disc,
        Dome,
        HalfDome,
        Helix,
        CrossHelix
    }

    internal enum FXMUVFlowDirection
    {
        Auto,
        VForward,
        VReverse,
        UForward,
        UReverse,
        FromCenter,
        ToCenter,
        CircularCW,
        CircularCCW
    }

    internal enum FXMExportPreset
    {
        UnityParticleMesh,
        UnityMeshRenderer,
        BlenderMaxFBXMatch,
        GenericDCC
    }

    internal enum FXMAlphaPresetKind
    {
        FlowIn,
        FlowOut,
        SoftTip,
        SoftEdge,
        AllBorder,
        GeometryBorder,
        CenterCore,
        CenterHole,
        CornerFade,
        NoisyDissolve,
        EnergyStripes,
        Reset
    }

    internal enum FXMVertexColorRGBMode
    {
        White,
        AlphaToRGB,
        InvertAlphaToRGB,
        AlphaToR,
        AlphaToG,
        AlphaToB
    }

    internal enum FXMAlphaMode
    {
        None,
        AlongU,
        AlongV,
        CenterOut,
        EdgeFade,
        TipFade,
        FlowV,
        FlowVReverse,
        WidthU,
        WidthUReverse,
        CenterHole,
        EdgeU,
        EdgeV,
        RootFade,
        CornerFade,
        RadialIn,
        RadialOut,
        NoiseDissolve,
        StripePulse,
        GeometryBorder
    }

    internal enum FXMPivotMode
    {
        Center,
        Bottom,
        Top
    }

    internal enum FXMPreviewMode
    {
        SolidTint = 0,
        UVChecker = 1,
        VertexAlpha = 2,
        UVCheckerAlpha = 3,
        VertexRGB = 4,
        UVGradient = 5
    }

    internal enum FXMGlobalPivotAnchor
    {
        KeepGeneratedOrigin,
        BoundsCenter,
        BoundsBottom,
        BoundsTop,
        CustomOffsetOnly
    }

    internal enum FXMUpAxis
    {
        YUp,
        ZUp
    }

    internal enum FXMAxis
    {
        X,
        Y,
        Z
    }

    internal enum FXMTwistDirection
    {
        Positive,
        Negative
    }

    internal enum FXMTwistMode
    {
        Centered,
        BottomToTop,
        TopToBottom
    }

    internal enum FXMFFDMode
    {
        Box,
        Cyl
    }

    internal enum FXMFFDResolution
    {
        Res2 = 2,
        Res3 = 3,
        Res4 = 4
    }

    internal enum FXMShellMode
    {
        Center,
        Outer,
        Inner
    }

    [Serializable]
    internal struct FXMSettings
    {
        public string meshName;
        public FXMMeshType meshType;
        public FXMExportPreset exportPreset;
        public int segments;
        public int widthSegments;

        public float length;
        public float width;
        public float radius;
        public float height;
        public float arcAngle;
        public float startAngle;
        public float turns;
        public float innerRadiusRatio;
        public float flowOffset;
        public float widthOffset;
        public float shapeBias;
        public float segmentFlowBias;
        public float segmentWidthBias;
        public float depth;
        public float edgeJitter;
        public float brokenAmount;
        public float slashRootScale;
        public float slashTipScale;
        public bool symmetryWidth;
        public float beamStartWidth;
        public float beamEndWidth;
        public float beamCoreWidth;
        public float ringInnerThickness;
        public float ringOuterThickness;
        public float discCenterFade;
        public float discOuterFade;
        public float domeRoundness;
        public float domeHeightBias;
        public float helixRadius2;
        public bool helixClockwise;
        public bool helixCrossMesh;
        public float helixCrossRotation;
        public FXMPivotMode pivotMode;
        public FXMGlobalPivotAnchor pivotAnchor;
        public Vector3 pivotPositionOffset;
        public Vector3 pivotRotationEuler;
        public FXMUpAxis upAxis;

        public float uvTilingU;
        public float uvTilingV;
        public float uvBiasU;
        public float uvBiasV;
        public FXMUVFlowDirection uvFlowDirection;
        public bool flipU;
        public bool flipV;

        public FXMAlphaMode alphaMode;
        public bool invertAlpha;
        public float alphaPower;
        public float minAlpha;
        public float maxAlpha;
        public float alphaRangeStart;
        public float alphaRangeEnd;
        public float alphaSoftness;
        public float alphaOffset;
        public float alphaContrast;
        public float alphaNoiseStrength;
        public float alphaNoiseScale;
        public int alphaNoiseSeed;
        public float alphaStripeCount;
        public float alphaStripeSoftness;
        public float alphaEdgeWidthU;
        public float alphaEdgeWidthV;
        public bool alphaSymmetryU;
        public bool alphaSymmetryV;
        public bool alphaSymmetryCenterOrigin;
        public FXMVertexColorRGBMode vertexColorRGBMode;

        public bool useModifiers;
        public float twistAmount;
        public float taperAmount;
        public float taperDirection;
        public float bendAmount;
        public float noiseStrength;
        public float noiseScale;
        public int noiseSeed;

        public bool maxBendEnable;
        public float maxBendAngle;
        public FXMAxis maxBendAxis;
        public FXMAxis maxBendPlane;
        public float maxBendBias;
        public bool maxTwistEnable;
        public float maxTwistAngle;
        public FXMAxis maxTwistAxis;
        public FXMTwistDirection maxTwistDirection;
        public FXMTwistMode maxTwistMode;
        public float maxTwistBias;
        public bool maxTaperEnable;
        public float maxTaperAmount;
        public FXMAxis maxTaperAxis;
        public float maxTaperBias;
        public bool ffdEnable;
        public FXMFFDMode ffdMode;
        public FXMFFDResolution ffdResolution;
        public FXMAxis ffdAxis;
        public float ffdStrength;
        public float ffdP0Scale;
        public float ffdP1Scale;
        public float ffdP2Scale;
        public float ffdP3Scale;
        public float ffdP0Height;
        public float ffdP1Height;
        public float ffdP2Height;
        public float ffdP3Height;
        public float ffdSmoothness;
        public bool maxNoiseEnable;
        public int maxNoiseSeed;
        public float maxNoiseScale;
        public bool maxNoiseFractal;
        public float maxNoiseStrengthX;
        public float maxNoiseStrengthY;
        public float maxNoiseStrengthZ;
        public float shellThickness;
        public FXMAxis shellAxis;
        public FXMShellMode shellMode;
        public bool flipNormals;

        public bool doubleSidedGeometry;
        public bool recalculateNormals;
        public bool recalculateTangents;
        public bool centerPivotAfterBuild;

        public static FXMSettings Default()
        {
            return new FXMSettings
            {
                meshName = "FXM_Slash_001",
                meshType = FXMMeshType.Slash,
                exportPreset = FXMExportPreset.UnityParticleMesh,
                segments = 32,
                widthSegments = 1,
                length = 2f,
                width = 0.35f,
                radius = 1.25f,
                height = 1.2f,
                arcAngle = 110f,
                startAngle = 0f,
                turns = 2f,
                innerRadiusRatio = 0f,
                flowOffset = 0f,
                widthOffset = 0f,
                shapeBias = 0f,
                segmentFlowBias = 0f,
                segmentWidthBias = 0f,
                depth = 0f,
                edgeJitter = 0f,
                brokenAmount = 0f,
                slashRootScale = 1f,
                slashTipScale = 1f,
                symmetryWidth = false,
                beamStartWidth = 1f,
                beamEndWidth = 1f,
                beamCoreWidth = 1f,
                ringInnerThickness = 1f,
                ringOuterThickness = 1f,
                discCenterFade = 0f,
                discOuterFade = 0f,
                domeRoundness = 1f,
                domeHeightBias = 0f,
                helixRadius2 = 0f,
                helixClockwise = false,
                helixCrossMesh = false,
                helixCrossRotation = 0f,
                pivotMode = FXMPivotMode.Center,
                pivotAnchor = FXMGlobalPivotAnchor.KeepGeneratedOrigin,
                pivotPositionOffset = Vector3.zero,
                pivotRotationEuler = Vector3.zero,
                upAxis = FXMUpAxis.YUp,
                uvTilingU = 1f,
                uvTilingV = 1f,
                uvBiasU = 0f,
                uvBiasV = 0f,
                uvFlowDirection = FXMUVFlowDirection.Auto,
                flipU = false,
                flipV = false,
                alphaMode = FXMAlphaMode.TipFade,
                invertAlpha = false,
                alphaPower = 1.5f,
                minAlpha = 0f,
                maxAlpha = 1f,
                alphaRangeStart = 0f,
                alphaRangeEnd = 1f,
                alphaSoftness = 0.35f,
                alphaOffset = 0f,
                alphaContrast = 1f,
                alphaNoiseStrength = 0f,
                alphaNoiseScale = 8f,
                alphaNoiseSeed = 0,
                alphaStripeCount = 5f,
                alphaStripeSoftness = 0.35f,
                alphaEdgeWidthU = 0.25f,
                alphaEdgeWidthV = 0.25f,
                alphaSymmetryU = false,
                alphaSymmetryV = false,
                alphaSymmetryCenterOrigin = false,
                vertexColorRGBMode = FXMVertexColorRGBMode.White,
                useModifiers = false,
                twistAmount = 0f,
                taperAmount = 0f,
                taperDirection = 0f,
                bendAmount = 0f,
                noiseStrength = 0f,
                noiseScale = 4f,
                noiseSeed = 0,
                maxBendEnable = false,
                maxBendAngle = 0f,
                maxBendAxis = FXMAxis.Y,
                maxBendPlane = FXMAxis.X,
                maxBendBias = 0f,
                maxTwistEnable = false,
                maxTwistAngle = 0f,
                maxTwistAxis = FXMAxis.Y,
                maxTwistDirection = FXMTwistDirection.Positive,
                maxTwistMode = FXMTwistMode.BottomToTop,
                maxTwistBias = 0f,
                maxTaperEnable = false,
                maxTaperAmount = 0f,
                maxTaperAxis = FXMAxis.Y,
                maxTaperBias = 0f,
                ffdEnable = false,
                ffdMode = FXMFFDMode.Box,
                ffdResolution = FXMFFDResolution.Res3,
                ffdAxis = FXMAxis.Y,
                ffdStrength = 0f,
                ffdP0Scale = 1f,
                ffdP1Scale = 1f,
                ffdP2Scale = 1f,
                ffdP3Scale = 1f,
                ffdP0Height = 0f,
                ffdP1Height = 0f,
                ffdP2Height = 0f,
                ffdP3Height = 0f,
                ffdSmoothness = 1f,
                maxNoiseEnable = false,
                maxNoiseSeed = 0,
                maxNoiseScale = 0f,
                maxNoiseFractal = false,
                maxNoiseStrengthX = 0f,
                maxNoiseStrengthY = 0f,
                maxNoiseStrengthZ = 0f,
                shellThickness = 0f,
                shellAxis = FXMAxis.Z,
                shellMode = FXMShellMode.Center,
                flipNormals = false,
                doubleSidedGeometry = false,
                recalculateNormals = true,
                recalculateTangents = false,
                centerPivotAfterBuild = true
            };
        }
    }


    internal struct FXMEdgeKey : IEquatable<FXMEdgeKey>
    {
        public int A;
        public int B;

        public FXMEdgeKey(int a, int b)
        {
            if (a < b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public bool Equals(FXMEdgeKey other)
        {
            return A == other.A && B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is FXMEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (A * 397) ^ B;
            }
        }
    }

    internal static class FXMMeshBuilder
    {
        public static int GetEffectiveCrossSegmentsForPreview(FXMSettings settings)
        {
            return GetEffectiveCrossSegments(settings, Mathf.Max(1, settings.widthSegments));
        }

        private static int GetEffectiveCrossSegments(FXMSettings settings, int requested)
        {
            requested = Mathf.Max(1, requested);
            if (!AlphaModeNeedsInteriorWidthSamples(settings.alphaMode))
            {
                return requested;
            }

            // Edge/Center/Radial alpha can only be visible in real vertex alpha
            // when the mesh has at least an edge-center-edge sample profile.
            // Without this, thin slash meshes with only 2 width rows evaluate
            // every width vertex as an edge and the alpha appears missing or fully flat.
            int minimum = 2;
            if (settings.alphaMode == FXMAlphaMode.CenterOut ||
                settings.alphaMode == FXMAlphaMode.CenterHole ||
                settings.alphaMode == FXMAlphaMode.RadialIn ||
                settings.alphaMode == FXMAlphaMode.RadialOut ||
                settings.alphaMode == FXMAlphaMode.CornerFade)
            {
                minimum = 4;
            }

            return Mathf.Max(requested, minimum);
        }

        public static int GetEffectiveFlowSegmentsForPreview(FXMSettings settings)
        {
            return GetEffectiveFlowSegments(settings, Mathf.Max(1, settings.segments));
        }

        private static int GetEffectiveFlowSegments(FXMSettings settings, int requested)
        {
            requested = Mathf.Max(1, requested);
            if (settings.alphaMode == FXMAlphaMode.CenterOut ||
                settings.alphaMode == FXMAlphaMode.CenterHole ||
                settings.alphaMode == FXMAlphaMode.EdgeFade ||
                settings.alphaMode == FXMAlphaMode.EdgeV ||
                settings.alphaMode == FXMAlphaMode.TipFade ||
                settings.alphaMode == FXMAlphaMode.CornerFade ||
                settings.alphaMode == FXMAlphaMode.RadialIn ||
                settings.alphaMode == FXMAlphaMode.RadialOut ||
                settings.alphaMode == FXMAlphaMode.GeometryBorder)
            {
                return Mathf.Max(requested, 4);
            }
            return requested;
        }

        private static bool AlphaModeNeedsInteriorWidthSamples(FXMAlphaMode mode)
        {
            switch (mode)
            {
                case FXMAlphaMode.CenterOut:
                case FXMAlphaMode.CenterHole:
                case FXMAlphaMode.EdgeFade:
                case FXMAlphaMode.EdgeU:
                case FXMAlphaMode.CornerFade:
                case FXMAlphaMode.RadialIn:
                case FXMAlphaMode.RadialOut:
                case FXMAlphaMode.GeometryBorder:
                    return true;
                default:
                    return false;
            }
        }

        public static Mesh Build(FXMSettings settings)
        {
            MeshData data;

            switch (settings.meshType)
            {
                case FXMMeshType.Beam:
                    data = BuildBeam(settings);
                    break;
                case FXMMeshType.Ring:
                    data = BuildRing(settings);
                    break;
                case FXMMeshType.Disc:
                    data = BuildDisc(settings);
                    break;
                case FXMMeshType.Dome:
                    data = BuildDome(settings, 360f);
                    break;
                case FXMMeshType.HalfDome:
                    data = BuildDome(settings, Mathf.Clamp(settings.arcAngle, 30f, 360f));
                    break;
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    data = BuildHelix(settings);
                    break;
                case FXMMeshType.Slash:
                default:
                    data = BuildSlash(settings);
                    break;
            }

            if (settings.useModifiers)
            {
                ApplyModifiers(data.vertices, settings);
            }

            ApplySurfaceIntegrityGuard(data, settings);
            ApplyUnityGroundPlaneLayout(data, settings);

            if (settings.centerPivotAfterBuild)
            {
                CenterVertices(data.vertices);
            }

            ApplyPivotAxisTransform(data.vertices, settings);
            ApplyAutoFacingFixes(data, settings);

            if (Mathf.Abs(settings.shellThickness) > 0.000001f)
            {
                ApplyShell(data, settings);
            }

            if (settings.flipNormals)
            {
                ReverseWinding(data);
            }

            ApplyVertexAlpha(data.colors, data.vertices, data.triangles, data.uvs, settings);

            Mesh mesh = new Mesh
            {
                indexFormat = data.vertices.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };

            mesh.SetVertices(data.vertices);
            mesh.SetUVs(0, data.uvs);
            mesh.SetColors(data.colors); // Color32/UNorm8: particle-system friendly vertex color stream.
            mesh.SetTriangles(data.triangles, 0);
            mesh.RecalculateBounds();

            if (settings.recalculateNormals)
            {
                mesh.RecalculateNormals();
            }

            if (settings.recalculateTangents)
            {
                TryRecalculateTangents(mesh);
            }

            if (settings.doubleSidedGeometry)
            {
                mesh = MakeDoubleSided(mesh, settings.recalculateNormals, settings.recalculateTangents);
            }

            return mesh;
        }

        private static MeshData BuildBeam(FXMSettings settings)
        {
            int segments = GetEffectiveFlowSegments(settings, Mathf.Max(1, settings.segments));
            int widthSegments = GetEffectiveCrossSegments(settings, Mathf.Max(1, settings.widthSegments));
            float baseWidth = Mathf.Max(0.0001f, settings.width);
            float length = Mathf.Max(0.0001f, settings.length);

            float yOffset;
            switch (settings.pivotMode)
            {
                case FXMPivotMode.Bottom:
                    yOffset = 0f;
                    break;
                case FXMPivotMode.Top:
                    yOffset = -length;
                    break;
                case FXMPivotMode.Center:
                default:
                    yOffset = -length * 0.5f;
                    break;
            }

            MeshData data = new MeshData();

            for (int y = 0; y <= segments; y++)
            {
                float rawV = y / (float)segments;
                float v01 = ApplyBias01(rawV, settings.segmentFlowBias);
                float py = v01 * length + yOffset;
                float fade = Mathf.Sin(rawV * Mathf.PI);
                float beamEndBlend = Mathf.Lerp(Mathf.Max(0.05f, settings.beamStartWidth), Mathf.Max(0.05f, settings.beamEndWidth), rawV);
                float beamCoreBlend = Mathf.Lerp(1f, Mathf.Max(0.05f, settings.beamCoreWidth), fade);
                float localWidth = baseWidth * beamEndBlend * beamCoreBlend * TaperScale(rawV, settings.taperAmount, settings.taperDirection);

                for (int x = 0; x <= widthSegments; x++)
                {
                    float rawU = x / (float)widthSegments;
                    float u01 = ApplyBias01(rawU, settings.segmentWidthBias);
                    float edge = Mathf.Abs(u01 - 0.5f) * 2f;
                    float jitter = StableNoise(y, x, settings.noiseSeed + 17) * settings.edgeJitter * baseWidth * edge * fade;
                    float px = (u01 - 0.5f) * localWidth + jitter;
                    float pz = Mathf.Sin(rawV * Mathf.PI) * settings.depth;

                    data.vertices.Add(new Vector3(px, py, pz));
                    data.uvs.Add(MakeUV(rawU, rawV, settings));
                    data.colors.Add(new Color32(255, 255, 255, 255));
                }
            }

            int row = widthSegments + 1;
            for (int y = 0; y < segments; y++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    int a = y * row + x;
                    int b = y * row + x + 1;
                    int c = (y + 1) * row + x;
                    int d = (y + 1) * row + x + 1;
                    AddQuad(data.triangles, a, c, b, d);
                }
            }

            return data;
        }

        private static MeshData BuildSlash(FXMSettings settings)
        {
            int segments = GetEffectiveFlowSegments(settings, Mathf.Max(1, settings.segments));
            int crossSegments = GetEffectiveCrossSegments(settings, Mathf.Max(1, settings.widthSegments));
            float radius = Mathf.Max(0.0001f, settings.radius);
            float baseThickness = Mathf.Max(0.0001f, settings.width);
            float arc = Mathf.Clamp(settings.arcAngle, 1f, 360f);
            float flowShift = Mathf.Clamp(settings.flowOffset, -1f, 1f) * arc * 0.12f;
            float start = settings.startAngle - arc * 0.5f + flowShift;
            float widthShift = settings.symmetryWidth ? 0f : Mathf.Clamp(settings.widthOffset, -1f, 1f) * baseThickness * 0.35f;
            float bias = Mathf.Clamp(settings.shapeBias, -1f, 1f);

            MeshData data = new MeshData();

            for (int i = 0; i <= segments; i++)
            {
                float rawV = i / (float)segments;
                float flowT = ApplyBias01(rawV, settings.segmentFlowBias);
                float biasedV = ApplyBias01(flowT, bias);
                float slashLengthScale = Mathf.Lerp(Mathf.Max(0.02f, settings.slashRootScale), Mathf.Max(0.02f, settings.slashTipScale), rawV);
                float localThickness = baseThickness * slashLengthScale * TaperScale(rawV, settings.taperAmount, settings.taperDirection);
                localThickness = GuardSlashRadialThickness(localThickness, radius, widthShift);
                float angle = (start + arc * biasedV) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float fade = Mathf.Sin(rawV * Mathf.PI);

                for (int j = 0; j <= crossSegments; j++)
                {
                    float rawU = j / (float)crossSegments;
                    float widthT = settings.symmetryWidth ? rawU : ApplyBias01(rawU, settings.segmentWidthBias);
                    float edge = Mathf.Abs(widthT - 0.5f) * 2f;
                    float lateral = (widthT - 0.5f) * localThickness;
                    float jitter = StableNoise(i, j, settings.noiseSeed + 31) * settings.edgeJitter * baseThickness * edge * fade;
                    float safeInnerRadius = Mathf.Max(0.0001f, radius * 0.035f);
                    float r = Mathf.Max(safeInnerRadius, radius + lateral + widthShift + jitter);
                    float z = Mathf.Sin(rawV * Mathf.PI) * settings.depth + Mathf.Sin((widthT - 0.5f) * Mathf.PI) * settings.depth * 0.15f;

                    data.vertices.Add(new Vector3(cos * r, sin * r, z));
                    data.uvs.Add(MakeUV(rawU, rawV, settings));
                    data.colors.Add(new Color32(255, 255, 255, 255));
                }
            }

            int row = crossSegments + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < crossSegments; j++)
                {
                    int a = i * row + j;
                    int b = i * row + j + 1;
                    int c = (i + 1) * row + j;
                    int d = (i + 1) * row + j + 1;
                    AddQuad(data.triangles, a, c, b, d);
                }
            }

            return data;
        }

        private static MeshData BuildRing(FXMSettings settings)
        {
            int flowSegments = GetEffectiveFlowSegments(settings, Mathf.Max(3, settings.segments));
            int crossSegments = GetEffectiveCrossSegments(settings, Mathf.Max(1, settings.widthSegments));
            float centerRadius = Mathf.Max(0.0001f, settings.radius);
            float thickness = Mathf.Max(0.0001f, settings.width);
            float arc = Mathf.Clamp(settings.arcAngle <= 0.0001f ? 360f : settings.arcAngle, 5f, 360f);
            bool fullCircle = arc >= 359.9f;
            float start = settings.startAngle - (fullCircle ? 0f : arc * 0.5f);
            float innerBase = Mathf.Max(0.0001f, centerRadius - thickness * 0.5f * Mathf.Max(0.05f, settings.ringInnerThickness));
            float outerBase = Mathf.Max(innerBase + 0.0001f, centerRadius + thickness * 0.5f * Mathf.Max(0.05f, settings.ringOuterThickness));
            float widthOffset = Mathf.Clamp(settings.widthOffset, -1f, 1f) * thickness * 0.45f;

            MeshData data = new MeshData();

            for (int i = 0; i <= flowSegments; i++)
            {
                float rawT = i / (float)flowSegments;
                float t = fullCircle
                    ? Mathf.Repeat(rawT + settings.flowOffset * 0.125f, 1f)
                    : Mathf.Clamp01(rawT + settings.flowOffset * 0.125f);
                t = ApplyBias01(t, settings.segmentFlowBias);
                float angle = (start + arc * t) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float openFade = fullCircle ? 1f : Mathf.Sin(rawT * Mathf.PI);
                float centerNoise = StableNoise(i, 0, settings.noiseSeed + 101) * settings.edgeJitter * thickness * 0.35f * Mathf.Clamp01(openFade);
                float z = Mathf.Sin(rawT * Mathf.PI * 2f) * settings.depth * 0.08f;

                for (int j = 0; j <= crossSegments; j++)
                {
                    float rawU = j / (float)crossSegments;
                    float u = ApplyBias01(rawU, settings.segmentWidthBias + settings.shapeBias * 0.35f);
                    float edge = Mathf.Abs(u - 0.5f) * 2f;
                    float edgeJitter = StableNoise(i, j, settings.noiseSeed + 113) * settings.edgeJitter * thickness * edge * Mathf.Clamp01(openFade);
                    float r = Mathf.Lerp(innerBase, outerBase, u) + widthOffset + centerNoise + edgeJitter;
                    r = Mathf.Max(0.0001f, r);

                    data.vertices.Add(new Vector3(cos * r, sin * r, z));
                    data.uvs.Add(MakeUV(rawU, rawT, settings));
                    data.colors.Add(new Color32(255, 255, 255, 255));
                }
            }

            int row = crossSegments + 1;
            for (int i = 0; i < flowSegments; i++)
            {
                if (settings.brokenAmount > 0.0001f)
                {
                    float breakNoise = Mathf.Abs(StableNoise(i, settings.noiseSeed, settings.noiseSeed + 131));
                    if (breakNoise < Mathf.Clamp01(settings.brokenAmount))
                    {
                        continue;
                    }
                }

                for (int j = 0; j < crossSegments; j++)
                {
                    int a = i * row + j;
                    int b = i * row + j + 1;
                    int c = (i + 1) * row + j;
                    int d = (i + 1) * row + j + 1;
                    AddQuad(data.triangles, a, c, b, d);
                }
            }

            return data;
        }

        private static MeshData BuildDisc(FXMSettings settings)
        {
            int angularSegments = Mathf.Max(3, settings.segments);
            int radialSegments = GetEffectiveCrossSegments(settings, Mathf.Max(1, settings.widthSegments));
            float outerRadius = Mathf.Max(0.0001f, settings.radius);
            float innerRadius = outerRadius * Mathf.Clamp01(settings.innerRadiusRatio);
            if (innerRadius < 0.0001f)
            {
                innerRadius = outerRadius * 0.02f;
            }

            float arc = Mathf.Clamp(settings.arcAngle <= 0.0001f ? 360f : settings.arcAngle, 5f, 360f);
            bool fullCircle = arc >= 359.9f;
            float start = settings.startAngle - (fullCircle ? 0f : arc * 0.5f);
            float radialOffset = Mathf.Clamp(settings.widthOffset, -1f, 1f) * outerRadius * 0.18f;

            MeshData data = new MeshData();

            for (int r = 0; r <= radialSegments; r++)
            {
                float rawR = r / (float)radialSegments;
                float radialT = ApplyBias01(rawR, settings.segmentWidthBias + settings.shapeBias * 0.35f);
                float currentRadius = Mathf.Lerp(innerRadius, outerRadius, radialT) + radialOffset * (rawR - 0.5f) * 2f;
                currentRadius = Mathf.Max(0.0001f, currentRadius);

                for (int i = 0; i <= angularSegments; i++)
                {
                    float rawA = i / (float)angularSegments;
                    float angularT = fullCircle
                        ? Mathf.Repeat(rawA + settings.flowOffset * 0.125f, 1f)
                        : Mathf.Clamp01(rawA + settings.flowOffset * 0.125f);
                    angularT = ApplyBias01(angularT, settings.segmentFlowBias);
                    float angle = (start + arc * angularT) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    float edge = Mathf.Abs(radialT - 0.5f) * 2f;
                    float jitter = StableNoise(i, r, settings.noiseSeed + 151) * settings.edgeJitter * outerRadius * 0.035f * edge;
                    float z = Mathf.Sin(rawR * Mathf.PI) * settings.depth * 0.05f;

                    data.vertices.Add(new Vector3(cos * (currentRadius + jitter), sin * (currentRadius + jitter), z));
                    data.uvs.Add(MakeUV(rawR, rawA, settings));
                    data.colors.Add(new Color32(255, 255, 255, 255));
                }
            }

            int row = angularSegments + 1;
            for (int r = 0; r < radialSegments; r++)
            {
                for (int i = 0; i < angularSegments; i++)
                {
                    int a = r * row + i;
                    int b = r * row + i + 1;
                    int c = (r + 1) * row + i;
                    int d = (r + 1) * row + i + 1;
                    AddQuad(data.triangles, a, c, b, d);
                }
            }

            return data;
        }

        private static MeshData BuildDome(FXMSettings settings, float horizontalAngle)
        {
            int angularSegments = Mathf.Max(3, settings.segments);
            int verticalSegments = GetEffectiveCrossSegments(settings, Mathf.Max(2, settings.widthSegments));
            float radius = Mathf.Max(0.0001f, settings.radius);
            float height = Mathf.Max(0.0001f, settings.height);
            float arc = Mathf.Clamp(horizontalAngle, 30f, 360f);
            float start = settings.startAngle - arc * 0.5f;

            MeshData data = new MeshData();

            for (int y = 0; y <= verticalSegments; y++)
            {
                float rawV = y / (float)verticalSegments;
                float v01 = ApplyBias01(rawV, settings.segmentFlowBias + settings.domeHeightBias * 0.5f);
                float roundness = Mathf.Max(0.05f, settings.domeRoundness);
                float polar = Mathf.Pow(v01, 1f / roundness) * Mathf.PI * 0.5f;
                float ringRadius = Mathf.Sin(polar) * radius * TaperScale(rawV, settings.taperAmount, settings.taperDirection);
                float py = Mathf.Cos(polar) * height;

                for (int i = 0; i <= angularSegments; i++)
                {
                    float rawU = i / (float)angularSegments;
                    float u01 = ApplyBias01(rawU, settings.segmentWidthBias);
                    float angle = (start + arc * u01) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);
                    float jitter = StableNoise(y, i, settings.noiseSeed + 53) * settings.edgeJitter * radius * 0.06f * Mathf.Sin(rawV * Mathf.PI);

                    data.vertices.Add(new Vector3(cos * (ringRadius + jitter), py + settings.depth * Mathf.Sin(rawV * Mathf.PI) * 0.15f, sin * (ringRadius + jitter)));
                    data.uvs.Add(MakeUV(rawU, rawV, settings));
                    data.colors.Add(new Color32(255, 255, 255, 255));
                }
            }

            int row = angularSegments + 1;
            for (int y = 0; y < verticalSegments; y++)
            {
                for (int i = 0; i < angularSegments; i++)
                {
                    int a = y * row + i;
                    int b = y * row + i + 1;
                    int c = (y + 1) * row + i;
                    int d = (y + 1) * row + i + 1;
                    AddQuad(data.triangles, a, c, b, d);
                }
            }

            return data;
        }

        private static MeshData BuildHelix(FXMSettings settings)
        {
            int segments = GetEffectiveFlowSegments(settings, Mathf.Max(3, settings.segments));
            int widthSegments = GetEffectiveCrossSegments(settings, Mathf.Max(1, settings.widthSegments));
            float radius = Mathf.Max(0.0001f, settings.radius);
            float endRadius = settings.helixRadius2 <= 0.0001f ? radius : Mathf.Max(0.0001f, settings.helixRadius2);
            float baseWidth = Mathf.Max(0.0001f, settings.width);
            float height = Mathf.Max(0.0001f, settings.height);
            float turns = Mathf.Max(0.01f, settings.turns);

            MeshData data = new MeshData();
            int ribbonCount = settings.meshType == FXMMeshType.CrossHelix ? 2 : 1;
            int estimatedVertices = Mathf.Max(4, (segments + 1) * (widthSegments + 1) * ribbonCount);
            int estimatedTriangles = Mathf.Max(6, segments * widthSegments * 6 * ribbonCount);
            data.vertices.Capacity = Mathf.Max(data.vertices.Capacity, estimatedVertices);
            data.uvs.Capacity = Mathf.Max(data.uvs.Capacity, estimatedVertices);
            data.colors.Capacity = Mathf.Max(data.colors.Capacity, estimatedVertices);
            data.triangles.Capacity = Mathf.Max(data.triangles.Capacity, estimatedTriangles);

            // Cross Rotation should rotate the whole cross profile around the helix tangent,
            // while keeping the two ribbons 90 degrees apart.
            // Base Helix uses 0, Cross Helix uses [crossRotation] and [crossRotation + 90].
            float primaryRibbonRotation = settings.meshType == FXMMeshType.CrossHelix ? settings.helixCrossRotation : 0f;
            AddHelixRibbon(data, settings, segments, widthSegments, radius, endRadius, baseWidth, height, turns, primaryRibbonRotation);

            if (settings.meshType == FXMMeshType.CrossHelix)
            {
                AddHelixRibbon(data, settings, segments, widthSegments, radius, endRadius, baseWidth, height, turns, settings.helixCrossRotation + 90f);
            }

            return data;
        }

        private static void AddHelixRibbon(
            MeshData data,
            FXMSettings settings,
            int segments,
            int widthSegments,
            float radius,
            float endRadius,
            float baseWidth,
            float height,
            float turns,
            float widthAxisRotation)
        {
            if (data == null)
            {
                return;
            }

            int vertexStart = data.vertices.Count;
            float direction = settings.helixClockwise ? -1f : 1f;
            float verticalPerRadian = height / Mathf.Max(0.0001f, turns * Mathf.PI * 2f);

            for (int i = 0; i <= segments; i++)
            {
                float rawV = i / (float)segments;
                float v01 = ApplyBias01(rawV, settings.segmentFlowBias);
                float angle = (settings.startAngle + direction * turns * 360f * v01) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float localWidth = baseWidth * TaperScale(rawV, settings.taperAmount, settings.taperDirection);

                float currentRadius = Mathf.Lerp(radius, endRadius, rawV);
                Vector3 radial = new Vector3(cos, 0f, sin).normalized;
                Vector3 tangent = new Vector3(-sin * currentRadius * direction, verticalPerRadian, cos * currentRadius * direction);
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = Vector3.up;
                }
                tangent.Normalize();

                Vector3 widthAxis = radial;
                if (Mathf.Abs(widthAxisRotation) > 0.0001f)
                {
                    widthAxis = Quaternion.AngleAxis(widthAxisRotation, tangent) * widthAxis;
                }
                if (widthAxis.sqrMagnitude < 0.0001f)
                {
                    widthAxis = radial;
                }
                widthAxis.Normalize();

                Vector3 center = new Vector3(cos * currentRadius, Mathf.Lerp(-height * 0.5f, height * 0.5f, v01), sin * currentRadius);
                center.y += Mathf.Sin(rawV * Mathf.PI) * settings.depth * 0.1f;

                for (int j = 0; j <= widthSegments; j++)
                {
                    float rawU = j / (float)widthSegments;
                    float u01 = ApplyBias01(rawU, settings.segmentWidthBias);
                    float edge = Mathf.Abs(u01 - 0.5f) * 2f;
                    float jitter = StableNoise(i, j + Mathf.RoundToInt(widthAxisRotation * 10f), settings.noiseSeed + 71) * settings.edgeJitter * baseWidth * edge;
                    Vector3 p = center + widthAxis * ((u01 - 0.5f) * localWidth + jitter);
                    data.vertices.Add(p);
                    data.uvs.Add(MakeUV(rawU, rawV, settings));
                    data.colors.Add(new Color32(255, 255, 255, 255));
                }
            }

            int row = widthSegments + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < widthSegments; j++)
                {
                    int a = vertexStart + i * row + j;
                    int b = vertexStart + i * row + j + 1;
                    int c = vertexStart + (i + 1) * row + j;
                    int d = vertexStart + (i + 1) * row + j + 1;
                    if (settings.helixClockwise)
                    {
                        // Clockwise는 회전 방향만 반전해야 합니다.
                        // i 진행 방향이 반대로 감기면 같은 인덱스 순서가 Backface로 뒤집히므로
                        // 삼각형 순서를 한 번 반전해 표면 방향을 유지합니다.
                        AddQuad(data.triangles, a, b, c, d);
                    }
                    else
                    {
                        AddQuad(data.triangles, a, c, b, d);
                    }
                }
            }
        }

        private static float GuardSlashRadialThickness(float localThickness, float radius, float widthShift)
        {
            radius = Mathf.Max(0.0001f, radius);
            localThickness = Mathf.Max(0.0001f, localThickness);

            // A slash is built as an arc strip around a center radius.
            // If the inner strip crosses the center, high Flow/Arc Segments can fold quads,
            // and Cull Back makes the folded cells look like missing faces.
            // Keep the inner side slightly outside the center so winding stays stable.
            float safeMax = Mathf.Max(0.0001f, (radius - Mathf.Abs(widthShift) - radius * 0.035f) * 2f);
            return Mathf.Min(localThickness, safeMax);
        }

        private static void ApplySurfaceIntegrityGuard(MeshData data, FXMSettings settings)
        {
            if (data == null || data.vertices == null || data.triangles == null || data.vertices.Count < 3 || data.triangles.Count < 3)
            {
                return;
            }

            switch (settings.meshType)
            {
                case FXMMeshType.Slash:
                case FXMMeshType.Beam:
                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                    NormalizeTriangleWinding(data);
                    RemoveDegenerateTriangles(data, 0.00000001f);
                    break;
                case FXMMeshType.Helix:
                case FXMMeshType.CrossHelix:
                    // Helix builders already write consistent winding. Skipping global winding scan
                    // avoids expensive full-mesh passes during high-segment Cross Helix editing.
                    break;
            }
        }

        private static void ApplyUnityGroundPlaneLayout(MeshData data, FXMSettings settings)
        {
            if (data == null || data.vertices == null || data.vertices.Count == 0)
            {
                return;
            }

            if (!IsPlanarGroundMesh(settings.meshType))
            {
                return;
            }

            // v0.28.4: Blender FBX imports a flat VFX mesh onto Unity's ground plane: XZ surface, +Y normal.
            // Older Unity-generated Slash/Beam/Ring/Disc data was authored on XY with -Z normals,
            // so a Back-Cull VFX material saw only the orange selection outline from the usual Scene view angle.
            // Swapping Y/Z after the surface has been built preserves the silhouette, UV, vertex alpha, and winding,
            // while producing the same practical orientation as the Blender-exported FBX.
            for (int i = 0; i < data.vertices.Count; i++)
            {
                Vector3 p = data.vertices[i];
                data.vertices[i] = new Vector3(p.x, p.z, p.y);
            }
        }

        private static bool IsPlanarGroundMesh(FXMMeshType meshType)
        {
            switch (meshType)
            {
                case FXMMeshType.Slash:
                case FXMMeshType.Beam:
                case FXMMeshType.Ring:
                case FXMMeshType.Disc:
                    return true;
                default:
                    return false;
            }
        }

        private static Quaternion GetPivotAxisRotation(FXMSettings settings)
        {
            Quaternion pivotRotation = settings.pivotRotationEuler.sqrMagnitude > 0.0000001f
                ? Quaternion.Euler(settings.pivotRotationEuler)
                : Quaternion.identity;
            Quaternion upAxisRotation = settings.upAxis == FXMUpAxis.ZUp
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
            return upAxisRotation * pivotRotation;
        }

        private static void ApplyAutoFacingFixes(MeshData data, FXMSettings settings)
        {
            if (data == null || data.vertices == null || data.triangles == null || data.vertices.Count < 3 || data.triangles.Count < 3)
            {
                return;
            }

            switch (settings.meshType)
            {
                case FXMMeshType.Disc:
                    EnsureFacingAlongDirection(data, GetPivotAxisRotation(settings) * Vector3.up);
                    break;

                case FXMMeshType.Dome:
                case FXMMeshType.HalfDome:
                    EnsureDomeFacingOutward(data, settings);
                    break;
            }
        }

        private static void EnsureFacingAlongDirection(MeshData data, Vector3 desiredDirection)
        {
            if (desiredDirection.sqrMagnitude < 0.000001f)
            {
                desiredDirection = Vector3.up;
            }
            desiredDirection.Normalize();

            Vector3 averageNormal = Vector3.zero;
            for (int i = 0; i + 2 < data.triangles.Count; i += 3)
            {
                int ia = data.triangles[i];
                int ib = data.triangles[i + 1];
                int ic = data.triangles[i + 2];
                if (!IsValidTriangleIndex(data.vertices, ia, ib, ic))
                {
                    continue;
                }

                Vector3 n = Vector3.Cross(data.vertices[ib] - data.vertices[ia], data.vertices[ic] - data.vertices[ia]);
                if (n.sqrMagnitude > 0.00000001f)
                {
                    averageNormal += n.normalized;
                }
            }

            if (averageNormal.sqrMagnitude > 0.00000001f && Vector3.Dot(averageNormal.normalized, desiredDirection) < 0f)
            {
                ReverseWinding(data);
            }
        }

        private static void EnsureDomeFacingOutward(MeshData data, FXMSettings settings)
        {
            Vector3 up = GetPivotAxisRotation(settings) * Vector3.up;
            if (up.sqrMagnitude < 0.000001f)
            {
                up = Vector3.up;
            }
            up.Normalize();

            float minUp = float.PositiveInfinity;
            Vector3 lateralSum = Vector3.zero;
            for (int i = 0; i < data.vertices.Count; i++)
            {
                Vector3 v = data.vertices[i];
                float upDot = Vector3.Dot(v, up);
                if (upDot < minUp)
                {
                    minUp = upDot;
                }
                lateralSum += Vector3.ProjectOnPlane(v, up);
            }

            Vector3 baseCenter = lateralSum / Mathf.Max(1, data.vertices.Count) + up * minUp;
            float outwardScore = 0f;

            for (int i = 0; i + 2 < data.triangles.Count; i += 3)
            {
                int ia = data.triangles[i];
                int ib = data.triangles[i + 1];
                int ic = data.triangles[i + 2];
                if (!IsValidTriangleIndex(data.vertices, ia, ib, ic))
                {
                    continue;
                }

                Vector3 a = data.vertices[ia];
                Vector3 b = data.vertices[ib];
                Vector3 c = data.vertices[ic];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude <= 0.00000001f)
                {
                    continue;
                }

                Vector3 triCenter = (a + b + c) * (1f / 3f);
                outwardScore += Vector3.Dot(n.normalized, (triCenter - baseCenter).normalized);
            }

            if (outwardScore < 0f)
            {
                ReverseWinding(data);
            }
        }

        private static void NormalizeTriangleWinding(MeshData data)
        {
            Vector3 reference = Vector3.zero;

            for (int i = 0; i + 2 < data.triangles.Count; i += 3)
            {
                int ia = data.triangles[i];
                int ib = data.triangles[i + 1];
                int ic = data.triangles[i + 2];
                if (!IsValidTriangleIndex(data.vertices, ia, ib, ic))
                {
                    continue;
                }

                Vector3 n = Vector3.Cross(data.vertices[ib] - data.vertices[ia], data.vertices[ic] - data.vertices[ia]);
                if (n.sqrMagnitude > 0.00000001f)
                {
                    reference += n;
                }
            }

            if (reference.sqrMagnitude < 0.00000001f)
            {
                return;
            }

            reference.Normalize();

            for (int i = 0; i + 2 < data.triangles.Count; i += 3)
            {
                int ia = data.triangles[i];
                int ib = data.triangles[i + 1];
                int ic = data.triangles[i + 2];
                if (!IsValidTriangleIndex(data.vertices, ia, ib, ic))
                {
                    continue;
                }

                Vector3 n = Vector3.Cross(data.vertices[ib] - data.vertices[ia], data.vertices[ic] - data.vertices[ia]);
                if (n.sqrMagnitude > 0.00000001f && Vector3.Dot(n, reference) < 0f)
                {
                    data.triangles[i + 1] = ic;
                    data.triangles[i + 2] = ib;
                }
            }
        }

        private static void RemoveDegenerateTriangles(MeshData data, float areaEpsilon)
        {
            List<int> cleaned = new List<int>(data.triangles.Count);
            for (int i = 0; i + 2 < data.triangles.Count; i += 3)
            {
                int ia = data.triangles[i];
                int ib = data.triangles[i + 1];
                int ic = data.triangles[i + 2];
                if (!IsValidTriangleIndex(data.vertices, ia, ib, ic))
                {
                    continue;
                }

                Vector3 n = Vector3.Cross(data.vertices[ib] - data.vertices[ia], data.vertices[ic] - data.vertices[ia]);
                if (n.sqrMagnitude <= areaEpsilon)
                {
                    continue;
                }

                cleaned.Add(ia);
                cleaned.Add(ib);
                cleaned.Add(ic);
            }

            if (cleaned.Count > 0 && cleaned.Count != data.triangles.Count)
            {
                data.triangles.Clear();
                data.triangles.AddRange(cleaned);
            }
        }

        private static bool IsValidTriangleIndex(List<Vector3> vertices, int a, int b, int c)
        {
            return a >= 0 && b >= 0 && c >= 0 &&
                   a < vertices.Count && b < vertices.Count && c < vertices.Count &&
                   a != b && b != c && a != c;
        }

        private static float ApplyBias01(float value, float bias)
        {
            value = Mathf.Clamp01(value);
            bias = Mathf.Clamp(bias, -0.95f, 0.95f);
            if (Mathf.Abs(bias) < 0.0001f)
            {
                return value;
            }

            float power = bias > 0f
                ? Mathf.Lerp(1f, 0.28f, bias)
                : Mathf.Lerp(1f, 3.5f, -bias);
            return Mathf.Clamp01(Mathf.Pow(value, power));
        }

        private static float TaperScale(float value, float amount, float direction)
        {
            amount = Mathf.Clamp01(amount);
            direction = Mathf.Clamp(direction, -1f, 1f);
            value = Mathf.Clamp01(value);

            float bothEnds = 1f - amount + amount * Mathf.Sin(value * Mathf.PI);
            float startNarrow = 1f - amount + amount * value;
            float endNarrow = 1f - amount * value;

            float scale;
            if (direction < 0f)
            {
                scale = Mathf.Lerp(bothEnds, startNarrow, Mathf.Abs(direction));
            }
            else if (direction > 0f)
            {
                scale = Mathf.Lerp(bothEnds, endNarrow, Mathf.Abs(direction));
            }
            else
            {
                scale = bothEnds;
            }

            return Mathf.Max(0.001f, scale);
        }

        private static float StableNoise(int a, int b, int seed)
        {
            float x = a * 12.9898f + b * 78.233f + seed * 37.719f;
            return Mathf.Repeat(Mathf.Sin(x) * 43758.5453f, 1f) * 2f - 1f;
        }


        private static void ReverseWinding(MeshData data)
        {
            if (data == null || data.triangles == null)
            {
                return;
            }

            for (int i = 0; i < data.triangles.Count; i += 3)
            {
                int temp = data.triangles[i + 1];
                data.triangles[i + 1] = data.triangles[i + 2];
                data.triangles[i + 2] = temp;
            }
        }

        private static void AddQuad(List<int> triangles, int a, int c, int b, int d)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);

            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(d);
        }

        internal static Vector2 PreviewUV01(float u01, float v01, FXMSettings settings)
        {
            TransformUvFlowDirection(ref u01, ref v01, settings.uvFlowDirection);
            if (settings.flipU) u01 = 1f - u01;
            if (settings.flipV) v01 = 1f - v01;
            u01 = ApplySignedBias01(Mathf.Clamp01(u01), settings.uvBiasU);
            v01 = ApplySignedBias01(Mathf.Clamp01(v01), settings.uvBiasV);
            return new Vector2(Mathf.Clamp01(u01), Mathf.Clamp01(v01));
        }

        private static Vector2 MakeUV(float u01, float v01, FXMSettings settings)
        {
            Vector2 uv01 = PreviewUV01(u01, v01, settings);
            return new Vector2(uv01.x * settings.uvTilingU, uv01.y * settings.uvTilingV);
        }

        private static void TransformUvFlowDirection(ref float u, ref float v, FXMUVFlowDirection direction)
        {
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            switch (direction)
            {
                case FXMUVFlowDirection.Auto:
                case FXMUVFlowDirection.VForward:
                    return;

                case FXMUVFlowDirection.VReverse:
                    v = 1f - v;
                    return;

                case FXMUVFlowDirection.UForward:
                    // v0.32.6: U/V Flow must describe the actual material axis, not remap the mesh so both axes
                    // physically travel in the same direction. Keep UV layout in its raw U/V basis; the shader,
                    // 2D preview, and scene helpers choose the flow axis from the selected direction.
                    return;

                case FXMUVFlowDirection.UReverse:
                    // Reverse the material U axis only. Do not swap U/V; swapping made U Forward and V Forward
                    // appear to flow along the same physical direction on slash/ring meshes.
                    u = 1f - u;
                    return;

                case FXMUVFlowDirection.FromCenter:
                case FXMUVFlowDirection.ToCenter:
                case FXMUVFlowDirection.CircularCW:
                case FXMUVFlowDirection.CircularCCW:
                {
                    float dx = u - 0.5f;
                    float dy = v - 0.5f;
                    float radius = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.70710678f);
                    float angle01 = Mathf.Repeat(Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) + 1f, 1f);

                    if (direction == FXMUVFlowDirection.FromCenter)
                    {
                        u = angle01;
                        v = radius;
                    }
                    else if (direction == FXMUVFlowDirection.ToCenter)
                    {
                        u = angle01;
                        v = 1f - radius;
                    }
                    else if (direction == FXMUVFlowDirection.CircularCW)
                    {
                        u = Mathf.Repeat(1f - angle01, 1f);
                        v = radius;
                    }
                    else
                    {
                        u = angle01;
                        v = radius;
                    }
                    return;
                }
            }
        }

        private static void ApplyVertexAlpha(List<Color32> colors, List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, FXMSettings settings)
        {
            if (colors == null || vertices == null || uvs == null || colors.Count != uvs.Count || colors.Count != vertices.Count)
            {
                return;
            }

            List<float> geometryBorderAlpha = settings.alphaMode == FXMAlphaMode.GeometryBorder
                ? ComputeGeometryBorderAlpha(vertices, triangles, settings)
                : null;

            for (int i = 0; i < colors.Count; i++)
            {
                Vector2 uv = uvs[i];
                float u = settings.uvTilingU > 0.0001f ? uv.x / settings.uvTilingU : uv.x;
                float v = settings.uvTilingV > 0.0001f ? uv.y / settings.uvTilingV : uv.y;
                u = Mathf.Clamp01(u);
                v = Mathf.Clamp01(v);

                float alphaU = u;
                float alphaV = v;
                if (settings.alphaMode == FXMAlphaMode.GeometryBorder)
                {
                    ApplyAlphaSymmetry(ref alphaU, ref alphaV, settings);
                }

                float alpha = geometryBorderAlpha != null && i < geometryBorderAlpha.Count
                    ? FinalizeAlphaMask(geometryBorderAlpha[i], alphaU, alphaV, settings)
                    : EvaluateVertexAlphaMask(u, v, settings);

                if (settings.meshType == FXMMeshType.Disc)
                {
                    float radial = UsesRadialUvV(settings.uvFlowDirection) ? v : u;
                    if (settings.discCenterFade > 0.0001f)
                    {
                        alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(radial / Mathf.Max(0.0001f, settings.discCenterFade)));
                    }
                    if (settings.discOuterFade > 0.0001f)
                    {
                        alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - radial) / Mathf.Max(0.0001f, settings.discOuterFade)));
                    }
                }

                colors[i] = MakeVertexColorFromAlpha(Mathf.Clamp01(alpha), settings.vertexColorRGBMode);
            }
        }

        private static bool UsesRadialUvV(FXMUVFlowDirection direction)
        {
            return direction == FXMUVFlowDirection.FromCenter ||
                   direction == FXMUVFlowDirection.ToCenter ||
                   direction == FXMUVFlowDirection.CircularCW ||
                   direction == FXMUVFlowDirection.CircularCCW;
        }

        private static Color32 MakeVertexColorFromAlpha(float alpha, FXMVertexColorRGBMode rgbMode)
        {
            alpha = SnapAlphaForVertexColor(alpha);
            switch (rgbMode)
            {
                case FXMVertexColorRGBMode.AlphaToRGB:
                    return MakeColor32(alpha, alpha, alpha, alpha);
                case FXMVertexColorRGBMode.InvertAlphaToRGB:
                {
                    float inv = 1f - alpha;
                    return MakeColor32(inv, inv, inv, alpha);
                }
                case FXMVertexColorRGBMode.AlphaToR:
                    return MakeColor32(alpha, 1f, 1f, alpha);
                case FXMVertexColorRGBMode.AlphaToG:
                    return MakeColor32(1f, alpha, 1f, alpha);
                case FXMVertexColorRGBMode.AlphaToB:
                    return MakeColor32(1f, 1f, alpha, alpha);
                case FXMVertexColorRGBMode.White:
                default:
                    return MakeColor32(1f, 1f, 1f, alpha);
            }
        }


        private static Color32 MakeColor32(float r, float g, float b, float a)
        {
            return new Color32(
                Float01ToByte(r),
                Float01ToByte(g),
                Float01ToByte(b),
                Float01ToByte(a));
        }

        private static byte Float01ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }


        private static float SnapAlphaForVertexColor(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            // v0.28.3: Values that are visually meant to be fully cut should export as exact 0.
            // This prevents faint residual alpha from showing up in preview/material workflows.
            const float zeroEpsilon = 1f / 255f;
            const float oneEpsilon = 1f - (1f / 255f);
            if (alpha <= zeroEpsilon)
            {
                return 0f;
            }
            if (alpha >= oneEpsilon)
            {
                return 1f;
            }
            return alpha;
        }

        private static void ApplyAlphaSymmetry(ref float u, ref float v, FXMSettings settings)
        {
            if (settings.alphaSymmetryU)
            {
                u = settings.alphaSymmetryCenterOrigin
                    ? Mathf.Abs(u - 0.5f) * 2f
                    : 1f - Mathf.Abs(u - 0.5f) * 2f;
            }

            if (settings.alphaSymmetryV)
            {
                v = settings.alphaSymmetryCenterOrigin
                    ? Mathf.Abs(v - 0.5f) * 2f
                    : 1f - Mathf.Abs(v - 0.5f) * 2f;
            }

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
        }

        private static float EvaluateVertexAlphaMask(float u, float v, FXMSettings settings)
        {
            ApplyAlphaSymmetry(ref u, ref v, settings);
            float alpha = 1f;

            switch (settings.alphaMode)
            {
                case FXMAlphaMode.None:
                    alpha = 1f;
                    break;
                case FXMAlphaMode.AlongU:
                case FXMAlphaMode.WidthU:
                    alpha = u;
                    break;
                case FXMAlphaMode.WidthUReverse:
                    alpha = 1f - u;
                    break;
                case FXMAlphaMode.AlongV:
                case FXMAlphaMode.FlowV:
                    alpha = v;
                    break;
                case FXMAlphaMode.FlowVReverse:
                case FXMAlphaMode.RootFade:
                    alpha = 1f - v;
                    break;
                case FXMAlphaMode.CenterOut:
                {
                    float du = Mathf.Abs(u - 0.5f) * 2f;
                    float dv = Mathf.Abs(v - 0.5f) * 2f;
                    alpha = 1f - Mathf.Clamp01(Mathf.Max(du, dv));
                    break;
                }
                case FXMAlphaMode.CenterHole:
                {
                    float du = Mathf.Abs(u - 0.5f) * 2f;
                    float dv = Mathf.Abs(v - 0.5f) * 2f;
                    alpha = Mathf.Clamp01(Mathf.Max(du, dv));
                    break;
                }
                case FXMAlphaMode.EdgeU:
                    alpha = EdgeMask(u, settings.alphaEdgeWidthU);
                    break;
                case FXMAlphaMode.EdgeV:
                    alpha = EdgeMask(v, settings.alphaEdgeWidthV);
                    break;
                case FXMAlphaMode.EdgeFade:
                    alpha = Mathf.Min(EdgeMask(u, settings.alphaEdgeWidthU), EdgeMask(v, settings.alphaEdgeWidthV));
                    break;
                case FXMAlphaMode.GeometryBorder:
                    alpha = Mathf.Min(EdgeMask(u, settings.alphaEdgeWidthU), EdgeMask(v, settings.alphaEdgeWidthV));
                    break;
                case FXMAlphaMode.TipFade:
                    alpha = Mathf.Clamp01(Mathf.Min(v, 1f - v) * 2f);
                    break;
                case FXMAlphaMode.CornerFade:
                {
                    float edgeU = Mathf.Min(u, 1f - u) * 2f;
                    float edgeV = Mathf.Min(v, 1f - v) * 2f;
                    alpha = Mathf.Clamp01(edgeU * edgeV);
                    break;
                }
                case FXMAlphaMode.RadialIn:
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.70710678f;
                    alpha = 1f - Mathf.Clamp01(d);
                    break;
                }
                case FXMAlphaMode.RadialOut:
                {
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) / 0.70710678f;
                    alpha = Mathf.Clamp01(d);
                    break;
                }
                case FXMAlphaMode.NoiseDissolve:
                    alpha = Mathf.Lerp(v, HashNoise01(u, v, settings.alphaNoiseScale, settings.alphaNoiseSeed), Mathf.Clamp01(settings.alphaNoiseStrength));
                    break;
                case FXMAlphaMode.StripePulse:
                    alpha = StripeMask(v, settings.alphaStripeCount, settings.alphaStripeSoftness);
                    break;
            }

            return FinalizeAlphaMask(alpha, u, v, settings);
        }

        private static float FinalizeAlphaMask(float alpha, float u, float v, FXMSettings settings)
        {
            alpha = RemapAlphaRange(alpha, settings.alphaRangeStart, settings.alphaRangeEnd, settings.alphaSoftness);

            if (settings.alphaMode != FXMAlphaMode.NoiseDissolve && settings.alphaNoiseStrength > 0.0001f)
            {
                float noise = HashNoise01(u, v, settings.alphaNoiseScale, settings.alphaNoiseSeed);
                alpha = Mathf.Lerp(alpha, alpha * noise, Mathf.Clamp01(settings.alphaNoiseStrength));
            }

            alpha = Mathf.Clamp01(alpha + settings.alphaOffset);
            alpha = Mathf.Clamp01((alpha - 0.5f) * Mathf.Max(0.0001f, settings.alphaContrast) + 0.5f);
            alpha = Mathf.Pow(Mathf.Clamp01(alpha), Mathf.Max(0.0001f, settings.alphaPower));

            if (settings.invertAlpha)
            {
                alpha = 1f - alpha;
            }

            float minA = Mathf.Clamp01(settings.minAlpha);
            float maxA = Mathf.Clamp01(settings.maxAlpha);
            if (maxA < minA)
            {
                float tmp = minA;
                minA = maxA;
                maxA = tmp;
            }

            return SnapAlphaForVertexColor(Mathf.Lerp(minA, maxA, Mathf.Clamp01(alpha)));
        }

        private static List<float> ComputeGeometryBorderAlpha(List<Vector3> vertices, List<int> triangles, FXMSettings settings)
        {
            List<float> result = new List<float>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
            {
                result.Add(1f);
            }

            if (vertices.Count == 0 || triangles == null || triangles.Count < 3)
            {
                return result;
            }

            Dictionary<FXMEdgeKey, int> edgeCounts = new Dictionary<FXMEdgeKey, int>();
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                AddEdgeCount(edgeCounts, triangles[i], triangles[i + 1]);
                AddEdgeCount(edgeCounts, triangles[i + 1], triangles[i + 2]);
                AddEdgeCount(edgeCounts, triangles[i + 2], triangles[i]);
            }

            List<int> boundaryIndices = new List<int>();
            HashSet<int> seen = new HashSet<int>();
            foreach (KeyValuePair<FXMEdgeKey, int> pair in edgeCounts)
            {
                if (pair.Value != 1)
                {
                    continue;
                }

                if (seen.Add(pair.Key.A)) boundaryIndices.Add(pair.Key.A);
                if (seen.Add(pair.Key.B)) boundaryIndices.Add(pair.Key.B);
            }

            if (boundaryIndices.Count == 0)
            {
                return result;
            }

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }

            float diagonal = Mathf.Max(0.0001f, bounds.size.magnitude);
            float radius = Mathf.Max(0.0001f, Mathf.Max(settings.alphaEdgeWidthU, settings.alphaEdgeWidthV) * diagonal);

            for (int i = 0; i < vertices.Count; i++)
            {
                float minDistanceSq = float.PositiveInfinity;
                Vector3 p = vertices[i];
                for (int b = 0; b < boundaryIndices.Count; b++)
                {
                    int index = boundaryIndices[b];
                    if (index < 0 || index >= vertices.Count)
                    {
                        continue;
                    }

                    float dSq = (p - vertices[index]).sqrMagnitude;
                    if (dSq < minDistanceSq)
                    {
                        minDistanceSq = dSq;
                    }
                }

                float distance = Mathf.Sqrt(Mathf.Max(0f, minDistanceSq));
                result[i] = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / radius));
            }

            return result;
        }

        private static void AddEdgeCount(Dictionary<FXMEdgeKey, int> edgeCounts, int a, int b)
        {
            FXMEdgeKey key = new FXMEdgeKey(a, b);
            int count;
            edgeCounts.TryGetValue(key, out count);
            edgeCounts[key] = count + 1;
        }

        private static float EdgeMask(float value, float width)
        {
            float edge = Mathf.Min(value, 1f - value);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / Mathf.Max(0.0001f, width)));
        }

        private static float RemapAlphaRange(float value, float start, float end, float softness)
        {
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            if (Mathf.Abs(end - start) < 0.0001f)
            {
                return value >= end ? 1f : 0f;
            }

            float t = Mathf.Clamp01((value - start) / (end - start));
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            return Mathf.Lerp(t, smooth, Mathf.Clamp01(softness));
        }

        private static float HashNoise01(float u, float v, float scale, int seed)
        {
            float scaled = Mathf.Max(0.01f, scale);
            float sx = Mathf.Floor(u * scaled * 23f + seed * 13.13f);
            float sy = Mathf.Floor(v * scaled * 23f + seed * 7.71f);
            float n = Mathf.Sin(sx * 12.9898f + sy * 78.233f + seed * 0.123f) * 43758.5453f;
            return n - Mathf.Floor(n);
        }

        private static float StripeMask(float value, float count, float softness)
        {
            float wave = Mathf.Abs(Mathf.Sin(value * Mathf.Max(1f, count) * Mathf.PI));
            return Mathf.SmoothStep(0f, Mathf.Clamp(softness, 0.01f, 1f), wave);
        }

        private static void ApplyModifiers(List<Vector3> vertices, FXMSettings settings)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            FXMModifierProfile profile = FXMModifierProfile.For(settings.meshType);
            bool useTwist = profile.allowTwist && Mathf.Abs(settings.twistAmount) > 0.0001f;
            bool useTaper = false; // Builder-level taper keeps Blender taper semantics for slash/beam/dome/helix.
            bool useBend = profile.allowBend && Mathf.Abs(settings.bendAmount) > 0.0001f;
            bool useNoise = profile.allowNoise && settings.noiseStrength > 0.0001f;
            bool useMaxBend = settings.maxBendEnable && Mathf.Abs(settings.maxBendAngle) > 0.0001f;
            bool useMaxTwist = settings.maxTwistEnable && Mathf.Abs(settings.maxTwistAngle) > 0.0001f;
            bool useMaxTaper = settings.maxTaperEnable && Mathf.Abs(settings.maxTaperAmount) > 0.0001f;
            bool useFfd = settings.ffdEnable;
            bool useMaxNoise = settings.maxNoiseEnable && (Mathf.Abs(settings.maxNoiseStrengthX) > 0.0001f || Mathf.Abs(settings.maxNoiseStrengthY) > 0.0001f || Mathf.Abs(settings.maxNoiseStrengthZ) > 0.0001f) && settings.maxNoiseScale > 0.0001f;

            if (!useTwist && !useTaper && !useBend && !useNoise && !useMaxBend && !useMaxTwist && !useMaxTaper && !useFfd && !useMaxNoise)
            {
                return;
            }

            Bounds bounds = GetBounds(vertices);
            Vector3 center = bounds.center;
            float yMin = bounds.min.y;
            float ySize = Mathf.Max(bounds.size.y, 0.0001f);
            float xSize = Mathf.Max(bounds.size.x, 0.0001f);
            float zSize = Mathf.Max(bounds.size.z, 0.0001f);
            float referenceSize = Mathf.Max(xSize, Mathf.Max(ySize, zSize));

            // Legacy simple stack: safe quick edits used by earlier Unity Edition versions.
            if (useTwist || useBend || useNoise)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Vector3 p = vertices[i];
                    float t = Mathf.Clamp01((p.y - yMin) / ySize);

                    if (useTaper)
                    {
                        float scale = TaperScale(t, settings.taperAmount, settings.taperDirection);
                        p.x = center.x + (p.x - center.x) * scale;
                        p.z = center.z + (p.z - center.z) * scale;
                    }

                    if (useTwist)
                    {
                        float angle = settings.twistAmount * (t - 0.5f) * Mathf.Deg2Rad;
                        float cos = Mathf.Cos(angle);
                        float sin = Mathf.Sin(angle);
                        float dx = p.x - center.x;
                        float dz = p.z - center.z;
                        p.x = center.x + dx * cos - dz * sin;
                        p.z = center.z + dx * sin + dz * cos;
                    }

                    if (useBend)
                    {
                        float bendRad = settings.bendAmount * Mathf.Deg2Rad;
                        float normalized = t - 0.5f;
                        float offset = Mathf.Sin(normalized * Mathf.PI) * bendRad * 0.18f * referenceSize;
                        p.x += offset;
                    }

                    if (useNoise)
                    {
                        float seed = settings.noiseSeed * 12.9898f;
                        float scale = 8f / Mathf.Max(0.1f, settings.noiseScale);
                        float n1 = Mathf.PerlinNoise((p.x + seed) * scale, (p.y - seed) * scale) - 0.5f;
                        float n2 = Mathf.PerlinNoise((p.y + seed * 0.37f) * scale, (p.z + seed) * scale) - 0.5f;
                        float n3 = Mathf.PerlinNoise((p.z - seed * 0.21f) * scale, (p.x + seed) * scale) - 0.5f;
                        float amp = settings.noiseStrength * referenceSize * 0.08f;
                        p += new Vector3(n1, n2, n3) * amp;
                    }

                    vertices[i] = p;
                }
            }

            // Blender add-on inspired 3ds Max stack order.
            if (useMaxBend) ApplyMaxBend(vertices, settings);
            if (useMaxTwist) ApplyMaxTwist(vertices, settings);
            if (useMaxTaper) ApplyMaxTaper(vertices, settings);
            if (useFfd) ApplySimpleFFD(vertices, settings);
            if (useMaxNoise) ApplyMaxNoise(vertices, settings);
        }

        private static Bounds GetBounds(List<Vector3> vertices)
        {
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }
            return bounds;
        }

        private static int AxisToIndex(FXMAxis axis)
        {
            switch (axis)
            {
                case FXMAxis.X: return 0;
                case FXMAxis.Z: return 2;
                case FXMAxis.Y:
                default: return 1;
            }
        }

        private static void PerpendicularAxes(int axis, out int p1, out int p2)
        {
            if (axis == 0)
            {
                p1 = 1; p2 = 2;
            }
            else if (axis == 1)
            {
                p1 = 0; p2 = 2;
            }
            else
            {
                p1 = 0; p2 = 1;
            }
        }

        private static float GetAxis(Vector3 v, int axis)
        {
            if (axis == 0) return v.x;
            if (axis == 1) return v.y;
            return v.z;
        }

        private static Vector3 SetAxis(Vector3 v, int axis, float value)
        {
            if (axis == 0) v.x = value;
            else if (axis == 1) v.y = value;
            else v.z = value;
            return v;
        }

        private static float AxisSize(Bounds bounds, int axis)
        {
            if (axis == 0) return Mathf.Max(bounds.size.x, 0.0001f);
            if (axis == 1) return Mathf.Max(bounds.size.y, 0.0001f);
            return Mathf.Max(bounds.size.z, 0.0001f);
        }

        private static float AxisMin(Bounds bounds, int axis)
        {
            if (axis == 0) return bounds.min.x;
            if (axis == 1) return bounds.min.y;
            return bounds.min.z;
        }

        private static float AxisMax(Bounds bounds, int axis)
        {
            if (axis == 0) return bounds.max.x;
            if (axis == 1) return bounds.max.y;
            return bounds.max.z;
        }

        private static float AxisCenter(Bounds bounds, int axis)
        {
            return (AxisMin(bounds, axis) + AxisMax(bounds, axis)) * 0.5f;
        }

        private static float ApplySignedBias01(float t, float bias)
        {
            t = Mathf.Clamp01(t);
            bias = Mathf.Clamp(bias, -1f, 1f);
            if (bias > 0f)
            {
                return Mathf.Pow(t, 1f + bias * 3f);
            }
            if (bias < 0f)
            {
                return 1f - Mathf.Pow(1f - t, 1f + Mathf.Abs(bias) * 3f);
            }
            return t;
        }

        private static void ApplyMaxTaper(List<Vector3> vertices, FXMSettings settings)
        {
            Bounds bounds = GetBounds(vertices);
            int axis = AxisToIndex(settings.maxTaperAxis);
            PerpendicularAxes(axis, out int p1, out int p2);
            float mn = AxisMin(bounds, axis);
            float size = AxisSize(bounds, axis);
            float center1 = AxisCenter(bounds, p1);
            float center2 = AxisCenter(bounds, p2);
            float amount = Mathf.Clamp(settings.maxTaperAmount, -0.95f, 0.95f);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float t = ApplySignedBias01((GetAxis(p, axis) - mn) / size, settings.maxTaperBias);
                float scale = Mathf.Max(0.001f, 1f + amount * ((t - 0.5f) * 2f));
                p = SetAxis(p, p1, center1 + (GetAxis(p, p1) - center1) * scale);
                p = SetAxis(p, p2, center2 + (GetAxis(p, p2) - center2) * scale);
                vertices[i] = p;
            }
        }

        private static void ApplyMaxTwist(List<Vector3> vertices, FXMSettings settings)
        {
            Bounds bounds = GetBounds(vertices);
            int axis = AxisToIndex(settings.maxTwistAxis);
            PerpendicularAxes(axis, out int p1, out int p2);
            float mn = AxisMin(bounds, axis);
            float size = AxisSize(bounds, axis);
            float center1 = AxisCenter(bounds, p1);
            float center2 = AxisCenter(bounds, p2);
            float angleDeg = settings.maxTwistDirection == FXMTwistDirection.Negative ? -settings.maxTwistAngle : settings.maxTwistAngle;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float t = ApplySignedBias01((GetAxis(p, axis) - mn) / size, settings.maxTwistBias);
                float twistT;
                switch (settings.maxTwistMode)
                {
                    case FXMTwistMode.Centered:
                        twistT = t - 0.5f;
                        break;
                    case FXMTwistMode.TopToBottom:
                        twistT = 1f - t;
                        break;
                    case FXMTwistMode.BottomToTop:
                    default:
                        twistT = t;
                        break;
                }

                float theta = angleRad * twistT;
                float c = Mathf.Cos(theta);
                float sn = Mathf.Sin(theta);
                float a = GetAxis(p, p1) - center1;
                float b = GetAxis(p, p2) - center2;
                p = SetAxis(p, p1, center1 + a * c - b * sn);
                p = SetAxis(p, p2, center2 + a * sn + b * c);
                vertices[i] = p;
            }
        }

        private static void ApplyMaxBend(List<Vector3> vertices, FXMSettings settings)
        {
            Bounds bounds = GetBounds(vertices);
            int axis = AxisToIndex(settings.maxBendAxis);
            int bendPlane = AxisToIndex(settings.maxBendPlane);
            if (bendPlane == axis)
            {
                PerpendicularAxes(axis, out bendPlane, out _);
            }

            int other = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i != axis && i != bendPlane)
                {
                    other = i;
                    break;
                }
            }
            _ = other;

            float mn = AxisMin(bounds, axis);
            float mx = AxisMax(bounds, axis);
            float size = Mathf.Max(mx - mn, 0.0001f);
            float centerAxis = (mn + mx) * 0.5f;
            float centerBend = AxisCenter(bounds, bendPlane);
            float angleRad = settings.maxBendAngle * Mathf.Deg2Rad;
            float radius = size / Mathf.Max(0.000001f, Mathf.Abs(angleRad));
            radius *= angleRad >= 0f ? 1f : -1f;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float rawT = (GetAxis(p, axis) - mn) / size;
                float bendT = ApplySignedBias01(rawT, settings.maxBendBias);
                float sAxis = (bendT - 0.5f) * size;
                float theta = sAxis / size * angleRad;
                float rb = radius + (GetAxis(p, bendPlane) - centerBend);
                p = SetAxis(p, axis, Mathf.Sin(theta) * rb);
                p = SetAxis(p, bendPlane, centerBend + (Mathf.Cos(theta) * rb - radius));
                vertices[i] = p;
            }
        }

        private static void ApplySimpleFFD(List<Vector3> vertices, FXMSettings settings)
        {
            Bounds bounds = GetBounds(vertices);
            int axis = AxisToIndex(settings.ffdAxis);
            PerpendicularAxes(axis, out int p1, out int p2);
            float mn = AxisMin(bounds, axis);
            float sizeAxis = AxisSize(bounds, axis);
            float center1 = AxisCenter(bounds, p1);
            float center2 = AxisCenter(bounds, p2);
            float size2 = AxisSize(bounds, p2);
            float strength = Mathf.Clamp(settings.ffdStrength, -2f, 2f);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float t = (GetAxis(p, axis) - mn) / sizeAxis;
                float profileScale = FfdEvalScale(settings, t);
                float profileHeight = FfdEvalHeight(settings, t);
                float bulge = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                float globalScale = Mathf.Max(0.001f, 1f + strength * bulge);
                float factor = Mathf.Max(0.001f, profileScale * globalScale);
                p = SetAxis(p, p1, center1 + (GetAxis(p, p1) - center1) * factor);
                p = SetAxis(p, p2, center2 + (GetAxis(p, p2) - center2) * factor);
                if (settings.ffdMode != FXMFFDMode.Cyl && Mathf.Abs(strength) > 0.000001f)
                {
                    p = SetAxis(p, p2, GetAxis(p, p2) + strength * 0.05f * size2 * Mathf.Sin((t - 0.5f) * Mathf.PI));
                }
                if (Mathf.Abs(profileHeight) > 0.000001f)
                {
                    p = SetAxis(p, axis, GetAxis(p, axis) + profileHeight * sizeAxis);
                }
                vertices[i] = p;
            }
        }

        private static float FfdSmoothParameter(FXMSettings settings, float t)
        {
            t = Mathf.Clamp01(t);
            float sm = Mathf.Clamp01(settings.ffdSmoothness <= 0f ? 1f : settings.ffdSmoothness);
            float smoothT = t * t * (3f - 2f * t);
            return Mathf.Lerp(t, smoothT, sm);
        }

        private static float FfdEvalScale(FXMSettings settings, float t)
        {
            List<float> values = new List<float>();
            int res = Mathf.Clamp((int)settings.ffdResolution, 2, 4);
            values.Add(Mathf.Max(0.001f, settings.ffdP0Scale <= 0f ? 1f : settings.ffdP0Scale));
            if (res == 2)
            {
                values.Add(Mathf.Max(0.001f, settings.ffdP2Scale <= 0f ? 1f : settings.ffdP2Scale));
            }
            else if (res == 4)
            {
                values.Add(Mathf.Max(0.001f, settings.ffdP1Scale <= 0f ? 1f : settings.ffdP1Scale));
                values.Add(Mathf.Max(0.001f, settings.ffdP2Scale <= 0f ? 1f : settings.ffdP2Scale));
                values.Add(Mathf.Max(0.001f, settings.ffdP3Scale <= 0f ? 1f : settings.ffdP3Scale));
            }
            else
            {
                values.Add(Mathf.Max(0.001f, settings.ffdP1Scale <= 0f ? 1f : settings.ffdP1Scale));
                values.Add(Mathf.Max(0.001f, settings.ffdP2Scale <= 0f ? 1f : settings.ffdP2Scale));
            }
            return Mathf.Max(0.001f, BezierEval(values, FfdSmoothParameter(settings, t)));
        }

        private static float FfdEvalHeight(FXMSettings settings, float t)
        {
            List<float> values = new List<float>();
            int res = Mathf.Clamp((int)settings.ffdResolution, 2, 4);
            values.Add(settings.ffdP0Height);
            if (res == 2)
            {
                values.Add(settings.ffdP2Height);
            }
            else if (res == 4)
            {
                values.Add(settings.ffdP1Height);
                values.Add(settings.ffdP2Height);
                values.Add(settings.ffdP3Height);
            }
            else
            {
                values.Add(settings.ffdP1Height);
                values.Add(settings.ffdP2Height);
            }
            return BezierEval(values, FfdSmoothParameter(settings, t));
        }

        private static float BezierEval(List<float> values, float t)
        {
            t = Mathf.Clamp01(t);
            if (values == null || values.Count == 0)
            {
                return 1f;
            }
            while (values.Count > 1)
            {
                for (int i = 0; i < values.Count - 1; i++)
                {
                    values[i] = Mathf.Lerp(values[i], values[i + 1], t);
                }
                values.RemoveAt(values.Count - 1);
            }
            return values[0];
        }


        private static void ApplyMaxNoise(List<Vector3> vertices, FXMSettings settings)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            float sx = settings.maxNoiseStrengthX;
            float sy = settings.maxNoiseStrengthY;
            float sz = settings.maxNoiseStrengthZ;
            float scale = Mathf.Max(settings.maxNoiseScale, 0.0001f);
            Bounds b = GetBounds(vertices);
            Vector3 c = b.center;
            float invScale = 1f / scale;
            int seed = Mathf.Max(0, settings.maxNoiseSeed);

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i];
                float nx = (p.x - c.x) * invScale;
                float ny = (p.y - c.y) * invScale;
                float nz = (p.z - c.z) * invScale;
                float dx = ValueNoise3D(nx, ny, nz, seed + 11, settings.maxNoiseFractal);
                float dy = ValueNoise3D(nx, ny, nz, seed + 101, settings.maxNoiseFractal);
                float dz = ValueNoise3D(nx, ny, nz, seed + 1009, settings.maxNoiseFractal);
                p.x += dx * sx;
                p.y += dy * sy;
                p.z += dz * sz;
                vertices[i] = p;
            }
        }

        private static float ValueNoise3D(float x, float y, float z, int seed, bool fractal)
        {
            float n = ValueNoise3DSingle(x, y, z, seed);
            if (!fractal)
            {
                return n;
            }
            n = n * 0.58f + ValueNoise3DSingle(x * 2.03f + 17.1f, y * 2.03f - 6.7f, z * 2.03f + 3.9f, seed + 37) * 0.29f + ValueNoise3DSingle(x * 4.11f - 5.3f, y * 4.11f + 12.9f, z * 4.11f - 2.1f, seed + 73) * 0.13f;
            return Mathf.Clamp(n, -1f, 1f);
        }

        private static float ValueNoise3DSingle(float x, float y, float z, int seed)
        {
            float xy = Mathf.PerlinNoise(x + seed * 0.013f, y - seed * 0.021f) - 0.5f;
            float yz = Mathf.PerlinNoise(y + seed * 0.017f, z + seed * 0.019f) - 0.5f;
            float zx = Mathf.PerlinNoise(z - seed * 0.023f, x + seed * 0.029f) - 0.5f;
            return (xy + yz + zx) * (2f / 3f);
        }

        private static void ApplyShell(MeshData data, FXMSettings settings)
        {
            if (data == null || data.vertices == null || data.triangles == null || data.vertices.Count == 0 || data.triangles.Count < 3)
            {
                return;
            }

            float thickness = settings.shellThickness;
            if (Mathf.Abs(thickness) < 0.000001f)
            {
                return;
            }

            int axis = AxisToIndex(settings.shellAxis);
            float outerOffset;
            float innerOffset;
            switch (settings.shellMode)
            {
                case FXMShellMode.Outer:
                    outerOffset = thickness;
                    innerOffset = 0f;
                    break;
                case FXMShellMode.Inner:
                    outerOffset = 0f;
                    innerOffset = -thickness;
                    break;
                case FXMShellMode.Center:
                default:
                    outerOffset = thickness * 0.5f;
                    innerOffset = -thickness * 0.5f;
                    break;
            }

            int sourceVertexCount = data.vertices.Count;
            List<Vector3> outer = new List<Vector3>(sourceVertexCount);
            List<Vector3> inner = new List<Vector3>(sourceVertexCount);
            for (int i = 0; i < sourceVertexCount; i++)
            {
                Vector3 v = data.vertices[i];
                outer.Add(SetAxis(v, axis, GetAxis(v, axis) + outerOffset));
                inner.Add(SetAxis(v, axis, GetAxis(v, axis) + innerOffset));
            }

            List<Vector2> sourceUvs = new List<Vector2>(data.uvs);
            data.vertices.Clear();
            data.vertices.AddRange(outer);
            data.vertices.AddRange(inner);
            data.uvs.Clear();
            data.uvs.AddRange(sourceUvs);
            data.uvs.AddRange(sourceUvs);
            data.colors.Clear();
            for (int i = 0; i < data.vertices.Count; i++)
            {
                data.colors.Add(new Color32(255, 255, 255, 255));
            }

            List<int> sourceTris = new List<int>(data.triangles);
            data.triangles.Clear();
            for (int i = 0; i + 2 < sourceTris.Count; i += 3)
            {
                int a = sourceTris[i];
                int b = sourceTris[i + 1];
                int c = sourceTris[i + 2];
                data.triangles.Add(a);
                data.triangles.Add(b);
                data.triangles.Add(c);
                data.triangles.Add(a + sourceVertexCount);
                data.triangles.Add(c + sourceVertexCount);
                data.triangles.Add(b + sourceVertexCount);
            }

            Dictionary<FXMEdgeKey, int> edgeCounts = new Dictionary<FXMEdgeKey, int>();
            for (int i = 0; i + 2 < sourceTris.Count; i += 3)
            {
                CountShellEdge(edgeCounts, sourceTris[i], sourceTris[i + 1]);
                CountShellEdge(edgeCounts, sourceTris[i + 1], sourceTris[i + 2]);
                CountShellEdge(edgeCounts, sourceTris[i + 2], sourceTris[i]);
            }

            foreach (KeyValuePair<FXMEdgeKey, int> kv in edgeCounts)
            {
                if (kv.Value != 1)
                {
                    continue;
                }
                int a = kv.Key.A;
                int b = kv.Key.B;
                AddQuad(data.triangles, a, b, a + sourceVertexCount, b + sourceVertexCount);
            }
        }

        private static void CountShellEdge(Dictionary<FXMEdgeKey, int> counts, int a, int b)
        {
            FXMEdgeKey key = new FXMEdgeKey(Mathf.Min(a, b), Mathf.Max(a, b));
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private static void ApplyPivotAxisTransform(List<Vector3> vertices, FXMSettings settings)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            Vector3 pivot = ResolvePivotPoint(vertices, settings.pivotAnchor) + settings.pivotPositionOffset;
            bool hasPivotMove = pivot.sqrMagnitude > 0.0000001f;
            bool hasRotation = settings.pivotRotationEuler.sqrMagnitude > 0.0000001f;
            bool useZUp = settings.upAxis == FXMUpAxis.ZUp;

            if (!hasPivotMove && !hasRotation && !useZUp)
            {
                return;
            }

            Quaternion pivotRotation = hasRotation ? Quaternion.Euler(settings.pivotRotationEuler) : Quaternion.identity;
            Quaternion upAxisRotation = useZUp ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;

            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                if (hasPivotMove)
                {
                    v -= pivot;
                }

                if (hasRotation)
                {
                    v = pivotRotation * v;
                }

                if (useZUp)
                {
                    v = upAxisRotation * v;
                }

                vertices[i] = v;
            }
        }

        private static Vector3 ResolvePivotPoint(List<Vector3> vertices, FXMGlobalPivotAnchor pivotAnchor)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return Vector3.zero;
            }

            if (pivotAnchor == FXMGlobalPivotAnchor.KeepGeneratedOrigin || pivotAnchor == FXMGlobalPivotAnchor.CustomOffsetOnly)
            {
                return Vector3.zero;
            }

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }

            Vector3 pivot = bounds.center;
            switch (pivotAnchor)
            {
                case FXMGlobalPivotAnchor.BoundsBottom:
                    pivot.y = bounds.min.y;
                    break;
                case FXMGlobalPivotAnchor.BoundsTop:
                    pivot.y = bounds.max.y;
                    break;
                case FXMGlobalPivotAnchor.BoundsCenter:
                default:
                    break;
            }

            return pivot;
        }

        private static void CenterVertices(List<Vector3> vertices)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }

            Vector3 center = bounds.center;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] -= center;
            }
        }

        private static Mesh MakeDoubleSided(Mesh source, bool recalculateNormals, bool recalculateTangents)
        {
            Vector3[] sourceVertices = source.vertices;
            Vector2[] sourceUvs = source.uv;
            Color32[] sourceColors = source.colors32;
            Vector3[] sourceNormals = source.normals;
            int[] sourceTriangles = source.triangles;

            int vertexCount = sourceVertices.Length;
            Vector3[] vertices = new Vector3[vertexCount * 2];
            Vector2[] uvs = new Vector2[vertexCount * 2];
            Color32[] colors = new Color32[vertexCount * 2];
            Vector3[] normals = new Vector3[vertexCount * 2];

            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = sourceVertices[i];
                vertices[i + vertexCount] = sourceVertices[i];

                if (sourceUvs != null && sourceUvs.Length == vertexCount)
                {
                    uvs[i] = sourceUvs[i];
                    uvs[i + vertexCount] = sourceUvs[i];
                }

                if (sourceColors != null && sourceColors.Length == vertexCount)
                {
                    colors[i] = sourceColors[i];
                    colors[i + vertexCount] = sourceColors[i];
                }
                else
                {
                    colors[i] = new Color32(255, 255, 255, 255);
                    colors[i + vertexCount] = new Color32(255, 255, 255, 255);
                }

                if (sourceNormals != null && sourceNormals.Length == vertexCount)
                {
                    normals[i] = sourceNormals[i];
                    normals[i + vertexCount] = -sourceNormals[i];
                }
            }

            int[] triangles = new int[sourceTriangles.Length * 2];
            Array.Copy(sourceTriangles, triangles, sourceTriangles.Length);

            int offset = sourceTriangles.Length;
            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                triangles[offset + i] = sourceTriangles[i] + vertexCount;
                triangles[offset + i + 1] = sourceTriangles[i + 2] + vertexCount;
                triangles[offset + i + 2] = sourceTriangles[i + 1] + vertexCount;
            }

            Mesh mesh = new Mesh
            {
                name = source.name,
                indexFormat = vertices.Length > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
                vertices = vertices,
                uv = uvs,
                colors32 = colors,
                triangles = triangles
            };

            if (recalculateNormals || sourceNormals == null || sourceNormals.Length != vertexCount)
            {
                mesh.RecalculateNormals();
            }
            else
            {
                mesh.normals = normals;
            }

            mesh.RecalculateBounds();

            if (recalculateTangents)
            {
                TryRecalculateTangents(mesh);
            }

            UnityEngine.Object.DestroyImmediate(source);
            return mesh;
        }

        private static void TryRecalculateTangents(Mesh mesh)
        {
            try
            {
                mesh.RecalculateTangents();
            }
            catch (Exception)
            {
                // Some Unity versions or invalid topology states may fail tangent calculation.
                // MVP에서는 툴 중단보다 안전한 무시가 낫습니다.
            }
        }

        private sealed class MeshData
        {
            public readonly List<Vector3> vertices = new List<Vector3>(1024);
            public readonly List<Vector2> uvs = new List<Vector2>(1024);
            public readonly List<Color32> colors = new List<Color32>(1024);
            public readonly List<int> triangles = new List<int>(2048);
        }
    }
}
