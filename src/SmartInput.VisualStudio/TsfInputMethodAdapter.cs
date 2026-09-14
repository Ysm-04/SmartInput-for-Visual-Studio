using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using SmartInput.Core;
using InputMode = SmartInput.Core.InputMode;

namespace SmartInput.VisualStudio
{
    /// <summary>UI-thread only. Reads and changes only the active profile's internal Chinese/English state.</summary>
    internal abstract class TsfInputMethodAdapter : IInputMethodAdapter
    {
        private static readonly Guid ProfileManagerClass = new Guid("33C53A50-F456-4884-B049-85FD643ECFED");
        private static readonly Guid KeyboardCategory = new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");
        private readonly int processId = Process.GetCurrentProcess().Id;
        private readonly InputMethod input = InputMethod.Current;
        private ITfInputProcessorProfileMgr profiles;

        protected abstract bool MatchesProfile(InputProcessorProfile profile);
        public abstract string DisplayName { get; }

        protected TsfInputMethodAdapter()
        {
            try
            {
                profiles = (ITfInputProcessorProfileMgr)Activator.CreateInstance(
                    Type.GetTypeFromCLSID(ProfileManagerClass));
            }
            catch (COMException) { }
            catch (InvalidOperationException) { }
        }

        public bool IsForeground
        {
            get { return IsCurrentProcessForeground(processId); }
        }

        public bool IsActive
        {
            get
            {
                if (profiles == null) return false;
                try
                {
                    var category = KeyboardCategory;
                    InputProcessorProfile profile;
                    if (profiles.GetActiveProfile(ref category, out profile) != 0) return false;
                    return MatchesProfile(profile);
                }
                catch (COMException) { return false; }
                catch (InvalidOperationException) { return false; }
            }
        }

        public virtual InputMode Read()
        {
            if (!IsForeground || !IsActive) return InputMode.Unknown;
            try
            {
                if (input.ImeState == InputMethodState.Off) return InputMode.English;
                if (input.ImeState != InputMethodState.On) return InputMode.Unknown;
                var conversion = input.ImeConversionMode;
                if (conversion == ImeConversionModeValues.DoNotCare) return InputMode.Unknown;
                return (conversion & ImeConversionModeValues.Native) != 0
                    ? InputMode.Chinese
                    : InputMode.English;
            }
            catch (COMException) { return InputMode.Unknown; }
            catch (InvalidOperationException) { return InputMode.Unknown; }
        }

        public virtual bool TrySet(InputMode desired)
        {
            if (!IsForeground || !IsActive || desired == InputMode.Unknown) return false;
            try
            {
                if (desired == InputMode.English)
                {
                    input.ImeState = InputMethodState.Off;
                }
                else
                {
                    var mode = input.ImeConversionMode;
                    if (mode == ImeConversionModeValues.DoNotCare) return false;
                    // Some third-party IMEs ignore conversion-mode writes while their TSF state is Off.
                    // Turn the active profile on first, then preserve its other flags while requesting
                    // native Chinese mode. The session still confirms the result asynchronously.
                    input.ImeState = InputMethodState.On;
                    input.ImeConversionMode = mode | ImeConversionModeValues.Native;
                }
                // A successful setter is not confirmation. The session reads back asynchronously.
                return true;
            }
            catch (COMException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        public virtual bool TryRecover(InputMode desired)
        {
            return TrySet(desired);
        }

        internal static bool IsCurrentProcessForeground(int processId)
        {
            uint owner;
            GetWindowThreadProcessId(GetForegroundWindow(), out owner);
            return owner == processId;
        }

        internal static bool IsCurrentProcessForeground()
        {
            return IsCurrentProcessForeground(Process.GetCurrentProcess().Id);
        }

        public void Dispose()
        {
            if (profiles != null)
            {
                Marshal.ReleaseComObject(profiles);
                profiles = null;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        // Layout and vtable order follow Windows SDK msctf.h. Only GetActiveProfile is invoked.
        [StructLayout(LayoutKind.Sequential)]
        protected struct InputProcessorProfile
        {
            public uint ProfileType;
            public ushort LanguageId;
            public Guid ClassId;
            public Guid ProfileId;
            public Guid CategoryId;
            public IntPtr SubstituteKeyboard;
            public uint Capabilities;
            public IntPtr Keyboard;
            public uint Flags;
        }

        [ComImport, Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITfInputProcessorProfileMgr
        {
            [PreserveSig] int ActivateProfile(uint type, ushort language, ref Guid clsid, ref Guid profile, IntPtr keyboard, uint flags);
            [PreserveSig] int DeactivateProfile(uint type, ushort language, ref Guid clsid, ref Guid profile, IntPtr keyboard, uint flags);
            [PreserveSig] int GetProfile(uint type, ushort language, ref Guid clsid, ref Guid profile, IntPtr keyboard, out InputProcessorProfile result);
            [PreserveSig] int EnumProfiles(ushort language, out IntPtr enumerator);
            [PreserveSig] int ReleaseInputProcessor(ref Guid clsid, uint flags);
            [PreserveSig] int RegisterProfile(ref Guid clsid, ushort language, ref Guid profile, IntPtr description, uint descriptionLength,
                IntPtr iconFile, uint iconFileLength, uint iconIndex, IntPtr substituteKeyboard, uint preferredLayout,
                [MarshalAs(UnmanagedType.Bool)] bool enabled, uint flags);
            [PreserveSig] int UnregisterProfile(ref Guid clsid, ushort language, ref Guid profile, uint flags);
            [PreserveSig] int GetActiveProfile(ref Guid category, out InputProcessorProfile result);
        }
    }
}
