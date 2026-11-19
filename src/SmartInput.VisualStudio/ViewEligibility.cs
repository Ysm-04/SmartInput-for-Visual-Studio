using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace SmartInput.VisualStudio
{
    /// <summary>
    /// Smart Input only belongs to real editor documents. The debugger expression surfaces
    /// (Watch / Immediate / QuickWatch) are WPF text views that reuse the C# (or SQL) content
    /// type, but their buffers are ephemeral tool-window buffers. Attaching the bottom status
    /// margin to them painted the "Smart Input" button over their input boxes (0.3.4 fix for
    /// the Watch / Immediate / QuickWatch overlay). A text document property exists only on
    /// buffers created for real editor documents, so it distinguishes the two reliably without
    /// depending on internal debugger roles.
    /// </summary>
    internal static class ViewEligibility
    {
        internal static bool IsDocumentEditor(ITextView view)
        {
            if (view == null || view.IsClosed) return false;
            if (!view.Roles.Contains(PredefinedTextViewRoles.Document)) return false;
            return HasTextDocument(view.TextBuffer);
        }

        internal static bool HasTextDocument(ITextBuffer buffer)
        {
            if (buffer == null) return false;
            ITextDocument document;
            return buffer.Properties.TryGetProperty(typeof(ITextDocument), out document) && document != null;
        }
    }
}
