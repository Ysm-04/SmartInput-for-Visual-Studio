using System;
using System.Diagnostics;

namespace SmartInput.VisualStudio
{
    internal sealed class InputMethodAdapterFactory : IDisposable
    {
        private readonly int processId = Process.GetCurrentProcess().Id;
        private readonly PinyinInputMethod microsoftPinyin = new PinyinInputMethod();
        private readonly SogouInputMethod sogou = new SogouInputMethod();

        public bool IsForeground
        {
            get { return TsfInputMethodAdapter.IsCurrentProcessForeground(processId); }
        }

        public IInputMethodAdapter GetActive()
        {
            if (microsoftPinyin.IsActive) return microsoftPinyin;
            if (sogou.IsActive) return sogou;
            return null;
        }

        public void Dispose()
        {
            microsoftPinyin.Dispose();
            sogou.Dispose();
        }
    }
}
