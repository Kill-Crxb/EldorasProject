using System.Collections.Generic;
using CrabSystem;
using MagicaCloth2;
using UnityEditor;
using UnityEngine;

// Tools → Characters → Add Clothing To Model. Staging: retire once body shapes graduate.
//
// Puts a clothing FBX's skinned meshes into a model prefab and makes them an equipment option on its
// ModelAppearance, so an item naming that group and option shows them while equipped. Clothing is weighted in
// Blender to the shared base armature, so each bone is bound by name to the model's own skeleton. Bones the
// clothing brings itself (a skirt's chains) are copied in under the model's bone of the same parent name at their
// authored pose; chains on an armature of their own (Armature.001) hang off Loose Bones Parent at the pose they
// have in model space. With Bone Cloth on they get a Magica BoneCloth (roots fixed, chains simulated, the leg
// capsules from Body Springs as colliders) that switches with the clothing. Collider bones picks whose colliders the
// cloth hits; a listed bone without one gets a starting sphere (Head, for hair) to size in the inspector. Mesh Cloth
// instead simulates the mesh itself (a tabard with no bones of its own); paint its fixed vertices in the Magica inspector, it won't run before.
// Meshes: a clothing FBX or prefab holding a whole set goes in one piece per run, ticking the meshes for that item. A missing group is made as an equipment group with an empty "none" option shown
// while unequipped. The meshes are saved hidden, because equipment groups only apply when an item goes on.
// Creation choice: the group is picked at character creation instead (a hair style); a missing one is made without
// the "none" option, so the first style is the default, unless Empty option asks for one (makeup, tattoos).
// No clothing FBX: the option is body layers only, a look painted on (makeup, a tattoo).
// Cloth preset: a Magica preset JSON (Database/Config/Cloth) tunes the cloth in place of the skirt start. Either way
// the roots don't turn with their chains, so what's weighted to them stays on the body.
// Body layers: materials drawn over the body while the option shows (an outfit painted on), bottom first; left
// empty, the option's layers are kept as they are.
// Fits: clothing made for some steps of a creation group (a cup for chest 0) goes in as a variant of the option,
// shown only on those steps; left on every step, it goes in the option's shown list.
// Clothing carrying the body's shape keys follows the creation sliders: wherever a creation option pushes a shape
// key, it pushes the clothing's key of the same name to the same weight. Rerunning replaces same-named meshes
// wherever they were listed, and the clothing's slider entries.

public class ClothingSetup : EditorWindow
{
    private const string UnequippedOption = "none";
    private const string ClothName = "Cloth";
    private const string ColliderPrefix = "Collider_";
    private const float SphereRadius = 0.09f;
    private const float SphereRise = 0.08f;

    [SerializeField] private GameObject clothing;
    [SerializeField] private GameObject model;
    [SerializeField] private Material material;
    [SerializeField] private string groupId = "body";
    [SerializeField] private string optionId = "";
    [SerializeField] private bool creation;
    [SerializeField] private bool emptyOption;
    [SerializeField] private string fitsGroup = "";
    [SerializeField] private List<string> fitsSteps = new List<string>();
    [SerializeField] private Material[] layers = new Material[0];
    [SerializeField] private bool boneCloth = true;
    [SerializeField] private bool meshCloth;
    [SerializeField] private TextAsset preset;
    [SerializeField] private List<string> skippedMeshes = new List<string>();
    [SerializeField] private string looseBonesParent = "Hips";
    [SerializeField] private List<string> colliderBones = new List<string> { "Thigh.L", "Thigh.R" };

    private Transform target;

    [MenuItem("Tools/Characters/Add Clothing To Model")]
    private static void Open() => GetWindow<ClothingSetup>("Add Clothing");

    private void OnGUI()
    {
        clothing = (GameObject)EditorGUILayout.ObjectField("Clothing FBX", clothing, typeof(GameObject), false);
        model = (GameObject)EditorGUILayout.ObjectField("Model prefab", model, typeof(GameObject), false);
        material = (Material)EditorGUILayout.ObjectField(new GUIContent("Material", "Empty keeps the FBX's materials."), material, typeof(Material), false);
        groupId = EditorGUILayout.TextField("Group", groupId);
        optionId = EditorGUILayout.TextField("Option", optionId);
        creation = EditorGUILayout.Toggle(new GUIContent("Creation choice", "Picked at character creation (a hair style), not driven by an item."), creation);
        emptyOption = EditorGUILayout.Toggle(new GUIContent("Empty option", "A new creation group starts with a 'none' option, the default (makeup, tattoos)."), emptyOption);
        FitsGUI();

        var self = new SerializedObject(this);
        EditorGUILayout.PropertyField(self.FindProperty("layers"), new GUIContent("Body layers", "Drawn over the body while equipped, bottom first."), true);
        EditorGUILayout.PropertyField(self.FindProperty("colliderBones"), new GUIContent("Collider bones", "Bones whose colliders the cloth hits. One without a collider gets a starting sphere."), true);
        self.ApplyModifiedProperties();
        boneCloth = EditorGUILayout.Toggle(new GUIContent("Bone cloth", "Simulate the clothing's own bones with Magica BoneCloth."), boneCloth);
        meshCloth = EditorGUILayout.Toggle(new GUIContent("Mesh cloth", "Simulate the ticked meshes themselves with Magica MeshCloth. Paint fixed vertices afterwards."), meshCloth);
        preset = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("Cloth preset", "A Magica preset JSON. Empty uses the skirt start."), preset, typeof(TextAsset), false);
        MeshesGUI();
        looseBonesParent = EditorGUILayout.TextField(new GUIContent("Loose bones parent", "Model bone that chains on an armature of their own hang off."), looseBonesParent);

        if (GUILayout.Button("Add")) Add();
    }

    private void MeshesGUI()
    {
        if (clothing == null) return;

        EditorGUILayout.LabelField("Meshes");
        foreach (var source in clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            bool on = !skippedMeshes.Contains(source.name);
            bool now = EditorGUILayout.ToggleLeft(source.name, on);
            if (now && !on) skippedMeshes.Remove(source.name);
            if (!now && on) skippedMeshes.Add(source.name);
        }
    }

    private void FitsGUI()
    {
        var appearance = model != null ? model.GetComponent<ModelAppearance>() : null;
        if (appearance == null) return;

        var names = new List<string> { "(every step)" };
        foreach (var group in appearance.Groups)
            if (group.chosenAtCreation) names.Add(group.groupId);

        int current = Mathf.Max(0, names.IndexOf(fitsGroup));
        int picked = EditorGUILayout.Popup(new GUIContent("Fits", "The creation group whose steps this mesh was made for."), current, names.ToArray());
        if (picked != current) fitsSteps.Clear();

        fitsGroup = picked == 0 ? "" : names[picked];
        if (picked == 0) return;

        foreach (var group in appearance.Groups)
            if (group.groupId == fitsGroup) StepToggles(group);
    }

    private void StepToggles(ModelAppearance.Group group)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Steps");
            foreach (var option in group.options)
            {
                bool on = fitsSteps.Contains(option.optionId);
                bool now = GUILayout.Toggle(on, option.optionId, "Button");
                if (now && !on) fitsSteps.Add(option.optionId);
                if (!now && on) fitsSteps.Remove(option.optionId);
            }
        }
    }

    // The Item Setup dashboard tab drives this too, one run per group of meshes. False when nothing was saved.
    internal bool Add()
    {
        string modelPath = model != null ? AssetDatabase.GetAssetPath(model) : "";
        string missing = clothing == null && layers.Length == 0 ? "Clothing FBX, or Body layers for a look painted on"
            : !modelPath.EndsWith(".prefab") ? "Model prefab (a .prefab from the Project window)"
            : string.IsNullOrEmpty(groupId) ? "Group"
            : string.IsNullOrEmpty(optionId) ? "Option"
            : "";
        if (missing != "")
        {
            Debug.LogError($"[ClothingSetup] Set {missing}.");
            return false;
        }

        if (!string.IsNullOrEmpty(fitsGroup) && fitsSteps.Count == 0)
        {
            Debug.LogError($"[ClothingSetup] Pick the {fitsGroup} steps this mesh fits.");
            return false;
        }

        var root = PrefabUtility.LoadPrefabContents(modelPath);
        bool built = Build(root);
        if (built) PrefabUtility.SaveAsPrefabAsset(root, modelPath);

        PrefabUtility.UnloadPrefabContents(root);
        return built;
    }

    // False leaves the prefab unsaved, so a failed run changes nothing.
    private bool Build(GameObject root)
    {
        var appearance = root.GetComponent<ModelAppearance>();
        var body = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (appearance == null || body == null)
        {
            Debug.LogError("[ClothingSetup] The model needs a ModelAppearance on its root and a skinned body mesh.", model);
            return false;
        }

        target = root.transform;
        var copies = new List<SkinnedMeshRenderer>();
        if (clothing != null && !BindMeshes(root, body, copies)) return false;

        return SetOption(appearance, copies, body);
    }

    private bool BindMeshes(GameObject root, SkinnedMeshRenderer body, List<SkinnedMeshRenderer> copies)
    {
        var skeleton = Skeleton(body);
        var bones = BonesByName(root);
        var colliders = Colliders(bones);
        foreach (var source in clothing.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skippedMeshes.Contains(source.name)) continue;

            var copy = CopyRenderer(source, body, bones);
            if (copy == null) return false;
            copies.Add(copy);

            var roots = OwnRoots(copy, skeleton);
            bool simulated = meshCloth || boneCloth && roots.Count > 0;
            if (!simulated) continue;

            var cloth = AddCloth(copy, meshCloth ? null : roots, colliders);
            if (preset != null) ApplyPreset(cloth);
        }

        if (copies.Count == 0)
        {
            Debug.LogError("[ClothingSetup] The clothing FBX has no skinned mesh. Export it with the armature.", clothing);
            return false;
        }

        return true;
    }

    // The body's bones and everything above them; any other bone in the model came with clothing.
    internal static HashSet<string> Skeleton(SkinnedMeshRenderer body)
    {
        var skeleton = new HashSet<string>();
        foreach (var bone in body.bones)
            for (Transform t = bone; t != null; t = t.parent)
                skeleton.Add(t.name);

        return skeleton;
    }

    // The colliders hung on each listed bone (Body Springs' capsules), or a starting sphere where there are none.
    private List<ColliderComponent> Colliders(Dictionary<string, Transform> bones)
    {
        var colliders = new List<ColliderComponent>();
        foreach (string boneName in colliderBones)
        {
            if (!bones.TryGetValue(boneName, out var bone))
            {
                Debug.LogError($"[ClothingSetup] Collider bone '{boneName}' isn't in {model.name}; skipped.", model);
                continue;
            }

            int before = colliders.Count;
            foreach (Transform child in bone)
                if (child.TryGetComponent<ColliderComponent>(out var collider)) colliders.Add(collider);

            if (colliders.Count == before) colliders.Add(Sphere(bone));
        }

        return colliders;
    }

    // Sized in metres whatever the rig's scale, raised along the bone to sit roughly in the middle of a head.
    private static MagicaSphereCollider Sphere(Transform bone)
    {
        var go = new GameObject(ColliderPrefix + bone.name);
        go.transform.SetParent(bone, false);
        go.transform.position = bone.position + bone.up * SphereRise;

        var sphere = go.AddComponent<MagicaSphereCollider>();
        sphere.SetSize(SphereRadius / go.transform.lossyScale.x);
        return sphere;
    }

    // The top bone of each chain the clothing brought, the one hanging off the body's skeleton.
    private static List<Transform> OwnRoots(SkinnedMeshRenderer copy, HashSet<string> skeleton)
    {
        var roots = new List<Transform>();
        foreach (var bone in copy.bones)
        {
            if (bone == null || skeleton.Contains(bone.name)) continue;

            Transform top = bone;
            while (top.parent != null && !skeleton.Contains(top.parent.name))
                top = top.parent;

            if (!roots.Contains(top)) roots.Add(top);
        }

        return roots;
    }

    // A child of the clothing, so it runs only while the clothing shows. BoneCloth on the given roots, or MeshCloth on
    // the clothing mesh when there are none. A starting tune for a skirt; tune on from the inspector.
    private static MagicaCloth AddCloth(SkinnedMeshRenderer copy, List<Transform> roots, List<ColliderComponent> colliders)
    {
        var go = new GameObject(ClothName);
        go.transform.SetParent(copy.transform, false);

        var cloth = go.AddComponent<MagicaCloth>();
        var data = cloth.SerializeData;
        if (roots == null)
        {
            data.clothType = ClothProcess.ClothType.MeshCloth;
            data.sourceRenderers = new List<Renderer> { copy };
            data.paintMode = ClothSerializeData.PaintMode.Manual;
        }
        else
        {
            data.clothType = ClothProcess.ClothType.BoneCloth;
            data.rootBones = roots;
            data.connectionMode = RenderSetupData.BoneConnectionMode.AutomaticMesh;
            data.rootRotation = 0f;
        }

        data.damping.SetValue(0.1f);
        data.radius.SetValue(0.02f);

        data.angleRestorationConstraint.useAngleRestoration = true;
        data.angleRestorationConstraint.stiffness.SetValue(0.3f, 1f, 0.3f);
        data.angleLimitConstraint.useAngleLimit = true;
        data.angleLimitConstraint.limitAngle.SetValue(50f);

        var inertia = data.inertiaConstraint;
        inertia.localInertia = 0.8f;
        inertia.localMovementSpeedLimit.SetValue(true, 3f);
        inertia.localRotationSpeedLimit.SetValue(true, 540f);
        inertia.particleSpeedLimit.SetValue(true, 3f);
        inertia.teleportMode = InertiaConstraint.TeleportMode.Reset;

        data.colliderCollisionConstraint.mode = ColliderCollisionConstraint.Mode.Edge;
        data.colliderCollisionConstraint.colliderList = new List<ColliderComponent>(colliders);
        return cloth;
    }

    // Over the start AddCloth gave it; Magica's import keeps the roots, root rotation, renderers and colliders.
    private void ApplyPreset(MagicaCloth cloth)
    {
        if (!cloth.SerializeData.ImportJson(preset.text)) Debug.LogError($"[ClothingSetup] {preset.name} isn't a Magica preset.", preset);
    }

    private static Dictionary<string, Transform> BonesByName(GameObject root)
    {
        var bones = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            bones.TryAdd(t.name, t);

        return bones;
    }

    // Sits beside the body mesh and copies its renderer settings, so outlines and shadows match.
    private SkinnedMeshRenderer CopyRenderer(SkinnedMeshRenderer source, SkinnedMeshRenderer body, Dictionary<string, Transform> bones)
    {
        var mapped = MapBones(source, bones);
        if (mapped == null) return null;

        Transform parent = body.transform.parent;
        Transform existing = parent.Find(source.name);
        if (existing == body.transform)
        {
            Debug.LogError($"[ClothingSetup] The clothing mesh is named {source.name}, the same as the body. Rename it in Blender.", clothing);
            return null;
        }

        if (existing != null) DestroyImmediate(existing.gameObject);

        var go = new GameObject(source.name);
        go.layer = body.gameObject.layer;
        go.transform.SetParent(parent, false);

        var renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = source.sharedMesh;
        renderer.sharedMaterials = Materials(source);
        renderer.bones = mapped;
        renderer.rootBone = source.rootBone != null && bones.TryGetValue(source.rootBone.name, out var rootBone) ? rootBone : body.rootBone;
        renderer.localBounds = source.localBounds;
        renderer.shadowCastingMode = body.shadowCastingMode;
        renderer.receiveShadows = body.receiveShadows;
        renderer.renderingLayerMask = body.renderingLayerMask;
        renderer.updateWhenOffscreen = body.updateWhenOffscreen;

        go.SetActive(false);
        return renderer;
    }

    private Transform[] MapBones(SkinnedMeshRenderer source, Dictionary<string, Transform> bones)
    {
        var mapped = new Transform[source.bones.Length];
        for (int i = 0; i < mapped.Length; i++)
        {
            mapped[i] = source.bones[i] != null ? EnsureBone(source.bones[i], bones) : null;
            if (mapped[i] != null) continue;

            string boneName = source.bones[i] != null ? source.bones[i].name : "(missing)";
            Debug.LogError($"[ClothingSetup] {source.name} is weighted to bone '{boneName}', and neither a parent of it nor '{looseBonesParent}' is in {model.name}.", clothing);
            return null;
        }

        return mapped;
    }

    // The model's bone of that name, or a copy of the clothing's bone hung under the model's bone of its parent's name.
    private Transform EnsureBone(Transform source, Dictionary<string, Transform> bones)
    {
        if (bones.TryGetValue(source.name, out var found)) return found;
        if (source.parent == null || source.parent == clothing.transform) return null;

        bool loose = IsLooseArmature(source.parent, bones);
        Transform parent = null;
        if (loose) bones.TryGetValue(looseBonesParent, out parent);
        else parent = EnsureBone(source.parent, bones);
        if (parent == null) return null;

        var bone = new GameObject(source.name).transform;
        bone.SetParent(parent, false);
        bone.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
        bone.localScale = source.localScale;
        if (loose) MatchModelSpacePose(bone, source);

        bones[source.name] = bone;
        return bone;
    }

    // A top-level object of the clothing FBX the model has no match for: a second armature.
    private bool IsLooseArmature(Transform t, Dictionary<string, Transform> bones)
    {
        return t.parent == clothing.transform && !bones.ContainsKey(t.name);
    }

    // Where the bone sits relative to the clothing FBX's root is where it goes relative to the model's root, so the
    // skin's bind poses still line up.
    private void MatchModelSpacePose(Transform bone, Transform source)
    {
        Transform from = clothing.transform;
        bone.position = target.TransformPoint(from.InverseTransformPoint(source.position));
        bone.rotation = target.rotation * (Quaternion.Inverse(from.rotation) * source.rotation);

        Vector3 wanted = Vector3.Scale(Divide(source.lossyScale, from.lossyScale), target.lossyScale);
        bone.localScale = Divide(wanted, bone.parent.lossyScale);
    }

    private static Vector3 Divide(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }

    private Material[] Materials(SkinnedMeshRenderer source)
    {
        if (material == null) return source.sharedMaterials;

        var materials = new Material[source.sharedMesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = material;

        return materials;
    }

    private bool SetOption(ModelAppearance appearance, List<SkinnedMeshRenderer> copies, SkinnedMeshRenderer body)
    {
        var so = new SerializedObject(appearance);
        var groups = so.FindProperty("groups");
        var group = Element(groups, "groupId", groupId, out bool newGroup);

        if (!newGroup && group.FindPropertyRelative("chosenAtCreation").boolValue != creation)
        {
            string kind = creation ? "an equipment" : "a creation";
            Debug.LogError($"[ClothingSetup] Group '{groupId}' is {kind} group; set Creation choice to match.", appearance);
            return false;
        }

        if (newGroup) ResetGroup(group);

        var option = Element(group.FindPropertyRelative("options"), "optionId", optionId, out bool newOption);
        if (newOption) ResetOption(option, ObjectNames.NicifyVariableName(optionId));

        DropMissing(option);
        if (string.IsNullOrEmpty(fitsGroup))
            AddObjects(option.FindPropertyRelative("show"), copies);
        else if (copies.Count > 0)
            AddVariant(option.FindPropertyRelative("variants"), copies);

        if (layers.Length > 0) SetLayers(option.FindPropertyRelative("layers"), body);

        FollowBodyShapes(groups, copies);
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // One stack on the body per option; a rerun with layers replaces it.
    private void SetLayers(SerializedProperty optionLayers, SkinnedMeshRenderer body)
    {
        optionLayers.arraySize = 1;
        var stack = optionLayers.GetArrayElementAtIndex(0);
        stack.FindPropertyRelative("renderer").objectReferenceValue = body;

        var materials = stack.FindPropertyRelative("materials");
        materials.arraySize = layers.Length;
        for (int i = 0; i < layers.Length; i++)
            materials.GetArrayElementAtIndex(i).objectReferenceValue = layers[i];
    }

    // A replaced mesh leaves a missing reference wherever it was listed; a variant left with nothing goes too.
    private static void DropMissing(SerializedProperty option)
    {
        DropNulls(option.FindPropertyRelative("show"));

        var variants = option.FindPropertyRelative("variants");
        for (int i = variants.arraySize - 1; i >= 0; i--)
        {
            var show = variants.GetArrayElementAtIndex(i).FindPropertyRelative("show");
            DropNulls(show);
            if (show.arraySize == 0) variants.DeleteArrayElementAtIndex(i);
        }
    }

    private static void DropNulls(SerializedProperty objects)
    {
        for (int i = objects.arraySize - 1; i >= 0; i--)
            if (objects.GetArrayElementAtIndex(i).objectReferenceValue == null)
                objects.DeleteArrayElementAtIndex(i);
    }

    private static void AddObjects(SerializedProperty objects, List<SkinnedMeshRenderer> copies)
    {
        foreach (var copy in copies)
        {
            objects.arraySize++;
            objects.GetArrayElementAtIndex(objects.arraySize - 1).objectReferenceValue = copy.gameObject;
        }
    }

    private void AddVariant(SerializedProperty variants, List<SkinnedMeshRenderer> copies)
    {
        variants.arraySize++;
        var variant = variants.GetArrayElementAtIndex(variants.arraySize - 1);
        variant.FindPropertyRelative("groupId").stringValue = fitsGroup;

        var steps = variant.FindPropertyRelative("optionIds");
        steps.arraySize = fitsSteps.Count;
        for (int i = 0; i < fitsSteps.Count; i++)
            steps.GetArrayElementAtIndex(i).stringValue = fitsSteps[i];

        var show = variant.FindPropertyRelative("show");
        show.arraySize = 0;
        AddObjects(show, copies);
    }

    private static void FollowBodyShapes(SerializedProperty groups, List<SkinnedMeshRenderer> copies)
    {
        for (int g = 0; g < groups.arraySize; g++)
        {
            var group = groups.GetArrayElementAtIndex(g);
            if (!group.FindPropertyRelative("chosenAtCreation").boolValue) continue;

            var options = group.FindPropertyRelative("options");
            for (int o = 0; o < options.arraySize; o++)
                FollowOption(options.GetArrayElementAtIndex(o).FindPropertyRelative("shapes"), copies);
        }
    }

    // Entries left pointing at a replaced mesh are dropped first, so a rerun doesn't pile them up.
    private static void FollowOption(SerializedProperty shapes, List<SkinnedMeshRenderer> copies)
    {
        for (int i = shapes.arraySize - 1; i >= 0; i--)
            if (shapes.GetArrayElementAtIndex(i).FindPropertyRelative("renderer").objectReferenceValue == null)
                shapes.DeleteArrayElementAtIndex(i);

        int existing = shapes.arraySize;
        for (int i = 0; i < existing; i++)
        {
            var entry = shapes.GetArrayElementAtIndex(i);
            string shape = entry.FindPropertyRelative("shape").stringValue;
            float weight = entry.FindPropertyRelative("weight").floatValue;

            foreach (var copy in copies)
                AddShape(shapes, copy, shape, weight);
        }
    }

    private static void AddShape(SerializedProperty shapes, SkinnedMeshRenderer renderer, string shape, float weight)
    {
        if (renderer.sharedMesh.GetBlendShapeIndex(shape) < 0) return;

        for (int i = 0; i < shapes.arraySize; i++)
        {
            var entry = shapes.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("renderer").objectReferenceValue == renderer && entry.FindPropertyRelative("shape").stringValue == shape)
                return;
        }

        shapes.arraySize++;
        var added = shapes.GetArrayElementAtIndex(shapes.arraySize - 1);
        added.FindPropertyRelative("renderer").objectReferenceValue = renderer;
        added.FindPropertyRelative("shape").stringValue = shape;
        added.FindPropertyRelative("weight").floatValue = weight;
    }

    // A new array element copies the one before it, so everything it shouldn't inherit is reset.
    private static SerializedProperty Element(SerializedProperty array, string idField, string id, out bool added)
    {
        added = false;
        for (int i = 0; i < array.arraySize; i++)
        {
            var element = array.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative(idField).stringValue == id) return element;
        }

        array.arraySize++;
        var created = array.GetArrayElementAtIndex(array.arraySize - 1);
        created.FindPropertyRelative(idField).stringValue = id;
        added = true;
        return created;
    }

    private void ResetGroup(SerializedProperty group)
    {
        group.FindPropertyRelative("displayName").stringValue = ObjectNames.NicifyVariableName(groupId);
        group.FindPropertyRelative("chosenAtCreation").boolValue = creation;
        group.FindPropertyRelative("slider").boolValue = false;
        group.FindPropertyRelative("unequippedOptionId").stringValue = creation ? "" : UnequippedOption;

        var options = group.FindPropertyRelative("options");
        options.arraySize = creation && !emptyOption ? 0 : 1;
        if (options.arraySize == 0) return;

        var none = options.GetArrayElementAtIndex(0);
        none.FindPropertyRelative("optionId").stringValue = UnequippedOption;
        ResetOption(none, "None");
    }

    private static void ResetOption(SerializedProperty option, string displayName)
    {
        option.FindPropertyRelative("displayName").stringValue = displayName;
        option.FindPropertyRelative("show").arraySize = 0;
        option.FindPropertyRelative("swaps").arraySize = 0;
        option.FindPropertyRelative("shapes").arraySize = 0;
        option.FindPropertyRelative("variants").arraySize = 0;
        option.FindPropertyRelative("layers").arraySize = 0;
    }
}
