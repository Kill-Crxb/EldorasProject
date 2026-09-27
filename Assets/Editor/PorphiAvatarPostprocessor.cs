using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Porphi.EditorTools
{
    /// Configures the humanoid avatar on import using the bone map the Blender
    /// addon wrote next to the FBX, so the mapping never has to be clicked
    /// through in the Avatar Configure window.
    ///
    /// Drop this in an Editor folder. It only touches models that have a
    /// matching "<name>_rig.json" beside them.
    public class PorphiAvatarPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            // "Porphi@Idle.fbx" is a clip for the "Porphi" model. It must copy that
            // model's avatar -- left to build its own, Unity maps the clip's
            // skeleton from scratch and retargets between two avatars, which shows
            // up as a character of the wrong size in a broken pose.
            if (ConfigureClip()) return;

            RigData data = LoadSidecar(assetPath);
            if (data == null || data.humanoid == null || data.humanoid.Length == 0) return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapeNormals = ModelImporterNormals.Calculate;
            importer.weldVertices = false;          // preserves the toon shading splits
            importer.importCameras = false;
            importer.importLights = false;

            HumanDescription description = importer.humanDescription;
            description.human = BuildHumanBones(data);
            description.upperArmTwist = 0.5f;
            description.lowerArmTwist = 0.5f;
            description.upperLegTwist = 0.5f;
            description.lowerLegTwist = 0.5f;
            description.armStretch = 0.05f;
            description.legStretch = 0.05f;
            description.feetSpacing = 0f;
            importer.humanDescription = description;
        }

        static HumanBone[] BuildHumanBones(RigData data)
        {
            var valid = new HashSet<string>(HumanTrait.BoneName);
            var bones = new List<HumanBone>();

            foreach (HumanoidEntry entry in data.humanoid)
            {
                string unityName = MatchHumanName(entry.unity, valid);
                if (unityName == null)
                {
                    Debug.LogWarning($"Porphi: '{entry.unity}' is not a HumanBodyBones name; skipped.");
                    continue;
                }

                bones.Add(new HumanBone
                {
                    humanName = unityName,
                    boneName = entry.bone,
                    limit = new HumanLimit { useDefaultValues = true },
                });
            }
            return bones.ToArray();
        }

        /// HumanBone.humanName must match a HumanTrait.BoneName entry exactly.
        /// Comparing with spaces stripped works whichever spelling this Unity
        /// version uses ("LeftUpperArm" or "Left Upper Arm").
        static string MatchHumanName(string enumName, HashSet<string> valid)
        {
            foreach (string candidate in valid)
                if (candidate.Replace(" ", "") == enumName)
                    return candidate;
            return null;
        }

        /// Wires "Model@Clip.fbx" to Model.fbx's avatar. Returns true if this asset
        /// is a clip, handled or not.
        bool ConfigureClip()
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath);
            string modelName = ModelPrefix(assetPath);
            if (modelName == null) return false;

            string modelPath = $"{folder}/{modelName}.fbx";
            if (!System.IO.File.Exists(modelPath)) return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;

            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(modelPath);
            if (avatar == null)
            {
                // The model may not have imported yet on a first bulk import.
                // Reimporting the clip afterwards picks it up.
                Debug.LogWarning($"Porphi: no avatar at '{modelPath}' yet for clip " +
                                 $"'{name}'. Reimport this clip once the model is in.");
                return true;
            }

            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            return true;
        }

        /// "Porphi@Idle" and "Porphi_Idle" are both clips of "Porphi". Returns the
        /// model name, or null when this looks like a model in its own right.
        /// "@" is Unity's own convention; "_" is what self-contained clips use,
        /// since a file carrying a mesh must not be collected onto another model.
        static string ModelPrefix(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            int cut = name.IndexOfAny(new[] { '@', '_' });
            return cut > 0 ? name.Substring(0, cut) : null;
        }

        static RigData LoadSidecar(string modelPath)
        {
            string folder = System.IO.Path.GetDirectoryName(modelPath);
            string name = System.IO.Path.GetFileNameWithoutExtension(modelPath);

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/{name}_rig.json");
            if (asset == null)
            {
                // A self-contained clip shares the model's sidecar.
                string prefix = ModelPrefix(modelPath);
                if (prefix != null)
                    asset = AssetDatabase.LoadAssetAtPath<TextAsset>($"{folder}/{prefix}_rig.json");
            }
            return asset != null ? RigData.Parse(asset) : null;
        }
    }
}
