using System;
using UnityEngine;

// STAGING: drives Crab/FlatToon Skin Layers (Staging) on one character. Gives the renderer its own
// instance of the skin material at Awake, then writes the chest step, makeup tints, tattoo picks,
// cloth layers and stealth fade onto it. An instance rather than a property block, because a
// renderer carrying a property block drops out of the SRP Batcher for as long as it holds one.
//
// On promotion this folds into ModelAppearance in place of its MaterialLayer stacking: that class
// rewrites sharedMaterials on every swap and layer change, which would drop this instance. Until
// then, test on a model whose appearance groups don't layer or swap the skin slot, or call Apply
// after they do.
//
// Per-mesh authoring values (chest slot offset, chest island rect) stay on the material; everything
// here is per character.

namespace CrabSystem
{
    public enum ClothLayer { Lining, Underwear, Sheer, Overlay, Clothes, Accessories }

    [Serializable]
    public struct SkinLayerSlot
    {
        public string name;
        public ClothLayer id;
        public Texture2D texture;
        public Color tint;
    }

    // A region's designs sit in a row or column of cells from its design-0 cell; stride is the UV step
    // from one design to the next.
    [Serializable]
    public struct TattooRegion
    {
        public bool on;
        public int design;
        public Vector2 stride;
    }

    public class SkinLayers : MonoBehaviour
    {
        private const int MaxClothSlots = 6;

        private static readonly int ChestStepId = Shader.PropertyToID("_ChestStep");
        private static readonly int TatTintId = Shader.PropertyToID("_TatTint");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private static readonly int[] MakeupIds =
        {
            Shader.PropertyToID("_MakeupTintR"), Shader.PropertyToID("_MakeupTintG"),
            Shader.PropertyToID("_MakeupTintB"), Shader.PropertyToID("_MakeupTintA")
        };

        private static readonly int[] RegionIds =
        {
            Shader.PropertyToID("_TatWomb"), Shader.PropertyToID("_TatBack"),
            Shader.PropertyToID("_TatArm"), Shader.PropertyToID("_TatSpare")
        };

        private static readonly int[] ClothTexIds =
        {
            Shader.PropertyToID("_Cloth0Tex"), Shader.PropertyToID("_Cloth1Tex"), Shader.PropertyToID("_Cloth2Tex"),
            Shader.PropertyToID("_Cloth3Tex"), Shader.PropertyToID("_Cloth4Tex"), Shader.PropertyToID("_Cloth5Tex")
        };

        private static readonly int[] ClothTintIds =
        {
            Shader.PropertyToID("_Cloth0Tint"), Shader.PropertyToID("_Cloth1Tint"), Shader.PropertyToID("_Cloth2Tint"),
            Shader.PropertyToID("_Cloth3Tint"), Shader.PropertyToID("_Cloth4Tint"), Shader.PropertyToID("_Cloth5Tint")
        };

        [SerializeField] private Renderer target;
        [SerializeField] private int materialSlot;

        [Header("Body")]
        [Range(0, 4)] public int chestStep;

        [Header("Makeup (alpha = strength)")]
        public Color[] makeupTints = { Color.clear, Color.clear, Color.clear, Color.clear };

        [Header("Tattoos: womb, back, arm, spare")]
        public TattooRegion[] tattooRegions = new TattooRegion[4];
        public Color tattooTint = Color.white;

        [Header("Cloth layers")]
        public SkinLayerSlot[] clothSlots = new SkinLayerSlot[0];

        [Header("Stealth")]
        [Range(0f, 1f)] public float fade = 1f;

        private Material material;

        private void Awake()
        {
            Material[] materials = target.materials;
            material = materials[materialSlot];

            if (!material.HasProperty(ClothTexIds[MaxClothSlots - 1]))
                Debug.LogError($"{name}: material in slot {materialSlot} has no skin layer slots");

            Apply();
        }

        public void Apply()
        {
            material.SetFloat(ChestStepId, chestStep);
            material.SetFloat(FadeId, fade);
            material.SetColor(TatTintId, tattooTint);

            for (int i = 0; i < MakeupIds.Length; i++)
                material.SetColor(MakeupIds[i], i < makeupTints.Length ? makeupTints[i] : Color.clear);

            for (int i = 0; i < RegionIds.Length; i++)
                material.SetVector(RegionIds[i], RegionVector(i));

            for (int i = 0; i < MaxClothSlots; i++)
                material.SetColor(ClothTintIds[i], Color.clear);

            foreach (SkinLayerSlot slot in clothSlots)
                ApplySlot(slot);
        }

        public void SetFade(float value)
        {
            fade = Mathf.Clamp01(value);
            material.SetFloat(FadeId, fade);
        }

        private Vector4 RegionVector(int index)
        {
            if (index >= tattooRegions.Length) return Vector4.zero;

            TattooRegion region = tattooRegions[index];
            Vector2 offset = region.stride * region.design;
            return new Vector4(offset.x, offset.y, 0f, region.on ? 1f : 0f);
        }

        private void ApplySlot(SkinLayerSlot slot)
        {
            int i = (int)slot.id;
            material.SetTexture(ClothTexIds[i], slot.texture);
            material.SetColor(ClothTintIds[i], slot.tint);
        }

        private void OnValidate()
        {
            if (clothSlots.Length > MaxClothSlots)
                Debug.LogError($"{name}: {clothSlots.Length} cloth slots, the shader has {MaxClothSlots}");

            int seen = 0;
            foreach (SkinLayerSlot slot in clothSlots)
            {
                int bit = 1 << (int)slot.id;
                if ((seen & bit) != 0) Debug.LogError($"{name}: cloth layer {slot.id} is used twice");
                seen |= bit;
            }

            if (material != null) Apply();
        }
    }
}
