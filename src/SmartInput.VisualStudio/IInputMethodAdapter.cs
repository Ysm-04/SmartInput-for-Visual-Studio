using System;
using SmartInput.Core;

namespace SmartInput.VisualStudio
{
    internal interface IInputMethodAdapter : IDisposable
    {
        string DisplayName { get; }
        bool IsForeground { get; }
        bool IsActive { get; }
        InputMode Read();
        bool TrySet(InputMode desired);
        bool TryRecover(InputMode desired);
    }
}
