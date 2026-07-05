using System;
using InterServerPortal.Portal;
using InterServerPortal.Security;
using UnityEngine;
using UnityEngine.UI;

namespace InterServerPortal.Hub
{
    /// <summary>
    /// The entry-code prompt shown before a locked portal will travel. On a
    /// correct code it runs <c>onSuccess</c>; a wrong code denies with a message
    /// and registers a throttle failure. See docs/Feature-Lock-Codes.md.
    /// </summary>
    internal static class CodePrompt
    {
        internal static void Show(TeleportWorld portal, Action onSuccess)
        {
            const float width = 400f;
            const float topMargin = 66f;
            const float labelH = 28f;
            const float inputH = 38f;
            const float btnH = 40f;
            const float gap = 18f;
            const float bottomMargin = 26f;

            float height = topMargin + labelH + gap + inputH + gap + btnH + bottomMargin;

            var win = HubWindow.Open("Locked portal", width, height);
            if (win == null)
            {
                // No UI available — don't lock the player out.
                onSuccess?.Invoke();
                return;
            }

            float cursor = height / 2f - topMargin;
            float Place(float h, float gapAfter)
            {
                float centreY = cursor - h / 2f;
                cursor -= h + gapAfter;
                return centreY;
            }

            win.AddLabel("Enter the portal's code:", 0f, Place(labelH, gap),
                width - 60f, labelH, 16, Color.white);

            var input = win.AddInput("Code", 0f, Place(inputH, gap), width - 100f, inputH,
                InputField.ContentType.Password);

            float btnY = Place(btnH, bottomMargin);
            win.AddButton("Enter", -90f, btnY, 150f, btnH, () =>
            {
                string entered = input != null ? input.text : "";
                if (PortalData.CheckCode(portal.m_nview, entered))
                {
                    HubWindow.Close();
                    onSuccess?.Invoke();
                }
                else
                {
                    LockGate.RegisterFail(portal);
                    HubWindow.Close();
                    var player = Player.m_localPlayer;
                    if (player != null)
                        player.Message(MessageHud.MessageType.Center, "Incorrect code.");
                }
            });

            win.AddButton("Cancel", 90f, btnY, 150f, btnH, HubWindow.Close);
        }
    }
}
