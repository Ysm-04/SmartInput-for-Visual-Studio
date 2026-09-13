using System;
using System.Collections.Generic;
using System.Threading;

namespace SmartInput.Core
{
    public enum SourceLanguage { Cpp, CSharp }
    public enum InputMode { Unknown, English, Chinese }
    public enum ContextKind { Code, Comment, String, Character }

    public sealed class InputContext
    {
        public ContextKind Kind { get; }
        public int Start { get; }
        public int End { get; }
        public InputMode Recommended { get; }
        public string Key => Kind + ":" + Start;

        public InputContext(ContextKind kind, int start, int end, InputMode recommended)
        {
            Kind = kind;
            Start = start;
            End = end;
            Recommended = recommended;
        }
    }

    /// <summary>Offsets are insertion positions: End includes the position BEFORE a closing delimiter.</summary>
    public sealed class ContextMap
    {
        private readonly List<InputContext> regions;
        private readonly int length;

        internal ContextMap(List<InputContext> regions, int length)
        {
            regions.Sort((a, b) => a.Start.CompareTo(b.Start));
            this.regions = regions;
            this.length = length;
        }

        public InputContext At(int position)
        {
            if (position < 0 || position > length) throw new ArgumentOutOfRangeException(nameof(position));
            int lo = 0, hi = regions.Count - 1, previous = -1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (regions[mid].Start <= position) { previous = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            if (previous >= 0 && position <= regions[previous].End) return regions[previous];
            int start = previous < 0 ? 0 : regions[previous].End + 1;
            int end = previous + 1 < regions.Count ? regions[previous + 1].Start - 1 : length;
            return new InputContext(ContextKind.Code, start, end, InputMode.English);
        }
    }

    /// <summary>
    /// Small, dependency-free lexical boundary scanner, not a compiler. Run once per immutable snapshot,
    /// off the UI thread. VS classifications additionally veto excluded/inactive code in the adapter.
    /// </summary>
    public sealed class ContextAnalyzer
    {
        private readonly string text;
        private readonly SourceLanguage language;
        private readonly CancellationToken cancellation;
        private readonly List<InputContext> regions = new List<InputContext>();

        private ContextAnalyzer(string text, SourceLanguage language, CancellationToken cancellation)
        {
            this.text = text ?? throw new ArgumentNullException(nameof(text));
            this.language = language;
            this.cancellation = cancellation;
        }

        public static ContextMap Analyze(string text, SourceLanguage language, CancellationToken cancellation = default(CancellationToken))
        {
            var scanner = new ContextAnalyzer(text, language, cancellation);
            int index = 0;
            scanner.ScanCode(ref index, 0, 0);
            cancellation.ThrowIfCancellationRequested();
            return new ContextMap(scanner.regions, text.Length);
        }

        private void ScanCode(ref int i, int closingBraces, int depth)
        {
            if (depth > 64) throw new InvalidOperationException("Interpolation nesting exceeds the supported limit.");
            int braceDepth = 0;
            while (i < text.Length)
            {
                CheckCancellation(i);
                if (closingBraces > 0 && text[i] == '}' && braceDepth == 0 && Run(i, '}') >= closingBraces)
                {
                    i += closingBraces;
                    return;
                }
                if (Starts(i, "//"))
                {
                    int start = i + 2;
                    i = start;
                    while (i < text.Length)
                    {
                        CheckCancellation(i);
                        if (text[i] == '\r' || text[i] == '\n')
                        {
                            // C/C++ translation phase line splicing keeps // comments alive.
                            if (language == SourceLanguage.Cpp && i > start && text[i - 1] == '\\')
                            {
                                if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                                i++;
                                continue;
                            }
                            break;
                        }
                        i++;
                    }
                    Add(ContextKind.Comment, start, i);
                }
                else if (Starts(i, "/*"))
                {
                    int start = i + 2;
                    i = start;
                    while (i < text.Length && !Starts(i, "*/")) { CheckCancellation(i); i++; }
                    Add(ContextKind.Comment, start, i);
                    if (i < text.Length) i += 2;
                }
                else if (language == SourceLanguage.Cpp && Starts(i, "R\"") && TryCppRaw(ref i)) { }
                else if (language == SourceLanguage.CSharp && TryCSharpString(ref i, depth)) { }
                else if (text[i] == '"') ScanQuoted(ref i, false, 0, 1, depth);
                else if (text[i] == '\'' && !IsDigitSeparator(i)) ScanCharacter(ref i);
                else
                {
                    if (closingBraces > 0)
                    {
                        if (text[i] == '{') braceDepth++;
                        else if (text[i] == '}' && braceDepth > 0) braceDepth--;
                    }
                    i++;
                }
            }
        }

        private bool TryCSharpString(ref int i, int depth)
        {
            int p = i, dollars = 0;
            bool verbatim = false;
            if (p < text.Length && text[p] == '@') { verbatim = true; p++; }
            while (p < text.Length && text[p] == '$') { dollars++; p++; }
            if (!verbatim && p < text.Length && text[p] == '@') { verbatim = true; p++; }
            if (p >= text.Length || text[p] != '"') return false;
            int quotes = Run(p, '"');
            int delimiter = !verbatim && quotes >= 3 ? quotes : 1;
            // Six consecutive quotes are the common empty raw literal """""".
            if (!verbatim && quotes >= 6 && quotes % 2 == 0) delimiter = quotes / 2;
            i = p;
            ScanQuoted(ref i, verbatim, dollars, delimiter, depth);
            return true;
        }

        private void ScanQuoted(ref int i, bool verbatim, int dollars, int quotes, int depth)
        {
            bool raw = quotes >= 3;
            i += quotes;
            int start = i;
            while (i < text.Length)
            {
                CheckCancellation(i);
                if (text[i] == '"' && (!raw || Run(i, '"') >= quotes))
                {
                    if (verbatim && Starts(i, "\"\"")) { i += 2; continue; }
                    if (raw) i += Run(i, '"') - quotes;
                    Add(ContextKind.String, start, i);
                    i += quotes;
                    return;
                }
                if (!raw && !verbatim && (text[i] == '\r' || text[i] == '\n')) break;
                if (!raw && !verbatim && text[i] == '\\')
                {
                    i++;
                    if (i < text.Length)
                    {
                        if (language == SourceLanguage.Cpp && text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                        i++;
                    }
                    continue;
                }
                if (dollars > 0 && text[i] == '{')
                {
                    int run = Run(i, '{');
                    if (!raw && run >= 2) { i += 2; continue; }
                    int delimiter = raw ? dollars : 1;
                    if (run >= delimiter)
                    {
                        // Extra leading braces in a raw interpolation are literal text.
                        i += run - delimiter;
                        Add(ContextKind.String, start, i);
                        i += delimiter;
                        ScanCode(ref i, delimiter, depth + 1);
                        start = i;
                        continue;
                    }
                }
                i++;
            }
            Add(ContextKind.String, start, i);
        }

        private bool TryCppRaw(ref int i)
        {
            int delimiterStart = i + 2, open = delimiterStart;
            while (open < text.Length && open - delimiterStart <= 16 && text[open] != '(')
            {
                char c = text[open];
                if (char.IsWhiteSpace(c) || c == ')' || c == '\\') return false;
                open++;
            }
            if (open >= text.Length || text[open] != '(' || open - delimiterStart > 16) return false;
            string endToken = ")" + text.Substring(delimiterStart, open - delimiterStart) + "\"";
            int start = open + 1;
            i = start;
            while (i < text.Length && !Starts(i, endToken)) { CheckCancellation(i); i++; }
            Add(ContextKind.String, start, i);
            if (i < text.Length) i += endToken.Length;
            return true;
        }

        private void ScanCharacter(ref int i)
        {
            int start = ++i;
            while (i < text.Length && text[i] != '\'' && text[i] != '\r' && text[i] != '\n')
            {
                CheckCancellation(i);
                if (text[i] == '\\' && i + 1 < text.Length) i++;
                i++;
            }
            Add(ContextKind.Character, start, i);
            if (i < text.Length && text[i] == '\'') i++;
        }

        private bool IsDigitSeparator(int i)
        {
            if (language != SourceLanguage.Cpp || i == 0 || i + 1 >= text.Length || !char.IsLetterOrDigit(text[i + 1])) return false;
            int start = i - 1;
            while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '\'' || text[start] == '.')) start--;
            return start + 1 < i && char.IsDigit(text[start + 1]);
        }

        private void Add(ContextKind kind, int start, int end)
        {
            bool chinese = kind == ContextKind.Comment;
            if (kind == ContextKind.String)
                for (int p = start; p < end && !chinese; p++)
                {
                    CheckCancellation(p);
                    int code = char.IsHighSurrogate(text[p]) && p + 1 < end && char.IsLowSurrogate(text[p + 1])
                        ? char.ConvertToUtf32(text[p], text[p + 1]) : text[p];
                    chinese = (code >= 0x3400 && code <= 0x4DBF) || (code >= 0x4E00 && code <= 0x9FFF)
                        || (code >= 0xF900 && code <= 0xFAFF) || (code >= 0x20000 && code <= 0x323AF);
                }
            regions.Add(new InputContext(kind, start, end, chinese ? InputMode.Chinese : InputMode.English));
        }

        private bool Starts(int i, string value) => i + value.Length <= text.Length && string.CompareOrdinal(text, i, value, 0, value.Length) == 0;
        private int Run(int i, char value) { int start = i; while (i < text.Length && text[i] == value) i++; return i - start; }
        private void CheckCancellation(int position) { if ((position & 1023) == 0) cancellation.ThrowIfCancellationRequested(); }
    }
}
