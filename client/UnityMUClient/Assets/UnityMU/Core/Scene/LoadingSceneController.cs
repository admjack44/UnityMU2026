using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMU.UI.Common;

namespace UnityMU.Core.Scene
{
    public sealed class LoadingSceneController : MonoBehaviour
    {
        [SerializeField] private LoadingScreenView view;
        [SerializeField] private float minimumLoadingTime = 0.75f;

        private static string targetScene;
        private static string loadingMessage;

        public static void SetTarget(string sceneName, string message)
        {
            targetScene = sceneName;
            loadingMessage = message;
        }

        private void Start()
        {
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            if (string.IsNullOrWhiteSpace(targetScene))
            {
                Debug.LogError("[LoadingSceneController] No hay escena destino.");
                yield break;
            }

            if (view != null)
            {
                view.SetVisible(true);
                view.SetProgress(0f);
                view.SetMessage(string.IsNullOrWhiteSpace(loadingMessage) ? "Cargando..." : loadingMessage);
            }

            float elapsed = 0f;

            AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                elapsed += Time.unscaledDeltaTime;

                float sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
                float timeProgress = Mathf.Clamp01(elapsed / minimumLoadingTime);
                float finalProgress = Mathf.Min(sceneProgress, timeProgress);

                if (view != null)
                    view.SetProgress(finalProgress);

                yield return null;
            }

            while (elapsed < minimumLoadingTime)
            {
                elapsed += Time.unscaledDeltaTime;

                if (view != null)
                    view.SetProgress(Mathf.Clamp01(elapsed / minimumLoadingTime));

                yield return null;
            }

            if (view != null)
            {
                view.SetProgress(1f);
                view.SetMessage("Entrando al continente...");
            }

            yield return new WaitForSecondsRealtime(0.25f);

            operation.allowSceneActivation = true;
        }
    }
}