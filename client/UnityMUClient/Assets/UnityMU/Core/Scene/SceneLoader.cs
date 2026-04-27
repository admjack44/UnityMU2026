using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMU.Core.App;

namespace UnityMU.Core.Scene
{
    public sealed class SceneLoader
    {
        private readonly AppConfig config;

        public SceneLoader(AppConfig config)
        {
            this.config = config;
        }

        public Task LoadSplashAsync()
        {
            return LoadSceneDirectAsync(config.splashScene);
        }

        public Task LoadLoginAsync()
        {
            return LoadWithLoadingAsync(config.loginScene, "Preparando acceso...");
        }

        public Task LoadServerSelectAsync()
        {
            return LoadWithLoadingAsync(config.serverSelectScene, "Buscando servidores...");
        }

        public Task LoadCharacterSelectAsync()
        {
            return LoadWithLoadingAsync(config.characterSelectScene, "Cargando personajes...");
        }

        public Task LoadWorldAsync()
        {
            return LoadWithLoadingAsync(config.worldScene, "Entrando al continente...");
        }

        private async Task LoadWithLoadingAsync(string targetScene, string message)
        {
            LoadingSceneController.SetTarget(targetScene, message);

            AsyncOperation operation = SceneManager.LoadSceneAsync(config.loadingScene);
            operation.allowSceneActivation = true;

            while (!operation.isDone)
                await Task.Yield();
        }

        private static async Task LoadSceneDirectAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
            operation.allowSceneActivation = true;

            while (!operation.isDone)
                await Task.Yield();
        }
    }
}