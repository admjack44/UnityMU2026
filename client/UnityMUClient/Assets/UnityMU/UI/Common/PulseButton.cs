using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityMU.UI.Common
{
    public sealed class PulseButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private float speed = 12f;

        private Vector3 desiredScale = Vector3.one;

        private void Awake()
        {
            if (target == null)
                target = GetComponent<RectTransform>();
        }

        private void Update()
        {
            target.localScale = Vector3.Lerp(
                target.localScale,
                desiredScale,
                Time.unscaledDeltaTime * speed);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            desiredScale = Vector3.one * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            desiredScale = Vector3.one;
        }
    }
}