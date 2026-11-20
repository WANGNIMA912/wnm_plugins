using UnityEngine;

namespace KKS.ModernGraphicEffect
{
    /// <summary>游戏内短提示：英文 / 简体中文 / 繁体中文，按系统语言选择。</summary>
    internal static class Toast
    {
        private static string _text;
        private static float _until;
        private static GUIStyle _style;

        public static void Show(string english, string simplified, string traditional)
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.ChineseSimplified:
                    _text = simplified;
                    break;
                case SystemLanguage.ChineseTraditional:
                    _text = traditional;
                    break;
                default:
                    _text = english;
                    break;
            }
            _until = Time.realtimeSinceStartup + 2.5f;
        }

        public static void Draw()
        {
            if (string.IsNullOrEmpty(_text) || Time.realtimeSinceStartup > _until) return;
            try
            {
                if (_style == null)
                {
                    _style = new GUIStyle(GUI.skin.label);
                    _style.fontSize = 22;
                    _style.alignment = TextAnchor.MiddleCenter;
                    _style.normal.textColor = Color.white;
                }
                GUI.Label(new Rect(0f, Screen.height * 0.12f, Screen.width, 44f), _text, _style);
            }
            catch (System.Exception)
            {
                // OnGUI 里不抛异常。
            }
        }
    }
}
