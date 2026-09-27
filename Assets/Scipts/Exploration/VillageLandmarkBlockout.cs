using UnityEngine;

namespace Game.Exploration
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class VillageLandmarkBlockout : MonoBehaviour
    {
        [SerializeField] private Sprite landmarkTreeSprite;
        [SerializeField] private Vector2 treePosition = new Vector2(2f, 2f);
        [SerializeField, Min(1f)] private float treeHeight = 15f;
        [SerializeField] private Vector2 trunkColliderSize = new Vector2(8.24f, 3.91f);
        [SerializeField] private Vector2 trunkColliderOffset = new Vector2(0f, 1.61f);
        [SerializeField] private float treeSortingOffsetY;

        private GameObject generated;
        private Sprite placeholderSprite;

        private void OnEnable() => Rebuild();
        private void OnDisable() => ClearGenerated();

        [ContextMenu("Rebuild Village Landmarks")]
        public void Rebuild()
        {
            ClearGenerated();
            generated = new GameObject("White landmark tree elements") { hideFlags = HideFlags.DontSave };
            generated.transform.SetParent(transform, false);
            placeholderSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            placeholderSprite.hideFlags = HideFlags.DontSave;

            AddLandmarkTreeSprite(treePosition);
        }

        private void AddLandmarkTreeSprite(Vector2 origin)
        {
            if (landmarkTreeSprite == null)
            {
                AddPrimitive("White tree placeholder", PrimitiveType.Cube,
                    new Vector3(origin.x, 0f, origin.y), new Vector3(6f, 1f, 8f),
                    new Color(0.92f, 0.92f, 0.86f), false);
                return;
            }

            GameObject trunk = new GameObject("White tree trunk collision") { hideFlags = HideFlags.DontSave };
            trunk.transform.SetParent(generated.transform, false);
            trunk.transform.localPosition = origin;
            CapsuleCollider2D collision = trunk.AddComponent<CapsuleCollider2D>();
            collision.direction = CapsuleDirection2D.Horizontal;
            collision.size = new Vector2(Mathf.Max(0.1f, trunkColliderSize.x),
                Mathf.Max(0.1f, trunkColliderSize.y));
            collision.offset = trunkColliderOffset;

            GameObject tree = new GameObject("White landmark tree") { hideFlags = HideFlags.DontSave };
            tree.transform.SetParent(generated.transform, false);
            tree.transform.localPosition = new Vector3(origin.x, origin.y + treeHeight * 0.5f - 0.6f, 0f);
            tree.transform.localScale = Vector3.one * (treeHeight / landmarkTreeSprite.bounds.size.y);
            SpriteRenderer renderer = tree.AddComponent<SpriteRenderer>();
            renderer.sprite = landmarkTreeSprite;
            renderer.sortingOrder = -Mathf.RoundToInt((origin.y + treeSortingOffsetY) * 100f);
        }

        private void AddPrimitive(string label, PrimitiveType type, Vector3 position,
            Vector3 scale, Color color, bool solid)
        {
            GameObject instance = new GameObject(label) { hideFlags = HideFlags.DontSave };
            instance.transform.SetParent(generated.transform, false);
            instance.transform.localPosition = new Vector3(position.x, position.z, 0f);
            instance.transform.localScale = new Vector3(scale.x, scale.z, 1f);

            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = placeholderSprite;
            renderer.color = color;
            renderer.sortingOrder = -Mathf.RoundToInt((position.z - scale.z * 0.5f) * 100f);

            if (solid)
                instance.AddComponent<BoxCollider2D>();
        }

        private void ClearGenerated()
        {
            if (generated != null)
            {
                if (Application.isPlaying) Destroy(generated);
                else DestroyImmediate(generated);
                generated = null;
            }
            if (placeholderSprite != null)
            {
                if (Application.isPlaying) Destroy(placeholderSprite);
                else DestroyImmediate(placeholderSprite);
                placeholderSprite = null;
            }
        }
    }
}
