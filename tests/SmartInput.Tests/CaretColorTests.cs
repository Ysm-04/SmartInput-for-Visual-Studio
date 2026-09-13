using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SmartInput.VisualStudio;

namespace SmartInput.Tests
{
    // Links the production controller, not a reimplementation. No VS process, input-method writes,
    // actual windows, or desktop automation are used. Geometry/lifetime are driven by a fake view.
    internal static class CaretColorTests
    {
        public static void Run(Action<string, Action> check)
        {
            check("光标：正常状态不改变原生光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(false);
                    Assert(!f.View.NativeCaretHidden && f.View.Visual == null && !f.Timer.Running);
                }
            });
            check("光标：手动状态创建独立红色光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    Assert(f.View.NativeCaretHidden && f.View.Visual.Visibility == Visibility.Visible);
                    Assert(((SolidColorBrush)f.View.Visual.Fill).Color == Colors.Red);
                    Assert(!f.View.Visual.IsHitTestVisible && !f.View.Visual.Focusable);
                }
            });
            check("光标：手动覆盖颜色可配置", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetColor(Colors.Yellow);
                    f.Controller.SetRed(true);
                    Assert(((SolidColorBrush)f.View.Visual.Fill).Color == Colors.Yellow);
                }
            });
            check("光标：离屏 WPF 渲染的像素确实为红色", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    var shape = f.View.Visual;
                    shape.Measure(new Size(shape.Width, shape.Height));
                    shape.Arrange(new Rect(0, 0, shape.Width, shape.Height));
                    var bitmap = new RenderTargetBitmap(2, 20, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(shape);
                    var pixels = new byte[2 * 20 * 4];
                    bitmap.CopyPixels(pixels, 8, 0);
                    int offset = (10 * 2 + 1) * 4;
                    Assert(pixels[offset] == 0 && pixels[offset + 1] == 0 && pixels[offset + 2] == 255 && pixels[offset + 3] == 255);
                }
            });
            check("光标：坐标与尺寸跟随编辑器", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    Assert(Canvas.GetLeft(f.View.Visual) == 10 && Canvas.GetTop(f.View.Visual) == 20);
                    Assert(f.View.Visual.Width == 2 && f.View.Visual.Height == 20);
                }
            });
            check("光标：切回原态移除装饰并恢复原生光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    var visual = f.View.Visual;
                    f.Controller.SetRed(false);
                    Assert(!f.View.NativeCaretHidden && f.View.Visual == null && !f.Timer.Running);
                    Assert(visual.Visibility == Visibility.Hidden);
                }
            });
            check("光标：失焦或选区不可绘制时恢复原生光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.View.CanRender = false;
                    f.View.Notify();
                    Assert(!f.View.NativeCaretHidden && f.View.Visual == null && !f.Timer.Running);
                }
            });
            check("光标：原本已隐藏时不抢夺可见性", () =>
            {
                using (var f = new Fixture())
                {
                    f.View.NativeCaretHidden = true;
                    f.Controller.SetRed(true);
                    f.Controller.SetRed(false);
                    Assert(f.View.NativeCaretHidden && f.View.AttachCount == 0);
                }
            });
            check("光标：光标离开视口时不遮挡其他区域", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.View.Bounds = new Rect(2000, 20, 2, 20);
                    f.View.Notify();
                    Assert(!f.View.NativeCaretHidden && f.View.Visual == null);
                    f.View.Bounds = new Rect(30, 40, 2, 20);
                    f.View.Notify();
                    Assert(f.View.NativeCaretHidden && Canvas.GetLeft(f.View.Visual) == 30);
                }
            });
            check("光标：视口边缘裁剪", () =>
            {
                using (var f = new Fixture())
                {
                    f.View.Bounds = new Rect(99, 90, 2, 20);
                    f.View.Viewport = new Rect(0, 0, 100, 100);
                    f.Controller.SetRed(true);
                    Assert(f.View.Visual.Width == 1 && f.View.Visual.Height == 10);
                }
            });
            check("光标：移动及缩放不重复添加装饰", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.View.Bounds = new Rect(20, 40, 4, 40);
                    f.View.Notify();
                    Assert(f.View.AttachCount == 1 && f.View.Visual.Width == 4 && f.View.Visual.Height == 40);
                    Assert(Canvas.GetTop(f.View.Visual) == 40);
                }
            });
            check("光标：覆盖模式采用编辑器报告的宽度", () =>
            {
                using (var f = new Fixture())
                {
                    f.View.Bounds = new Rect(10, 20, 16, 20);
                    f.Controller.SetRed(true);
                    Assert(f.View.Visual.Width == 16);
                }
            });
            check("光标：轮询不重置闪烁", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.Timer.Fire();
                    Assert(f.View.Visual.Visibility == Visibility.Hidden);
                    f.Controller.SetRed(true);
                    Assert(f.Timer.Restarts == 1 && f.View.Visual.Visibility == Visibility.Hidden);
                    f.Timer.Fire();
                    Assert(f.View.Visual.Visibility == Visibility.Visible && f.View.NativeCaretHidden);
                }
            });
            check("光标：移动后立即显示并重新开始闪烁", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.Timer.Fire();
                    f.View.Bounds = new Rect(12, 20, 2, 20);
                    f.View.Notify();
                    Assert(f.View.Visual.Visibility == Visibility.Visible && f.Timer.Restarts == 2);
                }
            });
            check("光标：装饰添加失败不隐藏原生光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.View.AcceptAttach = false;
                    f.Controller.SetRed(true);
                    Assert(!f.View.NativeCaretHidden && !f.Timer.Running);
                    f.View.AcceptAttach = true;
                    f.Controller.SetRed(true);
                    Assert(f.View.NativeCaretHidden && f.View.Visual != null);
                }
            });
            check("光标：装饰添加异常安全回退", () =>
            {
                using (var f = new Fixture())
                {
                    f.View.ThrowOnAttach = true;
                    f.Controller.SetRed(true);
                    Assert(!f.View.NativeCaretHidden && !f.Timer.Running);
                }
            });
            check("光标：运行中发生异常恢复原生光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.View.ThrowOnBounds = true;
                    f.View.Notify();
                    Assert(!f.View.NativeCaretHidden && f.View.Visual == null && !f.Timer.Running);
                }
            });
            check("光标：移除装饰失败也先恢复原生光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    var visual = f.View.Visual;
                    f.View.ThrowOnDetach = true;
                    f.Controller.SetRed(false);
                    Assert(!f.View.NativeCaretHidden && !f.Timer.Running && visual.Visibility == Visibility.Hidden);
                }
            });
            check("光标：意外移除不会让原生光标一直隐藏", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.View.Detach();
                    Assert(!f.View.NativeCaretHidden && !f.Timer.Running);
                    f.Controller.SetRed(true);
                    Assert(f.View.AttachCount == 2 && f.View.NativeCaretHidden);
                }
            });
            check("光标：可见性事件重入不递归添加", () =>
            {
                using (var f = new Fixture())
                {
                    f.View.NotifyOnVisibility = true;
                    f.Controller.SetRed(true);
                    Assert(f.View.AttachCount == 1 && f.View.NativeCaretHidden);
                    f.Controller.SetRed(false);
                    Assert(!f.View.NativeCaretHidden && f.View.Visual == null);
                }
            });
            check("光标：关闭文档清理事件与计时器", () =>
            {
                using (var f = new Fixture())
                {
                    f.Controller.SetRed(true);
                    f.View.Close();
                    Assert(!f.View.NativeCaretHidden && f.View.Disposed && f.Timer.Disposed);
                    f.View.Notify();
                    f.Timer.Fire();
                    f.Controller.SetRed(true);
                    Assert(f.View.AttachCount == 1 && f.View.Visual == null);
                }
            });
            check("光标：多个视图互不改变颜色和可见性", () =>
            {
                using (var a = new Fixture())
                using (var b = new Fixture())
                {
                    a.Controller.SetRed(true);
                    Assert(!b.View.NativeCaretHidden && b.View.Visual == null);
                    b.Controller.SetRed(true);
                    a.Controller.Dispose();
                    Assert(!a.View.NativeCaretHidden && b.View.NativeCaretHidden && b.View.Visual != null);
                }
            });
            check("光标：空几何不隐藏原生光标", () =>
            {
                using (var f = new Fixture())
                {
                    f.View.Bounds = Rect.Empty;
                    f.Controller.SetRed(true);
                    Assert(!f.View.NativeCaretHidden && f.View.AttachCount == 0);
                }
            });
        }

        private static void Assert(bool value) { if (!value) throw new Exception("caret assertion failed"); }
        private sealed class Fixture : IDisposable
        {
            internal readonly FakePresentation View = new FakePresentation();
            internal readonly FakeTimer Timer = new FakeTimer();
            internal readonly CaretColorController Controller;
            internal Fixture() { Controller = new CaretColorController(View, Timer); }
            public void Dispose() { Controller.Dispose(); }
        }

        private sealed class FakeTimer : ICaretBlinkTimer
        {
            public event EventHandler Tick;
            internal int Restarts;
            internal bool Running, Disposed;
            public void Restart() { Restarts++; Running = true; }
            public void Stop() { Running = false; }
            internal void Fire() { Tick?.Invoke(this, EventArgs.Empty); }
            public void Dispose() { Running = false; Disposed = true; }
        }

        private sealed class FakePresentation : ICaretPresentation
        {
            private Rect bounds = new Rect(10, 20, 2, 20);
            private bool hidden;
            private Action removed;
            public bool CanRender { get; set; } = true;
            public Rect Bounds { get { if (ThrowOnBounds) throw new InvalidOperationException(); return bounds; } set { bounds = value; } }
            public Rect Viewport { get; set; } = new Rect(0, 0, 1000, 1000);
            public bool NativeCaretHidden { get => hidden; set { hidden = value; if (NotifyOnVisibility) Notify(); } }
            public event EventHandler Changed;
            public event EventHandler Closed;
            internal Rectangle Visual;
            internal bool AcceptAttach = true, ThrowOnAttach, ThrowOnBounds, ThrowOnDetach, NotifyOnVisibility, Disposed;
            internal int AttachCount;

            public bool Attach(Rectangle visual, Action callback)
            {
                if (ThrowOnAttach) throw new InvalidOperationException();
                if (!AcceptAttach) return false;
                AttachCount++;
                Visual = visual;
                removed = callback;
                return true;
            }
            public void Detach()
            {
                if (ThrowOnDetach) throw new InvalidOperationException();
                Visual = null;
                var callback = removed; removed = null; callback?.Invoke();
            }
            internal void Notify() { Changed?.Invoke(this, EventArgs.Empty); }
            internal void Close() { Closed?.Invoke(this, EventArgs.Empty); }
            public void Dispose() { Disposed = true; }
        }
    }
}
