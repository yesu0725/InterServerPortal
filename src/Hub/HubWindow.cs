using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace InterServerPortal.Hub
{
    /// <summary>
    /// A single native (Jötunn) modal window used by the hub UI — the travel
    /// selection menu and the portal config/editor panel are both built on top
    /// of this. Handles the woodpanel + title, input-blocking (frees the cursor),
    /// Escape-to-close, and teardown. Only one is open at a time.
    /// See docs/Feature-Hub-Routing.md.
    /// </summary>
    internal class HubWindow : MonoBehaviour
    {
        private static HubWindow _current;

        internal static bool IsOpen => _current != null;

        internal GameObject Panel { get; private set; }

        private float _width;
        private float _height;

        /// <summary>Open a fresh window (closes any existing one). Null if UI isn't available.</summary>
        internal static HubWindow Open(string title, float width, float height)
        {
            if (GUIManager.IsHeadless() || GUIManager.Instance == null ||
                GUIManager.CustomGUIFront == null)
            {
                Plugin.Log.LogWarning("HubWindow.Open: GUI not available (headless or too early).");
                return null;
            }

            Close();

            var go = new GameObject("ISP_HubWindow");
            var win = go.AddComponent<HubWindow>();
            win._width = width;
            win._height = height;
            win.Build(title);
            _current = win;
            GUIManager.BlockInput(true);
            return win;
        }

        internal static void Close()
        {
            if (_current == null) return;
            GUIManager.BlockInput(false);
            if (_current.Panel != null) Destroy(_current.Panel);
            Destroy(_current.gameObject);
            _current = null;
        }

        private void Build(string title)
        {
            Panel = GUIManager.Instance.CreateWoodpanel(
                GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f),
                _width, _height, draggable: true);
            Panel.SetActive(true);

            AddLabel(title, 0f, _height / 2f - 34f, _width - 40f, 40f, 24, GUIManager.Instance.ValheimOrange);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        // ---- Content helpers (positions are offsets from the panel centre) ----

        internal Text AddLabel(string text, float x, float y, float width, float height,
                               int fontSize, Color color)
        {
            var go = GUIManager.Instance.CreateText(
                text, Panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y),
                GUIManager.Instance.AveriaSerifBold, fontSize, color,
                outline: true, outlineColor: Color.black,
                width, height, addContentSizeFitter: false);
            return go.GetComponent<Text>();
        }

        internal Button AddButton(string text, float x, float y, float width, float height,
                                  Action onClick, bool interactable = true)
        {
            var go = GUIManager.Instance.CreateButton(
                text, Panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y),
                width, height);
            go.SetActive(true);
            var button = go.GetComponent<Button>();
            button.interactable = interactable;
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
            return button;
        }

        internal InputField AddInput(string placeholder, float x, float y, float width, float height,
                                     InputField.ContentType contentType = InputField.ContentType.Standard)
        {
            var go = GUIManager.Instance.CreateInputField(
                Panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y),
                contentType, placeholder, 16, width, height);
            go.SetActive(true);
            return go.GetComponent<InputField>();
        }
    }
}
