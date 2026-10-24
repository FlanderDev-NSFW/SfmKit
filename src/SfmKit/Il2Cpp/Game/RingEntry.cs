using System;
using BepInEx.Logging;
using ExposureUnnoticed2.Object3D.Player.Scripts;
using ExposureUnnoticed2.ObjectUI.InGame.RingMenu;
using ExposureUnnoticed2.Scripts.Base;
using ExposureUnnoticed2.Scripts.InGame;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SfmKit.Game
{
    /// <summary>
    /// A mod's entry in the common ring menu (hold 1), with the game's number shortcuts. Call <see cref="Tick"/> from the mod's
    /// Update; the mod translates <see cref="LabelKey"/> itself (Harmony postfix on <c>Localizer.Trans(string)</c>) and stores
    /// <see cref="ShortcutMask"/> in the save (<c>SaveRapper.Save</c> after <c>SaveManager.Save</c>), handing it back with
    /// <see cref="OnShortcutsLoaded"/>.
    ///
    /// Game facts this relies on [observed 2026-10-11, SpreaderBar; generalised 2026-10-24 for WallRestraint]:
    /// - entries are <c>RingMenuParentView.commonRingMenuModel</c>; the array is rebuilt on loads and stage changes, so the entry
    ///   is re-added whenever the array changes. Other mods do the same, each copying the whole array: mod entries use
    ///   RingItemType values of <see cref="ModValueStart"/> and up, the game's are below.
    /// - there is no safe hook on the game's handler (patching <c>OnInvokeCommandCommon</c> broke saving), so a click is read
    ///   here: the entry selected on the shown page + a left click this frame.
    /// - releasing ring key N over an entry stores it as that key's shortcut (<c>lastInvokeModel*</c>) and its index in
    ///   <c>GameStateData.RingShortcutModelIndex*</c>, which is saved. On load the game reads that index before any mod entry
    ///   exists: a mod index threw IndexOutOfRange and the stage never started. So the stored index is always kept on a game
    ///   entry (the key's previous one), and the keys holding the mod entry are restored from <see cref="ShortcutMask"/>.
    /// - a short tap of N runs the key's model through the game's handler, which ignores mod entries: detected here with
    ///   <c>GetShortUp</c>.
    /// Verified: in game through SpreaderBar 0.2.0+ (L3) - this class is that code generalised; WallRestraint is its first user.
    /// </summary>
    public sealed class RingEntry
    {
        /// <summary>Mod entries use RingItemType values from here up (the game: 1..14 items, 10000+ poses - checked: no game
        /// entry is in 9000..9999 [observed 2026-10-11]).</summary>
        public const int ModValueStart = 9000, ModValueEnd = 9999;

        public readonly int Value;
        public readonly string LabelKey;
        private readonly Action _onChosen;
        private readonly ManualLogSource _log;
        private readonly string _what;

        private IntPtr _ringArray;
        private int _index = -1;
        private int _keys;                 // bit k: shortcut key k+1 runs the entry
        private bool _applyShortcuts;
        private readonly int[] _fallbackIndex = new int[8];
        private static Il2CppSystem.Func<bool> _alwaysReleased;

        /// <param name="value">RingItemType value, unique among mods (<see cref="ModValueStart"/>..<see cref="ModValueEnd"/>)</param>
        /// <param name="labelKey">localisation key the ring shows; translate it in a <c>Localizer.Trans</c> postfix</param>
        /// <param name="onChosen">run when the player picks the entry (click or number shortcut)</param>
        /// <param name="what">name for the log ("wall restraint")</param>
        public RingEntry(int value, string labelKey, Action onChosen, ManualLogSource log, string what)
        {
            if (value < ModValueStart || value > ModValueEnd) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
            LabelKey = labelKey;
            _onChosen = onChosen;
            _log = log;
            _what = what;
        }

        /// <summary>Keys (bit k = key k+1) that currently hold this entry - save it.</summary>
        public int ShortcutMask => _keys;

        public void OnShortcutsLoaded(int mask)
        {
            _keys = mask & 0xFF;
            _applyShortcuts = true;
        }

        /// <summary>A new game / the title replaced the game data without a load: forget the keys.</summary>
        public void Reset() => _keys = 0;

        /// <summary>The game's own entries in a ring array (mod entries are appended after them).</summary>
        public static int CountVanilla(Il2CppReferenceArray<RingMenuItemModel> models)
        {
            int n = 0;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null && !IsMod(models[i])) n++;
            return n;
        }

        private static bool IsMod(RingMenuItemModel m) => (int)m.RingItemType >= ModValueStart && (int)m.RingItemType <= ModValueEnd;

        /// <summary>Once per frame. <paramref name="uiOpen"/>: a game panel is open (shortcuts must not fire).</summary>
        public void Tick(bool uiOpen)
        {
            var parent = RingMenuParentView.Instance;
            if (parent == null) return;
            EnsureEntry(parent);
            WatchClick(parent);
            SyncShortcuts(parent);
            if (!uiOpen) WatchShortcuts(parent);
        }

        private void EnsureEntry(RingMenuParentView view)
        {
            var models = view.commonRingMenuModel;
            if (models == null || models.Pointer == _ringArray) return;
            for (int i = 0; i < models.Length; i++)
                if (models[i] != null && (int)models[i].RingItemType == Value)
                {
                    _ringArray = models.Pointer;
                    _index = i;
                    _applyShortcuts = true;
                    return;
                }
            if (_alwaysReleased == null) _alwaysReleased = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<bool>>(new Func<bool>(() => true));
            var entry = new RingMenuItemModel
            {
                RingItemType = (RingMenuParentView.RingItemType)Value,
                NameKey = LabelKey,
                Index = models.Length,
                ReleaseCondition = _alwaysReleased,
            };
            var grown = new Il2CppReferenceArray<RingMenuItemModel>(models.Length + 1);
            for (int i = 0; i < models.Length; i++) grown[i] = models[i];
            grown[models.Length] = entry;
            view.commonRingMenuModel = grown;
            _ringArray = grown.Pointer;
            _index = models.Length;
            _applyShortcuts = true;
            _log.LogInfo("[SFM-ACCEPT] ring: " + _what + " entry added at index " + models.Length);
            // 8 slots per page; the ring adds pages as needed (33 entries -> 5 pages, the 33rd on page 5) [observed 2026-10-24].
        }

        private void WatchClick(RingMenuParentView parent)
        {
            if (!parent.IsOpenRing) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            var views = parent.openRingList;
            int page = parent.CurrentRingIndexCommon;
            if (views == null || page < 0 || page >= views.Count) return;
            var selected = views[page] == null ? null : views[page].currentSelectView;
            if (selected != null && selected.Model != null && (int)selected.Model.RingItemType == Value)
            {
                _log.LogInfo("[SFM-ACCEPT] ring: " + _what + " chosen");
                _onChosen();
            }
        }

        private void SyncShortcuts(RingMenuParentView parent)
        {
            var data = GameState.GameStateData;
            if (data == null || _index < 0 || parent.commonRingMenuModel == null || _ringArray != parent.commonRingMenuModel.Pointer) return;
            var models = parent.commonRingMenuModel;
            var ours = models[_index];
            if (_applyShortcuts)
            {
                _applyShortcuts = false;
                for (int k = 0; k < 8; k++)
                    if ((_keys & (1 << k)) != 0) SetShortcutModel(parent, k, ours);
                if (_keys != 0) _log.LogInfo("[SFM-ACCEPT] shortcut: " + _what + " restored on keys " + KeyList(_keys));
            }
            int vanilla = CountVanilla(models);
            for (int k = 0; k < 8; k++)
            {
                var model = ShortcutModel(parent, k);
                int index = ShortcutIndex(data, k);
                if (model != null && (int)model.RingItemType == Value)
                {
                    if ((_keys & (1 << k)) == 0) _log.LogInfo("[SFM-ACCEPT] shortcut: " + _what + " assigned to key " + (k + 1));
                    _keys |= 1 << k;
                    if (index >= vanilla) SetShortcutIndex(data, k, _fallbackIndex[k]); // never save a mod index
                }
                else
                {
                    _keys &= ~(1 << k); // reassigned to another entry
                    if (index >= 0 && index < vanilla) _fallbackIndex[k] = index;
                }
            }
        }

        private static readonly InputManager.InputType[] ShortcutKeys =
        {
            InputManager.InputType.CommonRingMenu, InputManager.InputType.SkillRingMenu, InputManager.InputType.SkillRingMenu3,
            InputManager.InputType.SkillRingMenu4, InputManager.InputType.SkillRingMenu5, InputManager.InputType.SkillRingMenu6,
            InputManager.InputType.SkillRingMenu7, InputManager.InputType.SkillRingMenu8,
        };

        private void WatchShortcuts(RingMenuParentView parent)
        {
            if (parent.IsOpenRing || PlayerController.Instance == null) return;
            for (int i = 0; i < ShortcutKeys.Length; i++)
            {
                var model = ShortcutModel(parent, i);
                if (model == null || (int)model.RingItemType != Value) continue;
                if (!parent.GetShortUp(ShortcutKeys[i])) continue;
                _log.LogInfo("[SFM-ACCEPT] shortcut: key " + (i + 1) + " runs the " + _what + " entry");
                _onChosen();
                return;
            }
        }

        private static string KeyList(int mask)
        {
            var s = "";
            for (int k = 0; k < 8; k++) if ((mask & (1 << k)) != 0) s += (s.Length == 0 ? "" : ",") + (k + 1);
            return s;
        }

        private static RingMenuItemModel ShortcutModel(RingMenuParentView p, int key) => key switch
        {
            0 => p.lastInvokeModelCommon, 1 => p.lastInvokeModelSkill, 2 => p.lastInvokeModelSkill3, 3 => p.lastInvokeModelSkill4,
            4 => p.lastInvokeModelSkill5, 5 => p.lastInvokeModelSkill6, 6 => p.lastInvokeModelSkill7, _ => p.lastInvokeModelSkill8,
        };

        private static void SetShortcutModel(RingMenuParentView p, int key, RingMenuItemModel model)
        {
            switch (key)
            {
                case 0: p.lastInvokeModelCommon = model; break;
                case 1: p.lastInvokeModelSkill = model; break;
                case 2: p.lastInvokeModelSkill3 = model; break;
                case 3: p.lastInvokeModelSkill4 = model; break;
                case 4: p.lastInvokeModelSkill5 = model; break;
                case 5: p.lastInvokeModelSkill6 = model; break;
                case 6: p.lastInvokeModelSkill7 = model; break;
                default: p.lastInvokeModelSkill8 = model; break;
            }
            var views = p.shortcutItemViews;
            if (views == null) return;
            foreach (var v in views)
                if (v != null && (int)v.type == key) v.SetName(model);
        }

        public static int ShortcutIndex(GameStateData d, int key) => key switch
        {
            0 => d.RingShortcutModelIndexCommon, 1 => d.RingShortcutModelIndexSkill, 2 => d.RingShortcutModelIndexSkill3,
            3 => d.RingShortcutModelIndexSkill4, 4 => d.RingShortcutModelIndexSkill5, 5 => d.RingShortcutModelIndexSkill6,
            6 => d.RingShortcutModelIndexSkill7, _ => d.RingShortcutModelIndexSkill8,
        };

        public static void SetShortcutIndex(GameStateData d, int key, int index)
        {
            switch (key)
            {
                case 0: d.RingShortcutModelIndexCommon = index; break;
                case 1: d.RingShortcutModelIndexSkill = index; break;
                case 2: d.RingShortcutModelIndexSkill3 = index; break;
                case 3: d.RingShortcutModelIndexSkill4 = index; break;
                case 4: d.RingShortcutModelIndexSkill5 = index; break;
                case 5: d.RingShortcutModelIndexSkill6 = index; break;
                case 6: d.RingShortcutModelIndexSkill7 = index; break;
                default: d.RingShortcutModelIndexSkill8 = index; break;
            }
        }
    }
}
