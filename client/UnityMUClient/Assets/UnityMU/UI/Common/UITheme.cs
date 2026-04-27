using UnityEngine;

namespace UnityMU.UI.Common
{
    [CreateAssetMenu(
        fileName = "UITheme",
        menuName = "UnityMU/UI/UI Theme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Colors")]
        public Color primary = new Color(0.9f, 0.24f, 0.18f, 1f);
        public Color secondary = new Color(0.12f, 0.12f, 0.14f, 1f);
        public Color glass = new Color(1f, 1f, 1f, 0.72f);
        public Color textDark = new Color(0.16f, 0.16f, 0.16f, 1f);
        public Color textLight = Color.white;
        public Color dim = new Color(0f, 0f, 0f, 0.28f);

        [Header("Typography")]
        public int titleSize = 64;
        public int subtitleSize = 24;
        public int inputSize = 24;
        public int buttonSize = 26;
        public int statusSize = 20;
    }
}