using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SmartInput.Core;
using InputMode = SmartInput.Core.InputMode;

namespace SmartInput.VisualStudio
{
    internal sealed class SogouInputMethod : TsfInputMethodAdapter
    {
        protected override bool MatchesProfile(InputProcessorProfile profile)
        {
            return SupportedInputMethodProfile.IsSogouPinyin(
                profile.ProfileType, profile.LanguageId, profile.ClassId, profile.ProfileId);
        }

        public override string DisplayName { get { return "搜狗拼音"; } }

        public override InputMode Read()
        {
            // In a WPF editor the TSF channel (WPF InputMethod) is the one the editor's text
            // store actually uses; the IMM32 compatibility contexts can report a write back as
            // accepted while Sogou keeps its old mode. Prefer the TSF value and fall back to the
            // IMM32 contexts only when the TSF channel cannot answer.
            InputMode actual = base.Read();
            if (actual != InputMode.Unknown) return actual;
            return SogouNativeInput.Read();
        }

        public override bool TrySet(InputMode desired)
        {
            // Write through both channels: the TSF/WPF property path (what the WPF editor text
            // store consumes) and the IMM32 contexts of the focused editor window / shell frame.
            // Either one may be the one Sogou honours in a given host, and both writes are
            // idempotent.
            bool ok = base.TrySet(desired);
            ok |= SogouNativeInput.TrySet(desired);
            return ok;
        }

        public override bool TryRecover(InputMode desired)
        {
            // The normal TSF/IMM32 setters have already had one confirmation window.
            // Sogou's configured Shift shortcut is the last-resort transition; send it
            // only once and let the session read the resulting state back.
            return SogouNativeInput.SendShiftTap();
        }

        private static class SogouNativeInput
        {
            private const uint ImeConversionModeNative = 0x0001;
            private const uint InputKeyboard = 1;
            private const uint KeyEventKeyUp = 0x0002;
            private const ushort VirtualKeyShift = 0x0010;
            private const int MaximumAncestorDepth = 3;

            public static InputMode Read()
            {
                foreach (IntPtr window in CandidateWindows())
                {
                    IntPtr context = ImmGetContext(window);
                    if (context == IntPtr.Zero) continue;
                    try
                    {
                        if (!ImmGetOpenStatus(context)) return InputMode.English;
                        uint conversion, sentence;
                        if (!ImmGetConversionStatus(context, out conversion, out sentence)) continue;
                        return (conversion & ImeConversionModeNative) != 0
                            ? InputMode.Chinese
                            : InputMode.English;
                    }
                    finally { ImmReleaseContext(window, context); }
                }
                return InputMode.Unknown;
            }

            // The editor's input context is not guaranteed to live on exactly one HWND: a WPF
            // editor hosts the focused child HwndSource, the shell frame is a different top-level
            // window, and Sogou has been observed to honour only some of them. Writing the desired
            // state to every candidate window's IMM32 context (all belonging to this process, so
            // ImmGetContext works) makes the switch independent of which window Sogou listens to.
            // The writes are idempotent; the editor session still confirms the result by reading
            // the state back.
            public static bool TrySet(InputMode desired)
            {
                if (desired == InputMode.Unknown) return false;
                bool written = false;
                foreach (IntPtr window in CandidateWindows())
                {
                    IntPtr context = ImmGetContext(window);
                    if (context == IntPtr.Zero) continue;
                    try { written |= SetOnContext(context, desired); }
                    finally { ImmReleaseContext(window, context); }
                }
                return written;
            }

            private static bool SetOnContext(IntPtr context, InputMode desired)
            {
                if (desired == InputMode.English) return ImmSetOpenStatus(context, false);

                uint conversion, sentence;
                if (!ImmGetConversionStatus(context, out conversion, out sentence)) return false;
                if (!ImmSetOpenStatus(context, true)) return false;
                return ImmSetConversionStatus(context, conversion | ImeConversionModeNative, sentence);
            }

            public static bool SendShiftTap()
            {
                if ((GetAsyncKeyState(VirtualKeyShift) & 0x8000) != 0) return false;
                var inputs = new[]
                {
                    KeyboardInput(VirtualKeyShift, 0),
                    KeyboardInput(VirtualKeyShift, KeyEventKeyUp)
                };
                return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) == inputs.Length;
            }

            // Candidate windows in preference order: the focused window of this UI thread (the
            // editor's HwndSource), its parents, then the top-level foreground window (the shell
            // frame) and its parents. Duplicates are removed. All of them belong to this process,
            // which IMM32 requires for ImmGetContext.
            private static List<IntPtr> CandidateWindows()
            {
                var result = new List<IntPtr>(8);
                var seen = new HashSet<IntPtr>();

                IntPtr focus = GetFocus();
                if (focus == IntPtr.Zero)
                {
                    IntPtr foreground = GetForegroundWindow();
                    if (foreground != IntPtr.Zero)
                    {
                        uint threadId = GetWindowThreadProcessId(foreground, out _);
                        if (threadId != 0)
                        {
                            var info = new GUITHREADINFO();
                            info.Size = Marshal.SizeOf(typeof(GUITHREADINFO));
                            if (GetGUIThreadInfo(threadId, ref info)) focus = info.Focus;
                        }
                    }
                }
                AddWithAncestors(focus, result, seen);

                IntPtr frame = GetForegroundWindow();
                AddWithAncestors(frame, result, seen);
                return result;
            }

            private static void AddWithAncestors(IntPtr window, List<IntPtr> result, HashSet<IntPtr> seen)
            {
                for (int depth = 0; depth <= MaximumAncestorDepth && window != IntPtr.Zero; depth++)
                {
                    if (seen.Add(window)) result.Add(window);
                    window = GetParent(window);
                }
            }

            private static INPUT KeyboardInput(ushort key, uint flags)
            {
                return new INPUT
                {
                    Type = InputKeyboard,
                    Keyboard = new KEYBDINPUT { VirtualKey = key, Flags = flags }
                };
            }

            [DllImport("imm32.dll")]
            private static extern IntPtr ImmGetContext(IntPtr window);

            [DllImport("imm32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ImmReleaseContext(IntPtr window, IntPtr context);

            [DllImport("imm32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ImmGetOpenStatus(IntPtr context);

            [DllImport("imm32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ImmSetOpenStatus(IntPtr context, bool open);

            [DllImport("imm32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ImmGetConversionStatus(IntPtr context, out uint conversion, out uint sentence);

            [DllImport("imm32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool ImmSetConversionStatus(IntPtr context, uint conversion, uint sentence);

            [DllImport("user32.dll")]
            private static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            private static extern IntPtr GetFocus();

            [DllImport("user32.dll")]
            private static extern IntPtr GetParent(IntPtr window);

            [DllImport("user32.dll")]
            private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

            [DllImport("user32.dll")]
            private static extern short GetAsyncKeyState(ushort virtualKey);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

            [StructLayout(LayoutKind.Sequential)]
            private struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct GUITHREADINFO
            {
                public int Size;
                public int Flags;
                public IntPtr ActiveWindow;
                public IntPtr Focus;
                public IntPtr Capture;
                public IntPtr MenuOwner;
                public IntPtr MoveSize;
                public IntPtr Caret;
                public RECT CaretRect;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct INPUT
            {
                public uint Type;
                public KEYBDINPUT Keyboard;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct KEYBDINPUT
            {
                public ushort VirtualKey;
                public ushort ScanCode;
                public uint Flags;
                public uint Time;
                public IntPtr ExtraInfo;
            }
        }
    }
}