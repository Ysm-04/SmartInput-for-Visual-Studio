using System;
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
            InputMode actual = SogouNativeInput.Read();
            return actual == InputMode.Unknown ? base.Read() : actual;
        }

        public override bool TrySet(InputMode desired)
        {
            if (SogouNativeInput.TrySet(desired)) return true;
            return base.TrySet(desired);
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

            public static InputMode Read()
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero) return InputMode.Unknown;
                IntPtr context = ImmGetContext(window);
                if (context == IntPtr.Zero) return InputMode.Unknown;
                try
                {
                    if (!ImmGetOpenStatus(context)) return InputMode.English;
                    uint conversion, sentence;
                    if (!ImmGetConversionStatus(context, out conversion, out sentence))
                        return InputMode.Unknown;
                    return (conversion & ImeConversionModeNative) != 0
                        ? InputMode.Chinese
                        : InputMode.English;
                }
                finally { ImmReleaseContext(window, context); }
            }

            public static bool TrySet(InputMode desired)
            {
                if (desired == InputMode.Unknown) return false;
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero) return false;
                IntPtr context = ImmGetContext(window);
                if (context == IntPtr.Zero) return false;
                try
                {
                    if (desired == InputMode.English)
                        return ImmSetOpenStatus(context, false);

                    uint conversion, sentence;
                    if (!ImmGetConversionStatus(context, out conversion, out sentence)) return false;
                    if (!ImmSetOpenStatus(context, true)) return false;
                    return ImmSetConversionStatus(context, conversion | ImeConversionModeNative, sentence);
                }
                finally { ImmReleaseContext(window, context); }
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
            private static extern short GetAsyncKeyState(ushort virtualKey);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

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
