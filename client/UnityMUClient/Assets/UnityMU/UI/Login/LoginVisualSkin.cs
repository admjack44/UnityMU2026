using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMU.UI.Login
{
    public sealed class LoginVisualSkin : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image overlayDim;
        [SerializeField] private Image loginCard;

        [Header("Texts")]
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text statusText;

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

        private readonly Color gold = new Color32(218, 175, 55, 255);
        private readonly Color red = new Color32(139, 28, 28, 255);
        private readonly Color dark = new Color32(18, 18, 18, 230);
        private readonly Color inputDark = new Color32(15, 15, 15, 210);

        private void Awake()
        {
            Apply();
        }

        private void OnValidate()
        {
            Apply();
        }

        public void Apply()
        {
            if (backgroundImage != null)
                backgroundImage.color = Color.white;

            if (overlayDim != null)
                overlayDim.color = new Color(0f, 0f, 0f, 0.42f);

            if (loginCard != null)
                loginCard.color = dark;

            ApplyText(subtitleText, "BIENVENIDO AL CONTINENTE DE MU", 18, gold);
            ApplyText(statusText, "Selecciona tu método de acceso", 16, new Color(0.85f, 0.85f, 0.85f, 1f));

            ApplyInput(usernameInput, "Usuario o correo");
            ApplyInput(passwordInput, "Contraseña");

            ApplyButton(loginButton, red, Color.white, 24);
            ApplyButton(guestButton, new Color32(30, 30, 30, 240), gold, 20);
            ApplyButton(googleButton, Color.white, new Color32(40, 40, 40, 255), 18);
            ApplyButton(facebookButton, new Color32(45, 90, 160, 255), Color.white, 18);
            ApplyButton(createAccountButton, new Color32(25, 25, 25, 220), Color.white, 15);
            ApplyButton(forgotPasswordButton, new Color32(25, 25, 25, 220), Color.white, 15);
        }

        private void ApplyText(TMP_Text text, string value, int size, Color color)
        {
            if (text == null) return;

            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
        }

        private void ApplyInput(TMP_InputField input, string placeholder)
        {
            if (input == null) return;

            Image image = input.GetComponent<Image>();
            if (image != null)
                image.color = inputDark;

            if (input.textComponent != null)
            {
                input.textComponent.fontSize = 18;
                input.textComponent.color = Color.white;
            }

            if (input.placeholder is TMP_Text placeholderText)
            {
                placeholderText.text = placeholder;
                placeholderText.fontSize = 18;
                placeholderText.color = new Color(1f, 1f, 1f, 0.55f);
            }
        }

        private void ApplyButton(Button button, Color bg, Color textColor, int size)
        {
            if (button == null) return;

            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = bg;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.fontSize = size;
                text.color = textColor;
                text.alignment = TextAlignmentOptions.Center;
            }
        }
    }
}