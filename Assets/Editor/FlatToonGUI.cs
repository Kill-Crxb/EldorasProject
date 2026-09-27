// Editor-only. Lives in Assets/CrabSystem/Editor.
//
// Material inspector for Crab/FlatToon. The shader has around sixty properties; without
// this you scroll past fifty that do not apply to find the four that do.
//
// Two jobs:
//   1. Sections that collapse, and properties that only appear when their feature is on.
//   2. A Role at the top. Roles hide whole sections that are wrong for them (hair specular
//      on skin, face controls on cloth) and can stamp a starting preset. Custom shows
//      everything - switch to it when you want a feature the current role hides.
//
// Role is stored on the material in the hidden _Role property, so it survives reloads.

using UnityEditor;
using UnityEngine;

public class FlatToonGUI : ShaderGUI
{
    enum Role
    {
        Custom = 0,
        Skin = 1,
        Face = 2,
        Hair = 3,
        Cloth = 4,
        Prop = 5,
    }

    const string PrefPrefix = "CrabFlatToon.";

    MaterialEditor editor;
    MaterialProperty[] properties;
    Material material;
    Role role;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        editor = materialEditor;
        properties = props;
        material = materialEditor.target as Material;

        if (material == null)
            return;

        role = (Role)Get(material, "_Role", 0f);

        DrawRoleBar();
        EditorGUILayout.Space(6);

        DrawSurface();
        DrawShading();
        DrawAmbient();
        DrawFace();
        DrawBaseSDF();
        DrawBaseLight();
        DrawShadowPattern();
        DrawToonSpecular();
        DrawHairSpecular();
        DrawRim();
        DrawEmission();
        DrawOutline();

        EditorGUILayout.Space(8);
        DrawWarnings();

        EditorGUILayout.Space(4);
        editor.RenderQueueField();
        editor.EnableInstancingField();
        editor.DoubleSidedGIField();
    }

    // ------------------------------------------------------------------ role

    void DrawRoleBar()
    {
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        Role picked = (Role)EditorGUILayout.EnumPopup("Role", role);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObjects(editor.targets, "Change FlatToon Role");

            for (int i = 0; i < editor.targets.Length; i++)
                (editor.targets[i] as Material).SetFloat("_Role", (float)picked);

            role = picked;
        }

        if (role == Role.Custom)
            return;

        if (GUILayout.Button("Apply " + role + " Preset"))
            ApplyPreset(role);
    }

    void ApplyPreset(Role target)
    {
        Undo.RecordObjects(editor.targets, "Apply FlatToon Preset");

        for (int i = 0; i < editor.targets.Length; i++)
            ApplyPresetTo(editor.targets[i] as Material, target);
    }

    void ApplyPresetTo(Material mat, Role target)
    {
        if (mat == null)
            return;

        // Shared starting point, then each role overrides what it cares about.
        Set(mat, "_ShadeColor", new Color(0.78f, 0.68f, 0.70f, 1f));
        Set(mat, "_ShadeThreshold", 0f);
        Set(mat, "_ShadeSoftness", 0.015f);
        Set(mat, "_Bands", 1f);
        Set(mat, "_LightColorInfluence", 0f);
        Set(mat, "_ShadowStrength", 1f);
        Set(mat, "_OutlineWidth", 0.006f);
        Set(mat, "_OutlineZOffset", 0f);
        Set(mat, "_IsFace", 0f);
        SetFeature(mat, "_UseOutlineTint", "_OUTLINE_TINT", true);
        SetFeature(mat, "_UseSpecular", "_TOON_SPECULAR", false);
        SetFeature(mat, "_UseHairSpecular", "_HAIR_SPECULAR", false);
        SetFeature(mat, "_AlphaClip", "_ALPHATEST_ON", false);
        Set(mat, "_Cull", 2f);

        if (target == Role.Skin)
        {
            Set(mat, "_ShadeThreshold", -0.10f);
            Set(mat, "_ShadeSoftness", 0.020f);
            Set(mat, "_ShadowStrength", 0.4f);
            Set(mat, "_OutlineWidth", 0.005f);
        }

        if (target == Role.Face)
        {
            Set(mat, "_ShadeThreshold", -0.10f);
            Set(mat, "_ShadeSoftness", 0.020f);
            Set(mat, "_ShadowStrength", 0.4f);
            Set(mat, "_OutlineWidth", 0.004f);
            Set(mat, "_IsFace", 1f);
            Set(mat, "_FaceShadowLift", 0.55f);
            Set(mat, "_ShadowPosOffset", 0.1f);
            Set(mat, "_OutlineZOffset", 0.03f);
        }

        if (target == Role.Hair)
        {
            Set(mat, "_ShadeSoftness", 0.010f);
            Set(mat, "_OutlineWidth", 0.008f);
            Set(mat, "_Cull", 0f);
            SetFeature(mat, "_UseHairSpecular", "_HAIR_SPECULAR", true);
            SetFeature(mat, "_AlphaClip", "_ALPHATEST_ON", true);
        }

        if (target == Role.Cloth)
        {
            Set(mat, "_OutlineWidth", 0.006f);
        }

        if (target == Role.Prop)
        {
            Set(mat, "_OutlineWidth", 0.003f);
            SetFeature(mat, "_UseOutlineTint", "_OUTLINE_TINT", false);
        }

        EditorUtility.SetDirty(mat);
    }

    // Roles hide sections that are actively wrong for them. Custom hides nothing.
    bool ShowFor(Role a)
    {
        return role == Role.Custom || role == a;
    }

    bool ShowFor(Role a, Role b)
    {
        return role == Role.Custom || role == a || role == b;
    }

    bool ShowFor(Role a, Role b, Role c)
    {
        return role == Role.Custom || role == a || role == b || role == c;
    }

    bool ShowFor(Role a, Role b, Role c, Role d)
    {
        return role == Role.Custom || role == a || role == b || role == c || role == d;
    }

    // --------------------------------------------------------------- sections

    void DrawSurface()
    {
        if (!Section("Surface", "surface", true))
            return;

        Prop("_BaseMap");
        Prop("_BaseColor");
        Prop("_Cull");

        if (Feature("_AlphaClip"))
            Prop("_Cutoff");

        Prop("_ZOffset");
    }

    void DrawShading()
    {
        if (!Section("Shading", "shading", true))
            return;

        Prop("_ShadeColor");

        bool ramp = Feature("_UseRamp");

        if (ramp)
        {
            Prop("_ShadeRamp");
            EditorGUILayout.HelpBox("The ramp replaces Terminator Position, Softness and Tone Count. Import it with sRGB off and Wrap Mode Clamp.", MessageType.None);
        }

        if (!ramp)
        {
            Prop("_ShadeThreshold");
            Prop("_ShadeSoftness");
            Prop("_Bands");
        }

        Prop("_LightColorInfluence");
        Prop("_ShadowStrength");
        Prop("_AdditionalLightScale");
    }

    void DrawAmbient()
    {
        if (!Section("Ambient", "ambient", false))
            return;

        Prop("_IndirectMinColor");
        Prop("_IndirectStrength");
    }

    void DrawFace()
    {
        if (!ShowFor(Role.Face))
            return;

        if (!Section("Face", "face", true))
            return;

        Prop("_IsFace");

        if (Get(material, "_IsFace", 0f) < 0.5f)
            return;

        Prop("_FaceShadowLift");
        Prop("_ShadowPosOffset");
    }

    void DrawBaseSDF()
    {
        if (!ShowFor(Role.Face, Role.Skin, Role.Hair, Role.Cloth))
            return;

        if (!Section("Base SDF", "sdf", false))
            return;

        if (!Feature("_UseBaseSDF"))
            return;

        Prop("_BaseSDF");
        Prop("_BaseSDFCoverage");
        Prop("_BaseSDFSoftness");
        Prop("_BaseSDFFill");
        Prop("_BaseSDFDepth");
        EditorGUILayout.Space(2);
        Prop("_BaseSDFHighlightCoverage");
        Prop("_BaseSDFHighlight");
        Prop("_BaseSDFHighlightColor");
    }

    void DrawBaseLight()
    {
        if (!Section("Base Light", "baselight", false))
            return;

        if (!Feature("_UseBaseLight"))
            return;

        Prop("_BaseLightDir");
        Prop("_BaseLightStrength");
        Prop("_BaseLightThreshold");
        Prop("_BaseLightSoftness");
    }

    void DrawShadowPattern()
    {
        if (!Section("Shadow Pattern", "pattern", false))
            return;

        if (!Feature("_UseShadowTex"))
            return;

        Prop("_ShadowTex");
        Prop("_ShadowTexMode");
        Prop("_ShadowTexScale");
        Prop("_ShadowTexAmount");
        Prop("_ShadowTexSoftness");
        Prop("_ShadowTexBlend");
        EditorGUILayout.HelpBox("Pattern textures tile - import with Wrap Mode Repeat and sRGB off. Coverage controls dot size, not just density.", MessageType.None);
    }

    void DrawToonSpecular()
    {
        if (!ShowFor(Role.Cloth, Role.Prop, Role.Skin))
            return;

        if (!Section("Toon Specular", "spec", false))
            return;

        if (!Feature("_UseSpecular"))
            return;

        Prop("_SpecularColor");
        Prop("_SpecularSize");
        Prop("_SpecularSoftness");
    }

    void DrawHairSpecular()
    {
        if (!ShowFor(Role.Hair))
            return;

        if (!Section("Hair Specular", "hairspec", true))
            return;

        if (!Feature("_UseHairSpecular"))
            return;

        Prop("_HairSpecColor");
        Prop("_HairShiftMap");
        Prop("_HairShiftStrength");
        Prop("_HairSpecShift");
        Prop("_HairSpecPower");
        Prop("_HairSpecThreshold");
        Prop("_HairSpecSoftness");

        if (Feature("_UseSpecular"))
            EditorGUILayout.HelpBox("Toon Specular is also on. Use one or the other - together they double the highlight.", MessageType.Warning);
    }

    void DrawRim()
    {
        if (!Section("Rim", "rim", false))
            return;

        if (!Feature("_UseRim"))
            return;

        Prop("_RimColor");
        Prop("_RimSize");
        Prop("_RimSoftness");
        Prop("_RimMaskShadow");
    }

    void DrawEmission()
    {
        if (!Section("Emission", "emission", false))
            return;

        if (!Feature("_UseEmission"))
            return;

        Prop("_EmissionColor");
        Prop("_EmissionMap");
    }

    void DrawOutline()
    {
        if (!Section("Outline", "outline", true))
            return;

        Prop("_OutlineColor");
        Prop("_OutlineWidth");
        Prop("_OutlineSilhouetteBias");
        Prop("_OutlineWidthMap");
        Prop("_UseSmoothedNormals");

        EditorGUILayout.Space(2);

        if (Feature("_UseOutlineTint"))
        {
            Prop("_OutlineTintStrength");
            Prop("_OutlineTintDarkness");
        }

        EditorGUILayout.Space(2);
        Prop("_OutlineZOffset");
        Prop("_OutlineZOffsetMask");
        Prop("_OutlineFadeNear");
        Prop("_OutlineFadeFar");
    }

    // --------------------------------------------------------------- warnings

    void DrawWarnings()
    {
        float width = Get(material, "_OutlineWidth", 0f);
        bool outlineOn = width > 0.0001f;
        bool smoothed = material.IsKeywordEnabled("_SMOOTHED_NORMALS");

        if (outlineOn && !smoothed)
            EditorGUILayout.HelpBox("Outline is on but Extrude Along Baked Normals is off. On any mesh with hard edges or UV splits the hull will tear and the line will come out dashed.", MessageType.Warning);

        if (smoothed)
            EditorGUILayout.HelpBox("Baked normals require the mesh to be baked too: select the FBX and run Tools > Toon > Enable Smooth Normals On Selection.", MessageType.None);

        float near = Get(material, "_OutlineFadeNear", 0f);
        float far = Get(material, "_OutlineFadeFar", 0f);

        if (outlineOn && far <= near)
            EditorGUILayout.HelpBox("Outline Fade Far is not greater than Fade Near, so the outline is faded out everywhere.", MessageType.Warning);
    }

    // ---------------------------------------------------------------- helpers

    bool Section(string title, string key, bool openByDefault)
    {
        string prefKey = PrefPrefix + key;
        bool open = EditorPrefs.GetBool(prefKey, openByDefault);

        EditorGUI.BeginChangeCheck();
        bool next = EditorGUILayout.Foldout(open, title, true, EditorStyles.foldoutHeader);

        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetBool(prefKey, next);

        return next;
    }

    MaterialProperty Find(string name)
    {
        return FindProperty(name, properties, false);
    }

    void Prop(string name)
    {
        MaterialProperty property = Find(name);

        if (property == null)
            return;

        editor.ShaderProperty(property, property.displayName);
    }

    // Draws the feature's own toggle and reports whether it is on. Drawing through
    // ShaderProperty keeps the [Toggle(_KEYWORD)] drawer in charge of keyword state.
    bool Feature(string toggleName)
    {
        Prop(toggleName);

        MaterialProperty property = Find(toggleName);
        return property != null && property.floatValue > 0.5f;
    }

    // Materials authored before a property existed will not have it yet.
    static float Get(Material material, string name, float fallback)
    {
        return material.HasProperty(name) ? material.GetFloat(name) : fallback;
    }

    static void Set(Material material, string name, float value)
    {
        if (material.HasProperty(name))
            material.SetFloat(name, value);
    }

    static void Set(Material material, string name, Color value)
    {
        if (material.HasProperty(name))
            material.SetColor(name, value);
    }

    static void SetFeature(Material material, string name, string keyword, bool on)
    {
        Set(material, name, on ? 1f : 0f);

        if (on)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }
}
