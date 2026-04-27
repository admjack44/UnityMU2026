using System.Collections;
using UnityEngine;
using UnityMU.UI.Common;

namespace UnityMU.Core.App
{
    public sealed class SplashController : MonoBehaviour
    {
        [SerializeField] private SplashAnimator animator;

        private IEnumerator Start()
        {
            if (animator != null)
            {
                animator.Play();
                yield return new WaitForSecondsRealtime(animator.TotalDuration);
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.5f);
            }

            if (UnityMUApp.Instance == null)
            {
                Debug.LogError("[SplashController] UnityMUApp no existe. Inicia desde Boot.unity.");
                yield break;
            }

            var task = UnityMUApp.Instance.SceneLoader.LoadLoginAsync();

            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted)
                Debug.LogException(task.Exception);
        }
    }
}