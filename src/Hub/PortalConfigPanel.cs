using System;
using System.Collections.Generic;
using Jotunn.Managers;
using InterServerPortal.Portal;
using UnityEngine;
using UnityEngine.UI;

namespace InterServerPortal.Hub
{
    /// <summary>
    /// The per-portal config/editor panel (opened with alt-use). Toggles the
    /// inter-server flag and manages the destination list stored on the portal's
    /// ZDO. The panel rebuilds itself on every change (add/remove/toggle) to keep
    /// the layout simple. See docs/Feature-Hub-Routing.md + docs/Data-Model-ZDO.md.
    /// </summary>
    internal static class PortalConfigPanel
    {
        private static ZNetView _nview;
        private static bool _enabled;
        private static List<Destination> _dests;

        private enum CodeMode { Unchanged, Set, Clear }
        private static bool _lockedInitially;
        private static CodeMode _codeMode;
        private static string _codeValue;

        private static int _linkMode;     // ISP.link: 0 = tag pair (vanilla), 1 = network

        internal static void Show(TeleportWorld portal)
        {
            if (portal == null || portal.m_nview == null) return;
            _nview = portal.m_nview;
            _enabled = PortalData.IsInterServer(_nview);
            _dests = PortalData.GetDestinations(_nview);
            _lockedInitially = PortalData.IsLocked(_nview);
            _codeMode = CodeMode.Unchanged;
            _codeValue = "";
            _linkMode = PortalData.GetLinkMode(_nview);
            Rebuild();
        }

        // Layout metrics (offsets from the panel centre; y counts down from the top).
        private const float Width = 520f;
        private const float TopMargin = 74f;   // gap below the title
        private const float FlagH = 40f;
        private const float HeaderH = 30f;
        private const float RowH = 34f;
        private const float RowGap = 10f;
        private const float AddRowH = 36f;
        private const float AddBtnH = 38f;
        private const float SaveH = 42f;
        private const float SectionGap = 18f;
        private const float BottomMargin = 26f;

        private static void Rebuild()
        {
            int rowCount = _dests.Count > 0 ? _dests.Count : 1; // the "(none)" line counts as a row
            float height =
                TopMargin
                + FlagH + SectionGap   // inter-server flag button
                + FlagH + SectionGap   // same-world link-mode button
                + HeaderH + RowGap
                + rowCount * (RowH + RowGap)
                + SectionGap
                + AddRowH + SectionGap
                + AddBtnH + SectionGap
                // lock section: header + code input row + set/clear buttons
                + HeaderH + RowGap
                + AddRowH + RowGap
                + AddBtnH + SectionGap
                + SaveH + BottomMargin;

            var win = HubWindow.Open("Configure portal", Width, height);
            if (win == null) return;

            // Top-of-next-element cursor; Place() centres an element and advances it.
            float cursor = height / 2f - TopMargin;
            float Place(float h, float gapAfter)
            {
                float centreY = cursor - h / 2f;
                cursor -= h + gapAfter;
                return centreY;
            }

            // Inter-server flag (a flip button — simpler + clearer than a bare toggle).
            win.AddButton(_enabled ? "Inter-server portal: ON" : "Inter-server portal: OFF",
                0f, Place(FlagH, SectionGap), Width - 80f, FlagH,
                () => { _enabled = !_enabled; Rebuild(); });

            // Same-world link mode (ignored while inter-server is ON, which wins).
            string linkLabel = _linkMode == PortalData.LinkNetwork
                ? "Same-world link: NETWORK (all my portals)"
                : "Same-world link: TAG pair (vanilla)";
            win.AddButton(linkLabel, 0f, Place(FlagH, SectionGap), Width - 80f, FlagH, () =>
            {
                _linkMode = _linkMode == PortalData.LinkNetwork
                    ? PortalData.LinkTag : PortalData.LinkNetwork;
                Rebuild();
            });

            win.AddLabel("Destinations", 0f, Place(HeaderH, RowGap), Width - 80f, HeaderH, 18,
                GUIManager.Instance.ValheimOrange);

            if (_dests.Count == 0)
            {
                win.AddLabel("(none yet — add one below)", 0f, Place(RowH, RowGap),
                    Width - 100f, RowH, 14, Color.gray);
            }
            else
            {
                foreach (var dest in _dests)
                {
                    var captured = dest;
                    float rowY = Place(RowH, RowGap);
                    win.AddLabel($"{dest.Label}  ({dest.WorldName})",
                        -90f, rowY, 250f, RowH, 15, Color.white);
                    win.AddButton("Remove", 170f, rowY, 110f, 30f,
                        () => { _dests.Remove(captured); Rebuild(); });
                }
            }

            cursor -= SectionGap - RowGap; // reach the full SectionGap before the add row

            // ---- Add-new row: label input + world dropdown ----
            float addRowY = Place(AddRowH, SectionGap);
            var labelInput = win.AddInput("Label (optional)", -130f, addRowY, 220f, AddRowH);
            var worldNames = LocalWorldNames();
            var dropdown = CreateWorldDropdown(win, 120f, addRowY, worldNames);

            win.AddButton("Add destination", 0f, Place(AddBtnH, SectionGap), 300f, AddBtnH, () =>
            {
                string world = SelectedWorld(dropdown, worldNames);
                if (string.IsNullOrEmpty(world)) return;
                string label = labelInput != null ? labelInput.text : "";
                _dests.Add(new Destination(label, world));
                Rebuild();
            }, interactable: worldNames.Count > 0);

            // ---- Lock section (entry code) ----
            win.AddLabel("Lock — " + LockStateText(), 0f, Place(HeaderH, RowGap), Width - 80f, HeaderH,
                18, GUIManager.Instance.ValheimOrange);

            var codeInput = win.AddInput("New code (blank = unchanged)", 0f,
                Place(AddRowH, RowGap), Width - 120f, AddRowH, InputField.ContentType.Password);

            float lockBtnY = Place(AddBtnH, SectionGap);
            win.AddButton("Set code", -90f, lockBtnY, 180f, AddBtnH, () =>
            {
                string code = codeInput != null ? codeInput.text : "";
                if (string.IsNullOrEmpty(code)) return;
                _codeMode = CodeMode.Set;
                _codeValue = code;
                Rebuild();
            });
            win.AddButton("Clear lock", 90f, lockBtnY, 180f, AddBtnH, () =>
            {
                _codeMode = CodeMode.Clear;
                _codeValue = "";
                Rebuild();
            });

            // ---- Save & Close ----
            win.AddButton("Save & Close", 0f, Place(SaveH, BottomMargin), 220f, SaveH, Save);
        }

        private static string LockStateText()
        {
            switch (_codeMode)
            {
                case CodeMode.Set:   return "will be LOCKED (new code on save)";
                case CodeMode.Clear: return "will be UNLOCKED on save";
                default:             return _lockedInitially ? "locked" : "not locked";
            }
        }

        private static void Save()
        {
            if (_nview != null)
            {
                PortalData.SetInterServer(_nview, _enabled);
                PortalData.SetDestinations(_nview, _dests);

                if (_codeMode == CodeMode.Set)
                    PortalData.SetCode(_nview, _codeValue);
                else if (_codeMode == CodeMode.Clear)
                    PortalData.SetCode(_nview, null);

                PortalData.SetLinkMode(_nview, _linkMode);

                Plugin.Log.LogInfo(
                    $"Portal config saved: enabled={_enabled}, link={_linkMode}, " +
                    $"{_dests.Count} destination(s), code={_codeMode}.");
            }
            HubWindow.Close();
        }

        // ---- Helpers ----

        private static Dropdown CreateWorldDropdown(HubWindow win, float x, float y, List<string> worldNames)
        {
            var go = GUIManager.Instance.CreateDropDown(
                win.Panel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y),
                16, 200f, 34f);
            go.SetActive(true);
            var dropdown = go.GetComponent<Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(worldNames.Count > 0 ? worldNames : new List<string> { "(no local worlds)" });
            return dropdown;
        }

        private static string SelectedWorld(Dropdown dropdown, List<string> worldNames)
        {
            if (dropdown == null || worldNames.Count == 0) return "";
            int i = dropdown.value;
            return (i >= 0 && i < worldNames.Count) ? worldNames[i] : "";
        }

        /// <summary>Distinct local world names on disk (menu placeholder excluded).</summary>
        private static List<string> LocalWorldNames()
        {
            var names = new List<string>();
            try
            {
                var worlds = SaveSystem.GetWorldList();
                if (worlds != null)
                {
                    foreach (var w in worlds)
                    {
                        if (w == null || w.m_menu || string.IsNullOrEmpty(w.m_name)) continue;
                        if (!names.Contains(w.m_name)) names.Add(w.m_name);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"PortalConfigPanel: listing local worlds failed: {e}");
            }
            return names;
        }
    }
}
