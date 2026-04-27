using UnityEngine;
using UnityMU.Core.App;
using UnityMU.Network.Packets;
using UnityMU.Network.Services;

namespace UnityMU.UI.Login
{
    public sealed class LoginController : MonoBehaviour
    {
        [SerializeField] private LoginView view;

        private bool isBound;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<LoginView>();

            if (view == null)
            {
                Debug.LogError("[LoginController] LoginView no asignado.");
                return;
            }

            BindButtons();

            if (UnityMUApp.Instance == null)
            {
                view.SetStatus("Modo editor: inicia desde Boot para conectar.");
                return;
            }

            UnityMUApp.Instance.GameServer.OnLoginReceived += HandleLoginResponse;
            UnityMUApp.Instance.GameServer.OnError += HandleError;
        }

        private void OnDisable()
        {
            UnbindButtons();

            if (UnityMUApp.Instance == null)
                return;

            UnityMUApp.Instance.GameServer.OnLoginReceived -= HandleLoginResponse;
            UnityMUApp.Instance.GameServer.OnError -= HandleError;
        }

        private void BindButtons()
        {
            if (isBound)
                return;

            if (view.LoginButton != null)
                view.LoginButton.onClick.AddListener(OnAccountLoginClicked);

            if (view.GuestButton != null)
                view.GuestButton.onClick.AddListener(OnGuestLoginClicked);

            if (view.GoogleButton != null)
                view.GoogleButton.onClick.AddListener(OnGoogleLoginClicked);

            if (view.FacebookButton != null)
                view.FacebookButton.onClick.AddListener(OnFacebookLoginClicked);

            if (view.CreateAccountButton != null)
                view.CreateAccountButton.onClick.AddListener(OnCreateAccountClicked);

            if (view.ForgotPasswordButton != null)
                view.ForgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked);

            isBound = true;
        }

        private void UnbindButtons()
        {
            if (!isBound || view == null)
                return;

            if (view.LoginButton != null)
                view.LoginButton.onClick.RemoveListener(OnAccountLoginClicked);

            if (view.GuestButton != null)
                view.GuestButton.onClick.RemoveListener(OnGuestLoginClicked);

            if (view.GoogleButton != null)
                view.GoogleButton.onClick.RemoveListener(OnGoogleLoginClicked);

            if (view.FacebookButton != null)
                view.FacebookButton.onClick.RemoveListener(OnFacebookLoginClicked);

            if (view.CreateAccountButton != null)
                view.CreateAccountButton.onClick.RemoveListener(OnCreateAccountClicked);

            if (view.ForgotPasswordButton != null)
                view.ForgotPasswordButton.onClick.RemoveListener(OnForgotPasswordClicked);

            isBound = false;
        }

        private async void OnAccountLoginClicked()
        {
            if (UnityMUApp.Instance == null)
            {
                view.SetStatus("Inicia desde Boot.unity para conectar.");
                return;
            }

            if (string.IsNullOrWhiteSpace(view.Username))
            {
                view.SetStatus("Ingresa tu usuario o correo.");
                return;
            }

            if (string.IsNullOrWhiteSpace(view.Password))
            {
                view.SetStatus("Ingresa tu contraseña.");
                return;
            }

            view.SetLoading(true, "Iniciando sesión...");

            await UnityMUApp.Instance.GameServer.LoginAsync(view.Username, view.Password);
        }

        private async void OnGuestLoginClicked()
        {
            if (UnityMUApp.Instance == null)
            {
                view.SetStatus("Inicia desde Boot.unity para conectar.");
                return;
            }

            view.SetLoading(true, "Entrando como invitado...");
            await UnityMUApp.Instance.GameServer.LoginAsync("guest", "guest");
        }

        private void OnGoogleLoginClicked()
        {
            view.SetStatus("Google Login pendiente de integrar.");
        }

        private void OnFacebookLoginClicked()
        {
            view.SetStatus("Facebook Login pendiente de integrar.");
        }

        private void OnCreateAccountClicked()
        {
            view.SetStatus("Registro de cuenta pendiente.");
        }

        private void OnForgotPasswordClicked()
        {
            view.SetStatus("Recuperación de contraseña pendiente.");
        }

        private async void HandleLoginResponse(LoginResponse response)
        {
            if (!response.success)
            {
                view.SetLoading(false);
                view.SetStatus(response.message);
                return;
            }

            SessionState.Current.SetLogin(response);

            view.SetStatus("Login correcto.");
            view.SetLoading(true, "Cargando servidores...");

            await UnityMUApp.Instance.SceneLoader.LoadServerSelectAsync();
        }

        private void HandleError(string error)
        {
            view.SetLoading(false);
            view.SetStatus($"Error: {error}");
        }
    }
}