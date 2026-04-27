using UnityEngine;
using UnityMU.Network.Services;
using UnityMU.Core.Scene;

namespace UnityMU.Core.App
{
    public sealed class UnityMUApp : MonoBehaviour
    {
        public static UnityMUApp Instance { get; private set; }

        [SerializeField] private AppConfig config;

        public AppConfig Config => config;

        public ConnectServerClient ConnectServer { get; private set; }
        public GameServerClient GameServer { get; private set; }
        public SceneLoader SceneLoader { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (config == null)
            {
                Debug.LogError("[UnityMUApp] AppConfig no asignado.");
                return;
            }

            SceneLoader = new SceneLoader(config);
            ConnectServer = new ConnectServerClient(config);
            GameServer = new GameServerClient(config);

            Debug.Log("[UnityMUApp] Inicializado.");
        }

        private async void OnApplicationQuit()
        {
            if (ConnectServer != null)
                await ConnectServer.DisconnectAsync();

            if (GameServer != null)
                await GameServer.DisconnectAsync();
        }
    }
}