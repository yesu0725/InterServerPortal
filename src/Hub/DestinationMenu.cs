using System.Collections.Generic;
using UnityEngine;

namespace InterServerPortal.Hub
{
    /// <summary>
    /// The travel selection panel: one button per destination, greyed with a
    /// reason when unavailable (never silently dropped, per
    /// docs/Feature-Failure-Handling.md), plus Cancel. See docs/Feature-Hub-Routing.md.
    /// </summary>
    internal static class DestinationMenu
    {
        internal static void Show(List<HubEntry> entries)
        {
            const float width = 400f;
            const float rowH = 44f;
            const float rowGap = 8f;
            float height = 130f + entries.Count * (rowH + rowGap);

            var win = HubWindow.Open("Select destination", width, height);
            if (win == null) return;

            float top = height / 2f - 78f;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i]; // capture per-iteration for the closure
                float y = top - i * (rowH + rowGap);
                string text = entry.IsActionable
                    ? entry.Label
                    : $"{entry.Label}  —  {entry.UnavailableReason}";

                win.AddButton(text, 0f, y, width - 50f, rowH,
                    () => HubController.Go(entry), interactable: entry.IsActionable);
            }

            win.AddButton("Cancel", 0f, -height / 2f + 34f, 150f, 38f, HubWindow.Close);
        }
    }
}
