using System;
using System.ComponentModel;
using System.Drawing;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Settings;
using WpfColor = System.Windows.Media.Color;

namespace SmartInput.VisualStudio
{
    internal sealed class SmartInputSettingsSnapshot
    {
        public bool AutomaticSwitchingEnabled { get; set; }
        // Runtime-only; this is not read from or written to VS user settings.
        public bool Paused { get; set; }
        public bool StatusMarginVisible { get; set; }
        public Color ManualCaretColor { get; set; }
        public bool UseCustomStatusTextColor { get; set; }
        public Color StatusTextColor { get; set; }

        public WpfColor ManualCaretWpfColor => ToWpf(ManualCaretColor);
        public WpfColor StatusTextWpfColor => ToWpf(StatusTextColor);

        public static SmartInputSettingsSnapshot Defaults()
        {
            return new SmartInputSettingsSnapshot
            {
                AutomaticSwitchingEnabled = true,
                Paused = false,
                StatusMarginVisible = true,
                ManualCaretColor = Color.Red,
                UseCustomStatusTextColor = false,
                StatusTextColor = Color.White
            };
        }

        public SmartInputSettingsSnapshot Clone()
        {
            return new SmartInputSettingsSnapshot
            {
                AutomaticSwitchingEnabled = AutomaticSwitchingEnabled,
                Paused = Paused,
                StatusMarginVisible = StatusMarginVisible,
                ManualCaretColor = ManualCaretColor,
                UseCustomStatusTextColor = UseCustomStatusTextColor,
                StatusTextColor = StatusTextColor
            };
        }

        internal static WpfColor ToWpf(Color color) => WpfColor.FromRgb(color.R, color.G, color.B);
        internal static string ToHex(Color color) => "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

        internal static Color FromHex(string value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            try
            {
                var parsed = ColorTranslator.FromHtml(value.Trim());
                return Color.FromArgb(parsed.R, parsed.G, parsed.B);
            }
            catch
            {
                return fallback;
            }
        }
    }

    internal static class SmartInputSettings
    {
        private const string Collection = "SmartInput";
        private const string AutomaticSwitchingEnabledName = "AutomaticSwitchingEnabled";
        private const string StatusMarginVisibleName = "StatusMarginVisible";
        private const string ManualCaretColorName = "ManualCaretColor";
        private const string UseCustomStatusTextColorName = "UseCustomStatusTextColor";
        private const string StatusTextColorName = "StatusTextColor";

        private static readonly object Gate = new object();
        private static IServiceProvider serviceProvider;
        private static SmartInputSettingsSnapshot current = SmartInputSettingsSnapshot.Defaults();

        public static event EventHandler Changed;

        public static SmartInputSettingsSnapshot Current
        {
            get { lock (Gate) return current.Clone(); }
        }

        public static void Initialize(IServiceProvider provider)
        {
            if (provider != null)
            {
                lock (Gate) serviceProvider = provider;
            }
            Reload();
        }

        public static void Reload()
        {
            SetCurrent(LoadFromStorage(), false);
        }

        public static void Save(SmartInputSettingsSnapshot settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            SaveToStorage(settings);
            SetCurrent(settings, true);
        }

        public static void Update(Action<SmartInputSettingsSnapshot> update)
        {
            if (update == null) throw new ArgumentNullException(nameof(update));
            var next = Current;
            update(next);
            Save(next);
        }

        private static void SetCurrent(SmartInputSettingsSnapshot settings, bool alwaysNotify)
        {
            EventHandler changed = null;
            lock (Gate)
            {
                if (!alwaysNotify && Equivalent(current, settings)) return;
                current = settings.Clone();
                changed = Changed;
            }
            changed?.Invoke(null, EventArgs.Empty);
        }

        private static bool Equivalent(SmartInputSettingsSnapshot left, SmartInputSettingsSnapshot right)
        {
            return left.AutomaticSwitchingEnabled == right.AutomaticSwitchingEnabled
                && left.StatusMarginVisible == right.StatusMarginVisible
                && left.ManualCaretColor.ToArgb() == right.ManualCaretColor.ToArgb()
                && left.UseCustomStatusTextColor == right.UseCustomStatusTextColor
                && left.StatusTextColor.ToArgb() == right.StatusTextColor.ToArgb();
        }

        private static SmartInputSettingsSnapshot LoadFromStorage()
        {
            var defaults = SmartInputSettingsSnapshot.Defaults();
            var store = TryGetStore();
            if (store == null || !store.CollectionExists(Collection)) return defaults;
            return new SmartInputSettingsSnapshot
            {
                AutomaticSwitchingEnabled = GetBoolean(store, AutomaticSwitchingEnabledName, defaults.AutomaticSwitchingEnabled),
                StatusMarginVisible = GetBoolean(store, StatusMarginVisibleName, defaults.StatusMarginVisible),
                ManualCaretColor = SmartInputSettingsSnapshot.FromHex(GetString(store, ManualCaretColorName, SmartInputSettingsSnapshot.ToHex(defaults.ManualCaretColor)), defaults.ManualCaretColor),
                UseCustomStatusTextColor = GetBoolean(store, UseCustomStatusTextColorName, defaults.UseCustomStatusTextColor),
                StatusTextColor = SmartInputSettingsSnapshot.FromHex(GetString(store, StatusTextColorName, SmartInputSettingsSnapshot.ToHex(defaults.StatusTextColor)), defaults.StatusTextColor)
            };
        }

        private static void SaveToStorage(SmartInputSettingsSnapshot settings)
        {
            var store = TryGetStore();
            if (store == null) return;
            if (!store.CollectionExists(Collection)) store.CreateCollection(Collection);
            store.SetBoolean(Collection, AutomaticSwitchingEnabledName, settings.AutomaticSwitchingEnabled);
            store.SetBoolean(Collection, StatusMarginVisibleName, settings.StatusMarginVisible);
            store.SetString(Collection, ManualCaretColorName, SmartInputSettingsSnapshot.ToHex(settings.ManualCaretColor));
            store.SetBoolean(Collection, UseCustomStatusTextColorName, settings.UseCustomStatusTextColor);
            store.SetString(Collection, StatusTextColorName, SmartInputSettingsSnapshot.ToHex(settings.StatusTextColor));
        }

        private static bool GetBoolean(WritableSettingsStore store, string name, bool fallback)
        {
            return store.PropertyExists(Collection, name) ? store.GetBoolean(Collection, name) : fallback;
        }

        private static string GetString(WritableSettingsStore store, string name, string fallback)
        {
            return store.PropertyExists(Collection, name) ? store.GetString(Collection, name) : fallback;
        }

        private static WritableSettingsStore TryGetStore()
        {
            IServiceProvider provider;
            lock (Gate) provider = serviceProvider;
            if (provider == null) provider = Microsoft.VisualStudio.Shell.ServiceProvider.GlobalProvider;
            if (provider == null) return null;
            try
            {
                return new ShellSettingsManager(provider).GetWritableSettingsStore(SettingsScope.UserSettings);
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class SmartInputOptionsPage : Microsoft.VisualStudio.Shell.DialogPage
    {
        [Category("常规")]
        [DisplayName("启用自动切换")]
        [Description("关闭后插件不再主动读写输入法状态，也不会显示手动覆盖光标。")]
        [DefaultValue(true)]
        public bool AutomaticSwitchingEnabled { get; set; } = true;

        [Category("外观")]
        [DisplayName("显示左下角状态栏")]
        [Description("控制编辑器底部的 Smart Input 状态按钮是否显示。")]
        [DefaultValue(true)]
        public bool StatusMarginVisible { get; set; } = true;

        [Category("外观")]
        [DisplayName("手动覆盖光标颜色")]
        [Description("当当前输入态和自动推荐输入态不一致时，临时光标使用此颜色。")]
        [DefaultValue(typeof(Color), "Red")]
        public Color ManualCaretColor { get; set; } = Color.Red;

        [Category("外观")]
        [DisplayName("自定义状态栏文字颜色")]
        [Description("关闭时状态栏文字跟随 Visual Studio 主题；开启后使用下面的颜色。")]
        [DefaultValue(false)]
        public bool UseCustomStatusTextColor { get; set; }

        [Category("外观")]
        [DisplayName("状态栏文字颜色")]
        [Description("仅在启用自定义状态栏文字颜色时生效。透明或深色背景下可设为白色。")]
        [DefaultValue(typeof(Color), "White")]
        public Color StatusTextColor { get; set; } = Color.White;

        public override void LoadSettingsFromStorage()
        {
            FromSnapshot(SmartInputSettings.Current);
        }

        public override void SaveSettingsToStorage()
        {
            SmartInputSettings.Save(ToSnapshot());
        }

        public override void ResetSettings()
        {
            FromSnapshot(SmartInputSettingsSnapshot.Defaults());
        }

        private SmartInputSettingsSnapshot ToSnapshot()
        {
            return new SmartInputSettingsSnapshot
            {
                AutomaticSwitchingEnabled = AutomaticSwitchingEnabled,
                StatusMarginVisible = StatusMarginVisible,
                ManualCaretColor = ManualCaretColor,
                UseCustomStatusTextColor = UseCustomStatusTextColor,
                StatusTextColor = StatusTextColor
            };
        }

        private void FromSnapshot(SmartInputSettingsSnapshot settings)
        {
            AutomaticSwitchingEnabled = settings.AutomaticSwitchingEnabled;
            StatusMarginVisible = settings.StatusMarginVisible;
            ManualCaretColor = settings.ManualCaretColor;
            UseCustomStatusTextColor = settings.UseCustomStatusTextColor;
            StatusTextColor = settings.StatusTextColor;
        }
    }
}
