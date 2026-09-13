using System;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace SmartInput.VisualStudio
{
    [Export]
    internal sealed class SessionFactory
    {
        [Import] internal IClassifierAggregatorService Classifiers = null;
        [Import(typeof(SVsServiceProvider), AllowDefault = true)] internal IServiceProvider ServiceProvider = null;

        public EditorSession Get(IWpfTextView view)
        {
            SessionSettings.Initialize(ServiceProvider);
            return view.Properties.GetOrCreateSingletonProperty(
                () => new EditorSession(view, Classifiers.GetClassifier(view.TextBuffer)));
        }
    }

    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("C/C++"), ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class ViewListener : IWpfTextViewCreationListener
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(WpfCaretPresentation.LayerName)]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        internal AdornmentLayerDefinition ManualCaretLayer = null;

        [Import] internal SessionFactory Sessions = null;
        public void TextViewCreated(IWpfTextView textView) { Sessions.Get(textView); }
    }

    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(SmartInputMargin.MarginName)]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType("C/C++"), ContentType("CSharp")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class MarginProvider : IWpfTextViewMarginProvider
    {
        [Import] internal SessionFactory Sessions = null;
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost host, IWpfTextViewMargin container)
            => new SmartInputMargin(Sessions.Get(host.TextView));
    }

    internal sealed class SmartInputMargin : IWpfTextViewMargin
    {
        internal const string MarginName = "SmartInput.Status";
        private readonly EditorSession session;
        private readonly Button button;
        private bool disposed;

        public SmartInputMargin(EditorSession session)
        {
            this.session = session;
            button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(2, 0, 2, 0),
                Focusable = false,
                ToolTip = "单击：暂停/恢复所有编辑器的智能切换。红色光标表示手动覆盖；不会修改源文件。"
            };
            button.Click += Toggle;
            session.StatusChanged += Update;
            SessionSettings.Changed += Update;
            Update(this, EventArgs.Empty);
        }

        private void Toggle(object sender, RoutedEventArgs e) { SessionSettings.TogglePaused(); }
        private void Update(object sender, EventArgs e)
        {
            var settings = SessionSettings.Current;
            button.Visibility = settings.StatusMarginVisible ? Visibility.Visible : Visibility.Collapsed;
            if (settings.UseCustomStatusTextColor)
                button.Foreground = new SolidColorBrush(settings.StatusTextWpfColor);
            else
                button.ClearValue(Control.ForegroundProperty);
            button.Content = "Smart Input · " + session.Status;
        }
        public FrameworkElement VisualElement => button;
        public double MarginSize => button.Visibility == Visibility.Visible ? button.ActualHeight : 0;
        public bool Enabled => !disposed;
        public ITextViewMargin GetTextViewMargin(string marginName) => marginName == MarginName ? this : null;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            button.Click -= Toggle;
            session.StatusChanged -= Update;
            SessionSettings.Changed -= Update;
        }
    }

    internal static class SessionSettings
    {
        private static readonly object Gate = new object();
        private static bool paused;

        static SessionSettings()
        {
            SmartInputSettings.Changed += OnPersistentSettingsChanged;
        }

        public static SmartInputSettingsSnapshot Current
        {
            get
            {
                var settings = SmartInputSettings.Current;
                settings.Paused = settings.AutomaticSwitchingEnabled && Paused;
                return settings;
            }
        }

        public static bool Paused
        {
            get
            {
                lock (Gate) return paused;
            }
        }

        public static event EventHandler Changed;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            SmartInputSettings.Initialize(serviceProvider);
        }

        public static bool TogglePaused()
        {
            if (!SmartInputSettings.Current.AutomaticSwitchingEnabled) return false;
            lock (Gate) paused = !paused;
            RaiseChanged();
            return true;
        }

        private static void OnPersistentSettingsChanged(object sender, EventArgs e)
        {
            if (!SmartInputSettings.Current.AutomaticSwitchingEnabled)
            {
                lock (Gate) paused = false;
            }

            RaiseChanged();
        }

        private static void RaiseChanged()
        {
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
