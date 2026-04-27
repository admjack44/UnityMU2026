using UnityMU.Network.Packets;

namespace UnityMU.Network.Services
{
    public sealed class SessionState
    {
        public static SessionState Current { get; } = new SessionState();

        public string SessionToken { get; private set; }
        public int AccountId { get; private set; }
        public string AccountName { get; private set; }

        public ServerInfoDto SelectedServer { get; private set; }
        public CharacterDto SelectedCharacter { get; private set; }

        private SessionState() { }

        public void SetLogin(LoginResponse response)
        {
            SessionToken = response.sessionToken;
            AccountId = response.accountId;
            AccountName = response.accountName;
        }

        public void SetSelectedServer(ServerInfoDto server)
        {
            SelectedServer = server;
        }

        public void SetSelectedCharacter(CharacterDto character)
        {
            SelectedCharacter = character;
        }

        public void Clear()
        {
            SessionToken = null;
            AccountId = 0;
            AccountName = null;
            SelectedServer = null;
            SelectedCharacter = null;
        }
    }
}