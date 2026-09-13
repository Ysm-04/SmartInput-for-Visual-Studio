using System;
using System.Windows;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace SmartInput.VisualStudio
{
    /// <summary>Only public editor APIs; no private-field reflection and no format-map writes.</summary>
    internal sealed class WpfCaretPresentation : ICaretPresentation
    {
        internal const string LayerName = "SmartInput.ManualCaret";
        private readonly IWpfTextView view;
        private readonly IAdornmentLayer layer;
        private readonly IMultiSelectionBroker selections;
        private readonly object tag = new object();
        private bool disposed;

        public WpfCaretPresentation(IWpfTextView view)
        {
            this.view = view;
            layer = view.GetAdornmentLayer(LayerName);
            selections = view.GetMultiSelectionBroker();
            view.Caret.PositionChanged += OnCaretChanged;
            view.LayoutChanged += OnLayoutChanged;
            view.ZoomLevelChanged += OnZoomChanged;
            view.Options.OptionChanged += OnOptionChanged;
            view.Selection.SelectionChanged += OnChanged;
            view.GotAggregateFocus += OnChanged;
            view.LostAggregateFocus += OnChanged;
            view.VisualElement.IsKeyboardFocusWithinChanged += OnFocusChanged;
            view.Closed += OnClosed;
            if (selections != null) selections.MultiSelectionSessionChanged += OnChanged;
        }

        public bool CanRender => !disposed && !view.IsClosed && !view.InLayout
            && view.VisualElement.IsKeyboardFocusWithin && view.HasAggregateFocus
            && view.Selection.IsEmpty && selections?.HasMultipleSelections != true;

        public Rect Bounds
        {
            get
            {
                if (view.Caret.ContainingTextViewLine == null) return Rect.Empty;
                double width = view.Caret.Width, height = view.Caret.Height;
                if (width <= 0 || height <= 0) return Rect.Empty;
                return new Rect(view.Caret.Left, view.Caret.Top, width, height);
            }
        }

        public Rect Viewport => new Rect(view.ViewportLeft, view.ViewportTop, view.ViewportWidth, view.ViewportHeight);
        public bool NativeCaretHidden { get => view.Caret.IsHidden; set => view.Caret.IsHidden = value; }
        public event EventHandler Changed;
        public event EventHandler Closed;

        public bool Attach(Rectangle visual, Action removed) => layer.AddAdornment(
            AdornmentPositioningBehavior.OwnerControlled, null, tag, visual, (owner, element) => removed());
        public void Detach() { layer.RemoveAdornmentsByTag(tag); }
        private void OnCaretChanged(object sender, CaretPositionChangedEventArgs e) { OnChanged(sender, e); }
        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) { OnChanged(sender, e); }
        private void OnZoomChanged(object sender, ZoomLevelChangedEventArgs e) { OnChanged(sender, e); }
        private void OnOptionChanged(object sender, EditorOptionChangedEventArgs e) { OnChanged(sender, e); }
        private void OnFocusChanged(object sender, DependencyPropertyChangedEventArgs e) { OnChanged(sender, EventArgs.Empty); }
        private void OnChanged(object sender, EventArgs e) { Changed?.Invoke(this, EventArgs.Empty); }
        private void OnClosed(object sender, EventArgs e) { Closed?.Invoke(this, EventArgs.Empty); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.Caret.PositionChanged -= OnCaretChanged;
            view.LayoutChanged -= OnLayoutChanged;
            view.ZoomLevelChanged -= OnZoomChanged;
            view.Options.OptionChanged -= OnOptionChanged;
            view.Selection.SelectionChanged -= OnChanged;
            view.GotAggregateFocus -= OnChanged;
            view.LostAggregateFocus -= OnChanged;
            view.VisualElement.IsKeyboardFocusWithinChanged -= OnFocusChanged;
            view.Closed -= OnClosed;
            if (selections != null) selections.MultiSelectionSessionChanged -= OnChanged;
        }
    }
}
