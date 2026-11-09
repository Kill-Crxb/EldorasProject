using UnityEngine;

// STAGING: drives Crab/Face Atlas (Staging) on the face plane under the Head bone. Gives the renderer
// its own material instance at Awake (same reason as SkinLayers: a property block drops it out of
// the SRP Batcher), then writes the expression cell, iris cell, iris tint and look direction.
//
// Atlas layout, grid size, iris travel and shading stay on the material; this holds what changes
// per character or per moment. Nothing drives it yet: expressions and look are set from the
// inspector or by calling SetExpression / SetLook.

namespace CrabSystem
{
    public class FaceAtlas : MonoBehaviour
    {
        private static readonly int ExpressionCellId = Shader.PropertyToID("_ExpressionCell");
        private static readonly int IrisCellId = Shader.PropertyToID("_IrisCell");
        private static readonly int IrisTintId = Shader.PropertyToID("_IrisTint");
        private static readonly int IrisLookId = Shader.PropertyToID("_IrisLook");

        [SerializeField] private Renderer target;

        [Min(0)] public int expressionCell;
        [Min(0)] public int irisCell;
        public Color irisTint = Color.white;
        public Vector2 look;

        private Material material;

        private void Awake()
        {
            if (target == null) target = GetComponent<Renderer>();
            material = target.material;

            if (!material.HasProperty(ExpressionCellId))
                Debug.LogError($"{name}: face material has no atlas cells", this);

            Apply();
        }

        public void Apply()
        {
            material.SetFloat(ExpressionCellId, expressionCell);
            material.SetFloat(IrisCellId, irisCell);
            material.SetColor(IrisTintId, irisTint);
            material.SetVector(IrisLookId, look);
        }

        public void SetExpression(int cell)
        {
            expressionCell = cell;
            material.SetFloat(ExpressionCellId, cell);
        }

        // -1..1 per axis; the material's Iris Travel turns it into UV distance.
        public void SetLook(Vector2 direction)
        {
            look = Vector2.ClampMagnitude(direction, 1f);
            material.SetVector(IrisLookId, look);
        }

        private void OnValidate()
        {
            look = Vector2.ClampMagnitude(look, 1f);
            if (material != null) Apply();
        }
    }
}
