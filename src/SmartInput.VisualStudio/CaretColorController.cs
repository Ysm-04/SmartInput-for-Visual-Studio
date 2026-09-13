using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SmartInput.VisualStudio
{
    internal interface ICaretPresentation : IDisposable
    {
        bool CanRender { get; }
        Rect Bounds { get; }
        Rect Viewport { get; }
        bool NativeCaretHidden { get; set; }
        bool Attach(Rectangle visual, Action removed);
        void Detach();
        event EventHandler Changed;
        event EventHandler Closed;
    }

    internal interface ICaretBlinkTimer : IDisposable
    {
        event EventHandler Tick;
        void Restart();
        void Stop();
    }

    /// <summary>
    /// Owns a view-local custom-color caret, never a theme/Plain Text format override. In VS 17.14 the
    /// single-caret renderer uses Plain Text, not the old "Caret" editor-format entry.
    /// </summary>
    internal sealed class CaretColorController : IDisposable
    {
        private readonly ICaretPresentation presentation;
        private readonly ICaretBlinkTimer blink;
        private readonly Rectangle visual;
        private bool requested, attached, ownsNativeVisibility, updating, disposed, failed;
        private Rect lastBounds = Rect.Empty;

        public CaretColorController(ICaretPresentation presentation, ICaretBlinkTimer blink)
        {
            this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            this.blink = blink ?? throw new ArgumentNullException(nameof(blink));
            visual = new Rectangle
            {
                Fill = Brushes.Red,
                IsHitTestVisible = false,
                Focusable = false,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                Visibility = Visibility.Hidden
            };
            presentation.Changed += OnChanged;
            presentation.Closed += OnClosed;
            blink.Tick += OnBlink;
        }

        public void SetColor(Color color)
        {
            if (disposed) return;
            var brush = visual.Fill as SolidColorBrush;
            if (brush != null && brush.Color == color) return;
            visual.Fill = new SolidColorBrush(color);
        }

        public void SetRed(bool value)
        {
            if (disposed || failed) return;
            bool changed = requested != value;
            requested = value;
            Refresh(changed);
        }

        private void OnChanged(object sender, EventArgs e) { Refresh(true); }
        private void OnClosed(object sender, EventArgs e) { Dispose(); }

        private void Refresh(bool resetBlink)
        {
            if (updating || disposed || failed) return;
            updating = true;
            try
            {
                if (!requested || !presentation.CanRender)
                {
                    Hide();
                    return;
                }

                // Respect a caret hidden by the editor or another extension before our takeover.
                if (!ownsNativeVisibility && presentation.NativeCaretHidden) return;

                Rect bounds = presentation.Bounds;
                Rect viewport = presentation.Viewport;
                if (!Valid(bounds) || !Valid(viewport)) { Hide(); return; }
                bounds.Intersect(viewport);
                if (!Valid(bounds)) { Hide(); return; }

                Canvas.SetLeft(visual, bounds.Left);
                Canvas.SetTop(visual, bounds.Top);
                visual.Width = bounds.Width;
                visual.Height = bounds.Height;
                bool moved = bounds != lastBounds;
                lastBounds = bounds;

                if (!attached)
                {
                    visual.Visibility = Visibility.Visible;
                    attached = presentation.Attach(visual, OnRemoved);
                    // Never hide the native caret until a visible replacement was accepted.
                    if (!attached) { visual.Visibility = Visibility.Hidden; return; }
                    resetBlink = true;
                }
                if (!ownsNativeVisibility)
                {
                    // Claim before the setter, since changing visibility may raise view events.
                    ownsNativeVisibility = true;
                    presentation.NativeCaretHidden = true;
                }
                if (resetBlink || moved)
                {
                    visual.Visibility = Visibility.Visible;
                    blink.Restart();
                }
            }
            catch (Exception error)
            {
                failed = true;
                requested = false;
                CleanupSafely();
                Trace.TraceError("SmartInput caret display stopped: " + error.GetType().Name);
            }
            finally { updating = false; }
        }

        private void OnBlink(object sender, EventArgs e)
        {
            Refresh(false);
            if (attached && requested && !failed && !disposed)
                visual.Visibility = visual.Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible;
        }

        private void OnRemoved()
        {
            attached = false;
            if (updating || disposed) return;
            // If VS removes our element unexpectedly, immediately fall back to its native caret.
            // The next SetRed/layout notification may safely reattach the replacement.
            updating = true;
            try { CleanupSafely(); }
            finally { updating = false; }
        }

        private void Hide()
        {
            visual.Visibility = Visibility.Hidden;
            blink.Stop();
            try
            {
                if (ownsNativeVisibility)
                {
                    presentation.NativeCaretHidden = false;
                    ownsNativeVisibility = false;
                }
            }
            finally
            {
                if (attached)
                {
                    attached = false;
                    presentation.Detach();
                }
                lastBounds = Rect.Empty;
            }
        }

        private void CleanupSafely()
        {
            try { Hide(); }
            catch (Exception error) { Trace.TraceError("SmartInput caret cleanup: " + error.GetType().Name); }
        }

        private static bool Valid(Rect value) => !value.IsEmpty && value.Width > 0 && value.Height > 0
            && Finite(value.X) && Finite(value.Y) && Finite(value.Width) && Finite(value.Height);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            requested = false;
            presentation.Changed -= OnChanged;
            presentation.Closed -= OnClosed;
            blink.Tick -= OnBlink;
            updating = true;
            try { CleanupSafely(); }
            finally
            {
                try { blink.Dispose(); }
                finally { presentation.Dispose(); }
            }
        }
    }
}
