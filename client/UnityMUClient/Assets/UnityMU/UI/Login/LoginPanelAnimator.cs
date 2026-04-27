using System.Collections;
using UnityEngine;

namespace UnityMU.UI.Login
{
    public sealed class LoginPanelAnimator : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform target;
        [SerializeField] private float duration = 0.35f;
        [SerializeField] private Vector2 startOffset = new Vector2(0f, -40f);

        private Vector2 originalPosition;

        private void Awake()
        {
            if (target == null)
                target = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            originalPosition = target.anchoredPosition;
        }

        private void OnEnable()
        {
            PlayIntro();
        }

        public void PlayIntro()
        {
            StopAllCoroutines();
            StartCoroutine(AnimateIntro());
        }

        private IEnumerator AnimateIntro()
        {
            float time = 0f;

            canvasGroup.alpha = 0f;
            target.anchoredPosition = originalPosition + startOffset;
            target.localScale = Vector3.one * 0.96f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(time / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                canvasGroup.alpha = eased;
                target.anchoredPosition = Vector2.Lerp(originalPosition + startOffset, originalPosition, eased);
                target.localScale = Vector3.Lerp(Vector3.one * 0.96f, Vector3.one, eased);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            target.anchoredPosition = originalPosition;
            target.localScale = Vector3.one;
        }
    }
}