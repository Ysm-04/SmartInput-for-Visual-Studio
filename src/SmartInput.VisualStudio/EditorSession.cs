using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using SmartInput.Core;
using InputMode = SmartInput.Core.InputMode;

namespace SmartInput.VisualStudio
{
    internal sealed class EditorSession : IDisposable
    {
        private const int MaximumDocumentLength = 2 * 1024 * 1024;
        private const int SwitchConfirmationWindowMilliseconds = 600;
        private const int MaximumSwitchAttempts = 2;
        private readonly IWpfTextView view;
        private readonly IClassifier classifier;
        private readonly CaretColorController caret;
        private readonly InputMethodAdapterFactory inputMethods;
        private IInputMethodAdapter ime;
        private readonly SwitchPolicy policy = new SwitchPolicy();
        private readonly DispatcherTimer debounce, poll;
        private readonly InputMethod input;
        private CancellationTokenSource analysisCancellation;
        private Task analysisTask = Task.CompletedTask;
        private ITextSnapshot analyzedSnapshot, analyzingSnapshot;
        private ContextMap map;
        private HwndSource source;
        private bool disposed, faulted, wpfComposition, nativeComposition, wasFocused;
        private InputMode lastActual = InputMode.Unknown;
        private InputMode? pending;
        private DateTime pendingDeadline;
        private int pendingAttempts;
        private string pendingContext;
        private string blockedContext;

        public string Status { get; private set; } = "等待编辑器焦点";
        public event EventHandler StatusChanged;

        public EditorSession(IWpfTextView view, IClassifier classifier)
        {
            this.view = view;
            this.classifier = classifier;
            caret = new CaretColorController(new WpfCaretPresentation(view), new CaretBlinkTimer(view.VisualElement.Dispatcher));
            inputMethods = new InputMethodAdapterFactory();
            input = InputMethod.Current;
            debounce = new DispatcherTimer(DispatcherPriority.Background, view.VisualElement.Dispatcher) { Interval = TimeSpan.FromMilliseconds(45) };
            poll = new DispatcherTimer(DispatcherPriority.Background, view.VisualElement.Dispatcher) { Interval = TimeSpan.FromMilliseconds(150) };
            debounce.Tick += OnDebounce;
            poll.Tick += OnPoll;
            view.Caret.PositionChanged += OnCaretMoved;
            view.TextBuffer.Changed += OnTextChanged;
            view.TextBuffer.ContentTypeChanged += OnContentTypeChanged;
            view.GotAggregateFocus += OnGotFocus;
            view.LostAggregateFocus += OnLostFocus;
            view.Closed += OnClosed;
            input.StateChanged += OnInputStateChanged;
            SessionSettings.Changed += OnSettingsChanged;
            TextCompositionManager.AddPreviewTextInputStartHandler(view.VisualElement, OnCompositionStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(view.VisualElement, OnCompositionUpdate);
            TextCompositionManager.AddPreviewTextInputHandler(view.VisualElement, OnCompositionComplete);
            caret.SetColor(SessionSettings.Current.ManualCaretWpfColor);
            if (view.HasAggregateFocus) OnGotFocus(this, EventArgs.Empty);
        }

        private bool Focused => !disposed && !view.IsClosed && view.VisualElement.IsKeyboardFocusWithin && inputMethods.IsForeground;
        private bool Composing => wpfComposition || nativeComposition;
        private bool Supported => view.TextBuffer.ContentType.IsOfType("C/C++") || view.TextBuffer.ContentType.IsOfType("CSharp");

        private void OnCaretMoved(object sender, CaretPositionChangedEventArgs e) { Schedule(); }
        private void OnTextChanged(object sender, TextContentChangedEventArgs e)
        {
            Guard(() =>
            {
                analysisCancellation?.Cancel();
                analyzedSnapshot = null;
                Schedule();
            });
        }
        private void OnContentTypeChanged(object sender, ContentTypeChangedEventArgs e)
        {
            Guard(() =>
            {
                analysisCancellation?.Cancel();
                analyzedSnapshot = null;
                policy.Reset();
                Schedule();
            });
        }
        private void OnGotFocus(object sender, EventArgs e)
        {
            if (faulted) return;
            Guard(() =>
            {
                policy.Reset();
                ClearPending();
                blockedContext = null;
                lastActual = InputMode.Unknown;
                wasFocused = false;
                ime = null;
                AttachWindowHook();
                poll.Start();
                Schedule();
            });
        }
        private void OnLostFocus(object sender, EventArgs e)
        {
            Guard(() =>
            {
                poll.Stop(); debounce.Stop();
                wpfComposition = nativeComposition = false;
                wasFocused = false;
                ime = null;
                ClearPending();
                policy.Reset();
                caret.SetRed(false);
                SetStatus(InactiveStatus());
            });
        }
        private void OnInputStateChanged(object sender, InputMethodStateChangedEventArgs e)
        {
            if (e.IsImeStateChanged || e.IsImeConversionModeChanged) Schedule();
        }
        private void OnSettingsChanged(object sender, EventArgs e)
        {
            Guard(() =>
            {
                var settings = SessionSettings.Current;
                policy.Reset(); ClearPending(); blockedContext = null;
                caret.SetColor(settings.ManualCaretWpfColor);
                caret.SetRed(false);
                SetStatus(InactiveStatus(settings));
                Schedule();
            });
        }
        private void OnCompositionStart(object sender, TextCompositionEventArgs e) { wpfComposition = true; }
        private void OnCompositionUpdate(object sender, TextCompositionEventArgs e) { wpfComposition = true; }
        private void OnCompositionComplete(object sender, TextCompositionEventArgs e) { wpfComposition = false; Schedule(); }
        private void OnClosed(object sender, EventArgs e)
        {
            try { Dispose(); }
            catch (Exception ex) { Trace.TraceError("SmartInput cleanup: " + ex.GetType().Name); }
        }
        private void OnDebounce(object sender, EventArgs e) { debounce.Stop(); Guard(Evaluate); }
        private void OnPoll(object sender, EventArgs e) { Guard(Evaluate); }

        private void Schedule()
        {
            if (disposed || faulted || !view.HasAggregateFocus) return;
            debounce.Stop(); debounce.Start();
        }

        private void Evaluate()
        {
            if (disposed || faulted) return;
            var settings = SessionSettings.Current;
            caret.SetColor(settings.ManualCaretWpfColor);
            policy.Paused = settings.Paused || !settings.AutomaticSwitchingEnabled;
            if (!settings.AutomaticSwitchingEnabled)
            {
                caret.SetRed(false);
                SetStatus("自动切换已关闭");
                return;
            }
            if (settings.Paused || !Supported)
            {
                caret.SetRed(false);
                SetStatus(settings.Paused ? "已暂停" : "当前文件类型未接管");
                return;
            }
            if (!Focused)
            {
                wasFocused = false;
                caret.SetRed(false);
                return;
            }
            AttachWindowHook();
            var activeIme = inputMethods.GetActive();
            if (!object.ReferenceEquals(ime, activeIme))
            {
                ime = activeIme;
                policy.Reset();
                ClearPending();
                blockedContext = null;
                lastActual = InputMode.Unknown;
            }
            if (ime == null)
            {
                lastActual = InputMode.Unknown;
                pending = null;
                policy.Reset();
                caret.SetRed(false);
                SetStatus("未检测到可控制的微软拼音或搜狗拼音");
                return;
            }
            if (!wasFocused)
            {
                policy.Reset(); pending = null; blockedContext = null;
                lastActual = InputMode.Unknown;
                wasFocused = true;
            }
            if (Composing)
            {
                SetStatus("组词中，暂不切换");
                return;
            }
            InputMode actual = ime.Read();
            if (actual == InputMode.Unknown)
            {
                // No guessing and no switch to another keyboard when a user selects a different IME.
                lastActual = InputMode.Unknown; ClearPending(); policy.Reset();
                caret.SetRed(false);
                SetStatus("未检测到可控制的微软拼音或搜狗拼音");
                return;
            }
            if (pending.HasValue)
            {
                string currentPendingContext = CurrentContextKey();
                if (pendingContext != null && currentPendingContext != "pending"
                    && currentPendingContext != pendingContext)
                {
                    ClearPending();
                    blockedContext = null;
                }
                else if (actual == pending.Value)
                {
                    ClearPending();
                    lastActual = actual;
                }
                else if (DateTime.UtcNow < pendingDeadline)
                {
                    SetStatus("等待输入法确认");
                    return;
                }
                else if (pendingAttempts < MaximumSwitchAttempts)
                {
                    InputMode retryMode = pending.Value;
                    pendingAttempts++;
                    pendingDeadline = DateTime.UtcNow.AddMilliseconds(SwitchConfirmationWindowMilliseconds);
                    if (!ime.TryRecover(retryMode))
                    {
                        ClearPending();
                        blockedContext = pendingContext ?? currentPendingContext;
                        SetStatus("输入法拒绝切换；保留现状");
                        return;
                    }
                    SetStatus("等待输入法确认");
                    return;
                }
                else
                {
                    string failedContext = pendingContext ?? currentPendingContext;
                    ClearPending();
                    blockedContext = failedContext;
                    lastActual = actual;
                    caret.SetRed(false);
                    SetStatus("切换未确认；移动到其他区域后重试");
                    return;
                }
            }
            else if (lastActual != InputMode.Unknown && actual != lastActual)
            {
                policy.ObserveManualChange(actual);
                blockedContext = null;
            }
            lastActual = actual;

            // Selections can span conflicting contexts. Do not force a mode until there is one insertion point.
            if (!view.Selection.IsEmpty || view.GetMultiSelectionBroker()?.HasMultipleSelections == true)
            {
                caret.SetRed(false); SetStatus("选区中，暂不切换"); return;
            }
            var snapshot = view.TextSnapshot;
            if (snapshot != analyzedSnapshot)
            {
                if (snapshot.Length > MaximumDocumentLength)
                {
                    caret.SetRed(false); SetStatus("文件超过 2M 字符，已暂停自动识别"); return;
                }
                if (analyzingSnapshot != snapshot && analysisTask.IsCompleted) analysisTask = AnalyzeAsync(snapshot);
                SetStatus("分析当前位置");
                return;
            }
            int position = view.Caret.Position.BufferPosition.Position;
            var context = map.At(position);
            if (IsExcludedCode(snapshot, position))
                context = new InputContext(ContextKind.Code, context.Start, context.End, InputMode.English);
            policy.SetContext(context);
            if (blockedContext != null && blockedContext != context.Key) blockedContext = null;
            caret.SetRed(policy.IsRed(actual));
            if (blockedContext == context.Key)
            {
                SetStatus("切换未确认；移动到其他区域后重试");
                return;
            }
            InputMode? requested = policy.Request(actual, Composing, Focused);
            if (requested.HasValue)
            {
                pending = requested;
                pendingAttempts = 1;
                pendingContext = context.Key;
                pendingDeadline = DateTime.UtcNow.AddMilliseconds(SwitchConfirmationWindowMilliseconds);
                if (!ime.TrySet(requested.Value))
                {
                    ClearPending(); blockedContext = context.Key;
                    SetStatus("输入法拒绝切换；保留现状");
                    return;
                }
                Schedule();
                SetStatus("等待输入法确认");
                return;
            }
            SetStatus((policy.HasManualOverride ? "手动覆盖 · " : "自动 · ")
                + (actual == InputMode.Chinese ? "中文" : "英文") + " · " + ContextName(context.Kind));
        }

        private string CurrentContextKey()
        {
            if (map == null || view.TextSnapshot != analyzedSnapshot) return "pending";
            return map.At(view.Caret.Position.BufferPosition.Position).Key;
        }

        private void ClearPending()
        {
            pending = null;
            pendingAttempts = 0;
            pendingContext = null;
        }

        private bool IsExcludedCode(ITextSnapshot snapshot, int position)
        {
            if (snapshot.Length == 0) return false;
            var span = new SnapshotSpan(snapshot, Math.Min(position, snapshot.Length - 1), 1);
            foreach (var classification in classifier.GetClassificationSpans(span))
            {
                string name = classification.ClassificationType.Classification;
                if (name.IndexOf("excluded", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("inactive", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private async Task AnalyzeAsync(ITextSnapshot snapshot)
        {
            analysisCancellation?.Cancel();
            var cancellation = new CancellationTokenSource();
            analysisCancellation = cancellation;
            analyzingSnapshot = snapshot;
            var language = view.TextBuffer.ContentType.IsOfType("CSharp") ? SourceLanguage.CSharp : SourceLanguage.Cpp;
            try
            {
                var result = await Task.Run(() => ContextAnalyzer.Analyze(snapshot.GetText(), language, cancellation.Token));
                if (disposed || cancellation.IsCancellationRequested || view.TextSnapshot != snapshot) return;
                map = result;
                analyzedSnapshot = snapshot;
                Schedule();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Fail(ex); }
            finally
            {
                if (analysisCancellation == cancellation) { analysisCancellation = null; analyzingSnapshot = null; }
                cancellation.Dispose();
            }
        }

        private void AttachWindowHook()
        {
            var current = PresentationSource.FromVisual(view.VisualElement) as HwndSource;
            if (current == source) return;
            if (source != null) source.RemoveHook(WindowMessage);
            source = current;
            if (source != null) source.AddHook(WindowMessage);
        }

        private IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == 0x010D && Focused) nativeComposition = true; // WM_IME_STARTCOMPOSITION
            else if (message == 0x010E)
            {
                nativeComposition = wpfComposition = false; // includes cancellation with Escape
                Schedule();
            }
            return IntPtr.Zero; // Never consume/modify the IME's messages.
        }

        private static string ContextName(ContextKind kind)
        {
            switch (kind)
            {
                case ContextKind.Comment: return "注释";
                case ContextKind.String: return "字符串";
                case ContextKind.Character: return "字符";
                default: return "代码";
            }
        }
        private static string InactiveStatus()
        {
            return InactiveStatus(SessionSettings.Current);
        }
        private static string InactiveStatus(SmartInputSettingsSnapshot settings)
        {
            if (!settings.AutomaticSwitchingEnabled) return "自动切换已关闭";
            return settings.Paused ? "已暂停" : "等待编辑器焦点";
        }
        private void SetStatus(string status)
        {
            if (Status == status) return;
            Status = status; StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        private void Guard(Action action)
        {
            if (disposed) return;
            var dispatcher = view.VisualElement.Dispatcher;
            if (!dispatcher.CheckAccess())
            {
                if (!dispatcher.HasShutdownStarted) _ = dispatcher.BeginInvoke(new Action(() => Guard(action)));
                return;
            }
            try { action(); }
            catch (Exception ex) { Fail(ex); }
        }
        private void Fail(Exception error)
        {
            if (disposed) return;
            faulted = true;
            debounce.Stop(); poll.Stop();
            try { caret.SetRed(false); } catch (Exception) { }
            // No source text, paths, candidate words or keystrokes are recorded.
            Trace.TraceError("SmartInput session stopped: " + error.GetType().Name);
            SetStatus("已安全停用：" + error.GetType().Name + "（重新打开文档可重试）");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            analysisCancellation?.Cancel();
            debounce.Stop(); poll.Stop();
            debounce.Tick -= OnDebounce; poll.Tick -= OnPoll;
            view.Caret.PositionChanged -= OnCaretMoved;
            view.TextBuffer.Changed -= OnTextChanged;
            view.TextBuffer.ContentTypeChanged -= OnContentTypeChanged;
            view.GotAggregateFocus -= OnGotFocus; view.LostAggregateFocus -= OnLostFocus;
            view.Closed -= OnClosed;
            input.StateChanged -= OnInputStateChanged;
            SessionSettings.Changed -= OnSettingsChanged;
            TextCompositionManager.RemovePreviewTextInputStartHandler(view.VisualElement, OnCompositionStart);
            TextCompositionManager.RemovePreviewTextInputUpdateHandler(view.VisualElement, OnCompositionUpdate);
            TextCompositionManager.RemovePreviewTextInputHandler(view.VisualElement, OnCompositionComplete);
            try
            {
                if (source != null) source.RemoveHook(WindowMessage);
            }
            finally
            {
                try { caret.Dispose(); }
                finally
                {
                    try { inputMethods.Dispose(); }
                    finally { (classifier as IDisposable)?.Dispose(); }
                }
            }
        }
    }
}
