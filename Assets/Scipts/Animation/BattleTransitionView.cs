using System.Collections;
using TMPro;
using UnityEngine;

namespace Game.Battle
{
    public class BattleTransitionView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private GameObject selectedCharacterPanel;
        [SerializeField] private Vector2 dockAnchor = new Vector2(0.5f, 1f);
        [SerializeField] private Vector2 dockPosition = new Vector2(0f, -150f);
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.6f;
        [SerializeField, Min(0f)] private float moveSeconds = 0.8f;

        private void Awake() { Hide(); }

        public IEnumerator PlayStart()
        {
            if (messageText == null) yield break;
            ShowCentered("战斗开始");
            yield return Fade(0f, 1f);
            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Fade(1f, 0f);
            Hide();
        }

        public IEnumerator PlayEnd(string title)
        {
            if (messageText == null) yield break;
            ShowCentered(title);
            yield return Fade(0f, 1f);
        }

        public IEnumerator MoveToDock()
        {
            if (selectedCharacterPanel != null)
                selectedCharacterPanel.SetActive(false);
            if (messageText == null) yield break;

            RectTransform rect = messageText.rectTransform;
            RectTransform parent = (RectTransform)rect.parent;
            Vector2 start = rect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < moveSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                // The destination is authored independently of the character panel.
                Vector2 destination = Vector2.Scale(parent.rect.size, dockAnchor - new Vector2(0.5f, 0.5f)) + dockPosition;
                rect.anchoredPosition = Vector2.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, elapsed / moveSeconds));
                yield return null;
            }
            rect.anchorMin = rect.anchorMax = dockAnchor;
            rect.anchoredPosition = dockPosition;
        }

        private void ShowCentered(string title)
        {
            RectTransform rect = messageText.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            messageText.text = title;
            messageText.alpha = 0f;
            messageText.raycastTarget = false;
            messageText.gameObject.SetActive(true);
            rect.SetAsLastSibling();
        }

        private IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            messageText.alpha = from;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                messageText.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }
            messageText.alpha = to;
        }

        public void Hide()
        {
            if (messageText != null)
            {
                messageText.alpha = 0f;
                messageText.gameObject.SetActive(false);
            }
        }

        public void BringToFront()
        {
            if (messageText != null)
                messageText.rectTransform.SetAsLastSibling();
        }

        public void ShowDocked(string title)
        {
            if (messageText == null) return;
            ShowCentered(title);
            messageText.rectTransform.anchorMin = messageText.rectTransform.anchorMax = dockAnchor;
            messageText.rectTransform.anchoredPosition = dockPosition;
            messageText.alpha = 1f;
        }
    }
}
