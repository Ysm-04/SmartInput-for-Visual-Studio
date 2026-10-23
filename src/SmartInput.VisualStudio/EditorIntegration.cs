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
    [ContentType("C/C++"), ContentType("CSharp"), ContentType("XAML"), ContentType(EditorLanguage.Sql)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class ViewListener : IWpfTextViewCreationListener
    {
        [Export(typeof(AdornmentLayerDefinition))]
        [Name(WpfCaretPresentation.LayerName)]
        [Order(After = PredefinedAdornmentLayers.Caret)]
        internal AdornmentLayerDefinition ManualCaretLayer = null;

        [Import] internal SessionFactory Sessions = null;
        public void TextViewCreated(IWpfTextView textView)
        {
            // Debugger expression views (Watch / Immediate / QuickWatch) also carry the C# content
            // type; only real editor documents get a session (see ViewEligibility).
            if (ViewEligibility.IsDocumentEditor(textView)) Sessions.Get(textView);
        }
    }

    // Some SSMS builds surface the T-SQL query editor under a content type derived from "code" whose
    // name is not literally "SQL" (e.g. a T-SQL / SQL Server Tools type). Listen on "code" and filter
    // to SQL-like types at runtime so the session still attaches, without taking other editors over.
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class SqlFallbackListener : IWpfTextViewCreationListener
    {
        [Import] internal SessionFactory Sessions = null;
        public void TextViewCreated(IWpfTextView textView)
        {
            if (ViewEligibility.IsDocumentEditor(textView)
                && EditorLanguage.IsSqlDerivedOnly(textView.TextBuffer.ContentType)) Sessions.Get(textView);
        }
    }

    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(SmartInputMargin.MarginName + ".SqlFallback")]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType("code")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class SqlFallbackMarginProvider : IWpfTextViewMarginProvider
    {
        [Import] internal SessionFactory Sessions = null;
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost host, IWpfTextViewMargin container)
        {
            // Only attach to renamed SQL-like types not already covered by the exact "SQL" export;
            // every other "code" editor (plain text, other languages) is left untouched.
            if (!ViewEligibility.IsDocumentEditor(host.TextView)
                || !EditorLanguage.IsSqlDerivedOnly(host.TextView.TextBuffer.ContentType)) return null;
            return new SmartInputMargin(Sessions.Get(host.TextView));
        }
    }

    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(SmartInputMargin.MarginName)]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType("C/C++"), ContentType("CSharp"), ContentType("XAML"), ContentType(EditorLanguage.Sql)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class MarginProvider : IWpfTextViewMarginProvider
    {
        [Import] internal SessionFactory Sessions = null;
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost host, IWpfTextViewMargin container)
            // The bottom margin is also offered to tool-window text views (debugger expression
            // boxes). Returning null there keeps the status button out of Watch / Immediate /
            // QuickWatch instead of drawing it over their input fields.
            => ViewEligibility.IsDocumentEditor(host.TextView)
                ? new SmartInputMargin(Sessions.Get(host.TextView))
                : null;
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
            if (settings.UseCustomStatusBackgroundColor)
                button.Background = new SolidColorBrush(settings.StatusBackgroundWpfColor);
            else
                button.ClearValue(Control.BackgroundProperty);
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

    /// <summary>Identifies the Transact-SQL query editor across Visual Studio and SSMS content types.</summary>
    internal static class EditorLanguage
    {
        public const string Sql = "SQL";

        public static bool IsSql(Microsoft.VisualStudio.Utilities.IContentType contentType)
        {
            if (contentType == null) return false;
            if (contentType.IsOfType(Sql)) return true;
            // Derived/renamed SQL types seen in SSMS (T-SQL, SQL Server Tools, ...).
            string name = contentType.TypeName;
            return name != null && name.IndexOf("SQL", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // True only for SQL-like types that are NOT a proper "SQL" subtype. This covers renamed
        // SSMS T-SQL types via the "code" fallback without double-handling views the primary
        // [ContentType("SQL")] listener/margin exports already attach to.
        public static bool IsSqlDerivedOnly(Microsoft.VisualStudio.Utilities.IContentType contentType)
        {
            if (contentType == null || contentType.IsOfType(Sql)) return false;
            string name = contentType.TypeName;
            return name != null && name.IndexOf("SQL", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}