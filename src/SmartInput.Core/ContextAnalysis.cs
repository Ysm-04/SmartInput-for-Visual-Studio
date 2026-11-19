using System;
using System.Collections.Generic;
using System.Threading;

namespace SmartInput.Core
{
    public enum SourceLanguage { Cpp, CSharp, Sql, Xaml }
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

        // QUOTED_IDENTIFIER defaults to ON in SQL Server / SSMS. It is a connection/batch setting,
        // so the lexer approximates it by honoring the last "SET QUOTED_IDENTIFIER ON|OFF" seen in
        // the text. Runtime / in-procedure settings that cannot be seen statically stay at ON.
        private bool sqlQuotedIdentifierOn = true;

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
                // Transact-SQL has its own comment/string/identifier delimiters and no interpolation,
                // so it takes a separate branch that never touches the C/C# scanners below.
                if (language == SourceLanguage.Sql) { ScanSqlChunk(ref i); continue; }
                // XAML is XML markup: <!-- --> comments, quoted attribute values and element text
                // content. It also never uses the C/C# delimiters, so it has its own branch.
                if (language == SourceLanguage.Xaml) { ScanXamlChunk(ref i); continue; }
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

        // ---- Transact-SQL -------------------------------------------------
        // T-SQL uses -- line comments, nestable /* */ block comments, single-quoted strings
        // ('' is an escaped quote; an optional N prefix is just ordinary code), [bracketed]
        // and "double-quoted" identifiers. Identifiers stay code/English; only comments and
        // single-quoted strings can recommend Chinese. Backslash is not an escape character.
        private void ScanSqlChunk(ref int i)
        {
            if (TryDetectQuotedIdentifierSetting(ref i)) return;
            if (Starts(i, "--")) { ScanSqlLineComment(ref i); return; }
            if (Starts(i, "/*")) { ScanSqlBlockComment(ref i); return; }
            char c = text[i];
            if (c == '\'') { ScanSqlString(ref i); return; }
            if (c == '[') { ScanSqlBracketIdentifier(ref i); return; }
            if (c == '"')
            {
                // ON (default): double quotes delimit identifiers (English). OFF: they delimit
                // string literals, so a quoted run with Chinese text recommends Chinese.
                if (sqlQuotedIdentifierOn) ScanSqlQuotedIdentifier(ref i);
                else ScanSqlQuotedString(ref i);
                return;
            }
            i++;
        }

        // Detects a standalone "SET QUOTED_IDENTIFIER ON|OFF" at a code token boundary and updates
        // the lexer state. Only reached in code (comments/strings/bracketed identifiers are consumed
        // wholesale by their own scanners), so it never fires inside text.
        private bool TryDetectQuotedIdentifierSetting(ref int i)
        {
            int p = i;
            if (p > 0 && IsSqlIdentChar(text[p - 1])) return false;
            if (!WordMatchesAt(p, "set", out int afterSet)) return false;
            if (afterSet < text.Length && IsSqlIdentChar(text[afterSet])) return false;
            p = SkipSqlWhitespace(afterSet);
            if (!WordMatchesAt(p, "quoted_identifier", out int afterName)) return false;
            if (afterName < text.Length && IsSqlIdentChar(text[afterName])) return false;
            p = SkipSqlWhitespace(afterName);
            bool turnOn;
            int afterValue;
            if (WordMatchesAt(p, "on", out afterValue)) turnOn = true;
            else if (WordMatchesAt(p, "off", out afterValue)) turnOn = false;
            else return false;
            if (afterValue < text.Length && IsSqlIdentChar(text[afterValue])) return false;
            sqlQuotedIdentifierOn = turnOn;
            i = afterValue;
            return true;
        }

        private int SkipSqlWhitespace(int p)
        {
            while (p < text.Length && (text[p] == ' ' || text[p] == '\t' || text[p] == '\r' || text[p] == '\n')) p++;
            return p;
        }

        private static bool IsSqlIdentChar(char c) =>
            char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#' || c == '$';

        // Case-insensitive ordinal match of an ASCII keyword; reports the index just past it.
        private bool WordMatchesAt(int p, string word, out int after)
        {
            after = p + word.Length;
            if (after > text.Length) return false;
            for (int k = 0; k < word.Length; k++)
            {
                if (char.ToLowerInvariant(text[p + k]) != char.ToLowerInvariant(word[k])) return false;
            }
            return true;
        }

        private void ScanSqlLineComment(ref int i)
        {
            int start = i + 2;
            i = start;
            while (i < text.Length)
            {
                CheckCancellation(i);
                if (text[i] == '\r' || text[i] == '\n') break;
                i++;
            }
            Add(ContextKind.Comment, start, i);
        }

        private void ScanSqlBlockComment(ref int i)
        {
            // T-SQL block comments nest; track depth instead of stopping at the first */.
            int start = i + 2;
            i = start;
            int depth = 1;
            while (i < text.Length && depth > 0)
            {
                CheckCancellation(i);
                if (Starts(i, "/*")) { depth++; i += 2; }
                else if (Starts(i, "*/")) { depth--; i += 2; }
                else i++;
            }
            Add(ContextKind.Comment, start, depth == 0 ? i - 2 : i);
        }

        private void ScanSqlString(ref int i)
        {
            // On entry i points at the opening single quote. Literals may span multiple lines.
            int start = ++i;
            while (i < text.Length)
            {
                CheckCancellation(i);
                if (text[i] == '\'')
                {
                    // A doubled quote is an escaped quote, so the literal keeps going.
                    if (i + 1 < text.Length && text[i + 1] == '\'') { i += 2; continue; }
                    Add(ContextKind.String, start, i);
                    i++;
                    return;
                }
                i++;
            }
            Add(ContextKind.String, start, i);
        }

        private void ScanSqlBracketIdentifier(ref int i)
        {
            // [schema].[table]; ]] is an escaped closing bracket. Identifiers are code/English.
            i++;
            while (i < text.Length)
            {
                CheckCancellation(i);
                if (text[i] == ']')
                {
                    if (i + 1 < text.Length && text[i + 1] == ']') { i += 2; continue; }
                    i++;
                    return;
                }
                i++;
            }
        }

        private void ScanSqlQuotedIdentifier(ref int i)
        {
            // With QUOTED_IDENTIFIER ON (the default) double quotes delimit identifiers, not strings.
            i++;
            while (i < text.Length)
            {
                CheckCancellation(i);
                if (text[i] == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { i += 2; continue; }
                    i++;
                    return;
                }
                i++;
            }
        }

        private void ScanSqlQuotedString(ref int i)
        {
            // With QUOTED_IDENTIFIER OFF double quotes delimit string literals ("" is an escaped
            // quote). Literals may span multiple lines; Add() decides Chinese from their content.
            int start = ++i;
            while (i < text.Length)
            {
                CheckCancellation(i);
                if (text[i] == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { i += 2; continue; }
                    Add(ContextKind.String, start, i);
                    i++;
                    return;
                }
                i++;
            }
            Add(ContextKind.String, start, i);
        }

        // ---- XAML ---------------------------------------------------------
        // XAML is scanned as XML markup rather than as a C-family language:
        //   <!-- ... -->                       comment  -> Chinese
        //   <Element Attribute="value">        tag/attribute names -> code/English
        //   attribute="..." / '...'            value    -> Chinese when it contains Han text
        //   <![CDATA[ ... ]]>                  text     -> Chinese when it contains Han text
        //   bare element text content          text     -> Chinese when it contains Han text
        // Indentation and other whitespace-only text stay code so moving the caret between tags
        // does not report a spurious string context.
        private void ScanXamlChunk(ref int i)
        {
            if (Starts(i, "<!--")) { ScanXamlComment(ref i); return; }
            if (Starts(i, "<![CDATA[")) { ScanXamlCData(ref i); return; }
            if (text[i] == '<') { ScanXamlTag(ref i); return; }
            ScanXamlText(ref i);
        }

        private void ScanXamlComment(ref int i)
        {
            int start = i + 4;
            i = start;
            while (i < text.Length && !Starts(i, "-->")) { CheckCancellation(i); i++; }
            Add(ContextKind.Comment, start, i);
            if (i < text.Length) i += 3;
        }

        private void ScanXamlCData(ref int i)
        {
            int start = i + 9;
            i = start;
            while (i < text.Length && !Starts(i, "]]>")) { CheckCancellation(i); i++; }
            Add(ContextKind.String, start, i);
            if (i < text.Length) i += 3;
        }

        private void ScanXamlTag(ref int i)
        {
            // Quotes inside a tag always delimit an attribute value (this also covers the values
            // of an <?xml ... ?> declaration). Markup inside the value, e.g. {Binding Name}, is
            // treated as part of the value: it only recommends Chinese when the value itself has
            // Han text, so ordinary English bindings still recommend English.
            i++;
            while (i < text.Length)
            {
                CheckCancellation(i);
                char c = text[i];
                if (c == '"' || c == '\'')
                {
                    int start = ++i;
                    while (i < text.Length && text[i] != c) { CheckCancellation(i); i++; }
                    Add(ContextKind.String, start, i);
                    if (i < text.Length) i++;
                    continue;
                }
                if (c == '>') { i++; return; }
                // Malformed markup (a nested '<' inside a tag): stop and let the outer loop resume.
                if (c == '<') return;
                i++;
            }
        }

        private void ScanXamlText(ref int i)
        {
            int start = i;
            while (i < text.Length && text[i] != '<') { CheckCancellation(i); i++; }
            for (int p = start; p < i; p++)
            {
                if (!char.IsWhiteSpace(text[p])) { Add(ContextKind.String, start, i); return; }
            }
            // Whitespace-only text (indentation / line breaks) is left as code.
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