using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMU.UI.Common
{
    public sealed class LoadingScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private CanvasGroup canvasGroup;

        public void SetMessage(string message)
        {
            if (loadingText != null)
                loadingText.text = message;
        }

        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);

            if (progressSlider != null)
                progressSlider.value = value;
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}