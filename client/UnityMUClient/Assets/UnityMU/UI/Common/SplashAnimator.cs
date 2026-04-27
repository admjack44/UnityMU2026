using System.Collections;
using UnityEngine;

namespace UnityMU.UI.Common
{
    public sealed class SplashAnimator : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform logo;
        [SerializeField] private float fadeInDuration = 0.45f;
        [SerializeField] private float holdDuration = 1.05f;
        [SerializeField] private float fadeOutDuration = 0.45f;
        [SerializeField] private float startScale = 0.92f;
        [SerializeField] private float endScale = 1.0f;

        public float TotalDuration => fadeInDuration + holdDuration + fadeOutDuration;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Play()
        {
            StopAllCoroutines();
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (canvasGroup == null)
                yield break;

            canvasGroup.alpha = 0f;

            if (logo != null)
                logo.localScale = Vector3.one * startScale;

            float timer = 0f;

            while (timer < fadeInDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / fadeInDuration);
                float eased = EaseOutCubic(t);

                canvasGroup.alpha = eased;

                if (logo != null)
                    logo.localScale = Vector3.Lerp(Vector3.one * startScale, Vector3.one * endScale, eased);

                yield return null;
            }

            canvasGroup.alpha = 1f;

            if (logo != null)
                logo.localScale = Vector3.one * endScale;

            yield return new WaitForSecondsRealtime(holdDuration);

            timer = 0f;

            while (timer < fadeOutDuration)
            {
                timer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(timer / fadeOutDuration);

                canvasGroup.alpha = 1f - t;

                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - Mathf.Pow(1f - t, 3f);
        }
    }
}