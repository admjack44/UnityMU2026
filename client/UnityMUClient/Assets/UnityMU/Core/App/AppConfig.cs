using UnityEngine;

namespace UnityMU.Core.App
{
    [CreateAssetMenu(fileName = "AppConfig", menuName = "UnityMU/Config/App Config")]
    public sealed class AppConfig : ScriptableObject
    {
        [Header("Network")]
        public string connectServerHost = "127.0.0.1";
        public int connectServerPort = 44405;

        public string gameServerHost = "127.0.0.1";
        public int gameServerPort = 55901;

        [Header("Protocol")]
        public byte mobileProtocolId = 0xA1;

        [Header("Scenes")]
        public string bootScene = "Boot";
        public string splashScene = "Splash";
        public string loadingScene = "Loading";
        public string loginScene = "Login";
        public string serverSelectScene = "ServerSelect";
        public string characterSelectScene = "CharacterSelect";
        public string worldScene = "World";

        [Header("Debug")]
        public bool logNetworkPackets = true;
    }
}