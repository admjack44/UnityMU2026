using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMU.UI.Login
{
    public sealed class LoginView : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private TMP_InputField passwordInput;

        [Header("Buttons")]
        [SerializeField] private Button loginButton;
        [SerializeField] private Button guestButton;
        [SerializeField] private Button googleButton;
        [SerializeField] private Button facebookButton;
        [SerializeField] private Button createAccountButton;
        [SerializeField] private Button forgotPasswordButton;

        [Header("Texts")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text loadingText;

        [Header("Panels")]
        [SerializeField] private GameObject loadingOverlay;

        public string Username => usernameInput != null ? usernameInput.text.Trim() : string.Empty;
        public string Password => passwordInput != null ? passwordInput.text.Trim() : string.Empty;

        public Button LoginButton => loginButton;
        public Button GuestButton => guestButton;
        public Button GoogleButton => googleButton;
        public Button FacebookButton => facebookButton;
        public Button CreateAccountButton => createAccountButton;
        public Button ForgotPasswordButton => forgotPasswordButton;

        private void Awake()
        {
            SetLoading(false);
            SetStatus("Selecciona tu método de acceso");
        }

        public void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        public void SetLoading(bool value, string message = "Cargando...")
        {
            if (loadingOverlay != null)
                loadingOverlay.SetActive(value);

            if (loadingText != null)
                loadingText.text = message;

            SetInteractable(!value);
        }

        public void SetInteractable(bool value)
        {
            if (usernameInput != null) usernameInput.interactable = value;
            if (passwordInput != null) passwordInput.interactable = value;

            if (loginButton != null) loginButton.interactable = value;
            if (guestButton != null) guestButton.interactable = value;
            if (googleButton != null) googleButton.interactable = value;
            if (facebookButton != null) facebookButton.interactable = value;
            if (createAccountButton != null) createAccountButton.interactable = value;
            if (forgotPasswordButton != null) forgotPasswordButton.interactable = value;
        }
    }
}