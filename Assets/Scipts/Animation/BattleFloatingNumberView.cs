using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Battle
{
    public class BattleFloatingNumberView : MonoBehaviour
    {
        [SerializeField] private RectTransform numberLayer;
        [SerializeField] private TextMeshProUGUI fontSource;
        [SerializeField] private Color damageColor = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private Color healingColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color poisonColor = new Color(0.72f, 0.35f, 1f);
        [SerializeField, Min(0f)] private float duration = 0.65f;
        [SerializeField] private float riseDistance = 80f;
        [SerializeField] private float worldHeight = 0.9f;
        [SerializeField] private float fontSize = 52f;

        private readonly List<GameObject> activeNumbers = new List<GameObject>();
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
        }

        public Coroutine Show(Transform target, float healthChange, BattleNumberStyle style)
        {
            if (!isActiveAndEnabled || target == null || Mathf.Approximately(healthChange, 0f))
                return null;
            return StartCoroutine(AnimateNumber(target.position + Vector3.up * worldHeight,
                healthChange, style));
        }

        private IEnumerator AnimateNumber(Vector3 worldPosition, float healthChange,
            BattleNumberStyle style)
        {
            if (numberLayer == null || canvas == null)
                yield break;

            GameObject numberObject = new GameObject("FloatingNumber",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            activeNumbers.Add(numberObject);
            RectTransform rect = numberObject.GetComponent<RectTransform>();
            rect.SetParent(numberLayer, false);
            rect.sizeDelta = new Vector2(320f, 100f);

            TextMeshProUGUI text = numberObject.GetComponent<TextMeshProUGUI>();
            if (fontSource != null)
            {
                text.font = fontSource.font;
                text.fontSharedMaterial = fontSource.fontSharedMaterial;
            }
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = style == BattleNumberStyle.Healing ? "+" + FormatAmount(healthChange)
                : FormatAmount(healthChange);
            Color color = GetColor(style);
            text.color = color;

            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldPosition);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(numberLayer,
                screenPoint, eventCamera, out Vector2 start))
            {
                activeNumbers.Remove(numberObject);
                Destroy(numberObject);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (Time.timeScale == 0f) { yield return null; continue; }
                elapsed += Time.deltaTime;
                float progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = start + Vector2.up * riseDistance * progress;
                color.a = 1f - progress;
                text.color = color;
                yield return null;
            }

            activeNumbers.Remove(numberObject);
            Destroy(numberObject);
        }

        private string FormatAmount(float amount)
        {
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Abs(amount))).ToString();
        }

        private Color GetColor(BattleNumberStyle style)
        {
            if (style == BattleNumberStyle.Healing)
                return healingColor;
            if (style == BattleNumberStyle.Poison)
                return poisonColor;
            return damageColor;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            foreach (GameObject number in activeNumbers)
                if (number != null)
                    Destroy(number);
            activeNumbers.Clear();
        }
    }
}
