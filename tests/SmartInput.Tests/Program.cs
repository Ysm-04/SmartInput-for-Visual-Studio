using System;
using System.Diagnostics;
using System.Threading;
using SmartInput.Core;

namespace SmartInput.Tests
{
    internal static class Program
    {
        private static int passed, failed;

        [STAThread]
        private static int Main()
        {
            Case("空文件", "¦", ContextKind.Code, InputMode.English);
            Case("普通代码", "int cou¦nt = 0; // 当前数量", ContextKind.Code, InputMode.English);
            Case("中文标识符仍属于代码", "int 数¦量;", ContextKind.Code, InputMode.English);
            Case("单斜线", "/¦", ContextKind.Code, InputMode.English);
            Case("刚输入双斜线", "//¦", ContextKind.Comment, InputMode.Chinese);
            Case("英文注释", "// TO¦DO", ContextKind.Comment, InputMode.Chinese);
            Case("注释行尾", "// 中文¦\r\nint x;", ContextKind.Comment, InputMode.Chinese);
            Case("注释之后换行", "// 中文\r\n¦int x;", ContextKind.Code, InputMode.English);
            Case("三斜线文档", "/// ¦summary", ContextKind.Comment, InputMode.Chinese);
            Case("多行注释", "/* one\n ¦two */", ContextKind.Comment, InputMode.Chinese);
            Case("未闭合多行注释", "/*¦", ContextKind.Comment, InputMode.Chinese);
            Case("多行注释结束前", "/* 中文¦*/", ContextKind.Comment, InputMode.Chinese);
            Case("多行注释结束后", "/* 中文 */¦ int n;", ContextKind.Code, InputMode.English);
            Case("空字符串", "auto s = \"¦\";", ContextKind.String, InputMode.English);
            Case("中文字符串开头", "auto s = \"¦加载成功\";", ContextKind.String, InputMode.Chinese);
            Case("中文字符串末尾", "auto s = \"加载成功¦\";", ContextKind.String, InputMode.Chinese);
            Case("中文字符串后", "auto s = \"加载成功\"¦;", ContextKind.Code, InputMode.English);
            Case("字符串前", "auto s = ¦\"加载成功\";", ContextKind.Code, InputMode.English);
            Case("URL不是注释", "auto s = \"https://example.com/¦\";", ContextKind.String, InputMode.English);
            Case("字符串内块注释符号", "auto s = \"/* ¦ */\";", ContextKind.String, InputMode.English);
            Case("转义引号", "auto s = \"\\\"中文¦\\\"\";", ContextKind.String, InputMode.Chinese);
            Case("转义反斜线后退出字符串", "auto s = \"abc\\\\\";¦", ContextKind.Code, InputMode.English);
            Case("未闭合字符串", "auto s = \"中文¦", ContextKind.String, InputMode.Chinese);
            Case("普通字符串换行恢复", "auto s = \"abc\n¦int n;", ContextKind.Code, InputMode.English);
            Case("宽字符串", "auto s = L\"中文¦\";", ContextKind.String, InputMode.Chinese);
            Case("UTF8字符串", "auto s = u8\"中文¦\";", ContextKind.String, InputMode.Chinese);
            Case("C++原始字符串", "auto s = R\"tag(\" // 中文¦)tag\";", ContextKind.String, InputMode.Chinese);
            Case("C++原始多行", "auto s = u8R\"(abc\n中文¦)\";", ContextKind.String, InputMode.Chinese);
            Case("C++原始字符串后", "auto s = R\"tag(中文)tag\";¦", ContextKind.Code, InputMode.English);
            Case("C++原始字符串伪结束", "auto s = R\"x(foo)\"中文¦)x\";", ContextKind.String, InputMode.Chinese);
            Case("数字分隔符", "int n = 1'000; //¦ hello", ContextKind.Comment, InputMode.Chinese);
            Case("十六进制分隔符", "int n = 0xA'FFF; ¦", ContextKind.Code, InputMode.English);
            Case("字符字面量", "char c = '¦中';", ContextKind.Character, InputMode.English);
            Case("转义字符后注释", "char c = '\\''; //¦", ContextKind.Comment, InputMode.Chinese);
            Case("C++注释续行", "// 中文\\\r\n¦continued", ContextKind.Comment, InputMode.Chinese);
            Case("C++字符串续行", "auto s = \"中文\\\r\n¦continued\";", ContextKind.String, InputMode.Chinese);
            Case("扩展区汉字", "auto s = \"\U00020000¦\";", ContextKind.String, InputMode.Chinese);
            Case("只有emoji", "auto s = \"😀¦\";", ContextKind.String, InputMode.English);

            CSharp("C#常规注释", "// ¦", ContextKind.Comment, InputMode.Chinese);
            CSharp("C#注释无续行", "// 中文\\\n¦var x = 1;", ContextKind.Code, InputMode.English);
            CSharp("C#逐字字符串", "var s = @\"中文\n¦\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#逐字双引号", "var s = @\"a\"\"中文¦\"\"b\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#插值正文", "var s = $\"数¦量：{count}\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#插值表达式", "var s = $\"数量：{cou¦nt}\";", ContextKind.Code, InputMode.English);
            CSharp("C#插值表达式开始", "var s = $\"数量：{¦count}\";", ContextKind.Code, InputMode.English);
            CSharp("C#插值后中文", "var s = $\"{count}个¦\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#插值转义括号", "var s = $\"{{中文¦}}\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#插值对象初始化", "var s = $\"数量{new { N = 1 }.¦N}中文\";", ContextKind.Code, InputMode.English);
            CSharp("C#插值嵌套字符串", "var s = $\"数量{F(\"中文¦\")}\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#插值嵌套注释", "var s = $\"{ /* ¦ */ count }\";", ContextKind.Comment, InputMode.Chinese);
            CSharp("C#逐字插值", "var s = $@\"中文\n¦{x}\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#逐字插值另一前缀", "var s = @$\"中文{¦x}\";", ContextKind.Code, InputMode.English);
            CSharp("C#原始字符串", "var s = \"\"\"中文¦\"\"\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#原始多行", "var s = \"\"\"\n中文¦\n\"\"\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#原始内普通引号", "var s = \"\"\"\" 中文\"\"\"¦\"\"\"\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#双美元原始插值", "var s = $$\"\"\"中文{{¦count}}\"\"\";", ContextKind.Code, InputMode.English);
            CSharp("C#双美元原始单括号正文", "var s = $$\"\"\"中文{¦count}\"\"\";", ContextKind.String, InputMode.Chinese);
            CSharp("C#空原始字符串后", "var s = \"\"\"\"\"\";¦", ContextKind.Code, InputMode.English);

            SqlTests();
            XamlTests();
            PolicyTests();
            ImeResyncTests();
            InputMethodProfileTests();
            RobustnessTests();
            CaretColorTests.Run(Check);
            Console.WriteLine($"RESULT: {passed} passed, {failed} failed");
            return failed == 0 ? 0 : 1;
        }

        private static void CSharp(string name, string marked, ContextKind kind, InputMode mode) => Case(name, marked, kind, mode, SourceLanguage.CSharp);
        private static void Sql(string name, string marked, ContextKind kind, InputMode mode) => Case(name, marked, kind, mode, SourceLanguage.Sql);
        private static void Xaml(string name, string marked, ContextKind kind, InputMode mode) => Case(name, marked, kind, mode, SourceLanguage.Xaml);
        private static void Case(string name, string marked, ContextKind kind, InputMode mode, SourceLanguage language = SourceLanguage.Cpp)
        {
            Check(name, () =>
            {
                int position = marked.IndexOf('¦');
                var result = ContextAnalyzer.Analyze(marked.Remove(position, 1), language).At(position);
                Assert(result.Kind == kind && result.Recommended == mode, $"expected {kind}/{mode}, got {result.Kind}/{result.Recommended}");
            });
        }

        private static void SqlTests()
        {
            Sql("SQL空文件", "¦", ContextKind.Code, InputMode.English);
            Sql("SQL普通代码", "SELECT cou¦nt FROM dbo.[Order]", ContextKind.Code, InputMode.English);
            Sql("SQL中文标识符仍属代码", "SELECT 数¦量 FROM t", ContextKind.Code, InputMode.English);
            Sql("SQL负数单减号不是注释", "WHERE x = -¦1", ContextKind.Code, InputMode.English);
            Sql("SQL单行注释", "-- ¦查询用户", ContextKind.Comment, InputMode.Chinese);
            Sql("SQL英文注释也切中文", "-- TO¦DO", ContextKind.Comment, InputMode.Chinese);
            Sql("SQL注释行尾", "-- 中文¦\nSELECT 1", ContextKind.Comment, InputMode.Chinese);
            Sql("SQL注释换行后代码", "-- 中文\nSEL¦ECT 1", ContextKind.Code, InputMode.English);
            Sql("SQL块注释", "/* one\n ¦two */", ContextKind.Comment, InputMode.Chinese);
            Sql("SQL嵌套块注释内层", "/* outer /* in¦ner */ still */", ContextKind.Comment, InputMode.Chinese);
            Sql("SQL嵌套块注释外层", "/* outer /* inner */ sti¦ll */", ContextKind.Comment, InputMode.Chinese);
            Sql("SQL嵌套块注释闭合后", "/* a /* b */ c */ SEL¦ECT 1", ContextKind.Code, InputMode.English);
            Sql("SQL未闭合嵌套注释", "/* a /* b ¦", ContextKind.Comment, InputMode.Chinese);
            Sql("SQL块注释结束后", "/* 中文 */ ¦SELECT 1", ContextKind.Code, InputMode.English);
            Sql("SQL空字符串", "'¦'", ContextKind.String, InputMode.English);
            Sql("SQL英文字符串", "WHERE n = 'ab¦c'", ContextKind.String, InputMode.English);
            Sql("SQL中文字符串", "N'加载成¦功'", ContextKind.String, InputMode.Chinese);
            Sql("SQL中文字符串无前缀", "'查询用¦户'", ContextKind.String, InputMode.Chinese);
            Sql("SQL转义单引号英文", "'it''s ¦ok'", ContextKind.String, InputMode.English);
            Sql("SQL转义单引号中文", "'中文''继¦续'", ContextKind.String, InputMode.Chinese);
            Sql("SQL字符串跨行", "N'第一行\n第二行¦'", ContextKind.String, InputMode.Chinese);
            Sql("SQL字符串后代码", "N'中文' ¦", ContextKind.Code, InputMode.English);
            Sql("SQL字符串前代码", "SET @s = ¦N'中文'", ContextKind.Code, InputMode.English);
            Sql("SQL字符串内横线非注释", "'a-- ¦b'", ContextKind.String, InputMode.English);
            Sql("SQL字符串内块注释符号", "'/* ¦ */'", ContextKind.String, InputMode.English);
            Sql("SQL未闭合字符串", "'中文¦", ContextKind.String, InputMode.Chinese);
            Sql("SQL方括号标识符", "SELECT [列¦名] FROM t", ContextKind.Code, InputMode.English);
            Sql("SQL方括号内引号横线", "[a'-- ¦x]", ContextKind.Code, InputMode.English);
            Sql("SQL方括号转义", "[a]]b] ¦FROM t", ContextKind.Code, InputMode.English);
            Sql("SQL双引号标识符", "SELECT \"列¦名\" FROM t", ContextKind.Code, InputMode.English);
            Sql("SQL双引号内横线", "\"a-- ¦b\"", ContextKind.Code, InputMode.English);
            Sql("SQL QUOTED_IDENTIFIER OFF双引号字符串中文", "SET QUOTED_IDENTIFIER OFF; SELECT \"中¦文\";", ContextKind.String, InputMode.Chinese);
            Sql("SQL QUOTED_IDENTIFIER OFF双引号英文", "SET QUOTED_IDENTIFIER OFF; SELECT \"ab¦c\";", ContextKind.String, InputMode.English);
            Sql("SQL QUOTED_IDENTIFIER OFF双引号内横线", "SET QUOTED_IDENTIFIER OFF; SELECT \"a-- ¦b\";", ContextKind.String, InputMode.English);
            Sql("SQL QUOTED_IDENTIFIER OFF后重新ON为标识符", "SET QUOTED_IDENTIFIER OFF; SELECT \"中文\"; SET QUOTED_IDENTIFIER ON; SELECT \"列¦名\";", ContextKind.Code, InputMode.English);
            Sql("SQL小写set quoted_identifier识别", "set quoted_identifier off; select \"中¦文\";", ContextKind.String, InputMode.Chinese);
            Sql("SQL括号后字符串中文", "[t] SET @s = '加¦载'", ContextKind.String, InputMode.Chinese);
            Sql("SQL仅emoji字符串", "'😀¦'", ContextKind.String, InputMode.English);
            Sql("SQL扩展区汉字", "'\U00020000¦'", ContextKind.String, InputMode.Chinese);
        }


        private static void XamlTests()
        {
            Xaml("XAML空文件", "¦", ContextKind.Code, InputMode.English);
            Xaml("XAML标签名", "<Butt¦on Content=\"确定\"/>", ContextKind.Code, InputMode.English);
            Xaml("XAML属性名", "<Button Content¦=\"确定\"/>", ContextKind.Code, InputMode.English);
            Xaml("XAML英文属性值", "<Button x:Name=\"Btn¦1\"/>", ContextKind.String, InputMode.English);
            Xaml("XAML中文属性值", "<TextBlock Text=\"加载¦成功\"/>", ContextKind.String, InputMode.Chinese);
            Xaml("XAML中文属性值开头", "<TextBlock Text=\"¦中文\"/>", ContextKind.String, InputMode.Chinese);
            Xaml("XAML空属性值", "<TextBlock Text=\"¦\"/>", ContextKind.String, InputMode.English);
            Xaml("XAML单引号中文属性值", "<TextBlock Text='中¦文'/>", ContextKind.String, InputMode.Chinese);
            Xaml("XAML实体英文属性值", "<TextBlock Text=\"A&amp;¦B\"/>", ContextKind.String, InputMode.English);
            Xaml("XAML仅emoji属性值", "<TextBlock Text=\"😀¦\"/>", ContextKind.String, InputMode.English);
            Xaml("XAML中文注释", "<!-- ¦中文注释 -->", ContextKind.Comment, InputMode.Chinese);
            Xaml("XAML英文注释也中文", "<!-- TO¦DO -->", ContextKind.Comment, InputMode.Chinese);
            Xaml("XAML注释内标签", "<!-- <Button/> ¦-->", ContextKind.Comment, InputMode.Chinese);
            Xaml("XAML未闭合注释", "<!-- 中文¦", ContextKind.Comment, InputMode.Chinese);
            Xaml("XAML注释结束", "<!-- 中文 -->¦<Grid>", ContextKind.Code, InputMode.English);
            Xaml("XAML元素文本中文", "<TextBlock>加¦载</TextBlock>", ContextKind.String, InputMode.Chinese);
            Xaml("XAML元素文本英文", "<TextBlock>Lo¦ad</TextBlock>", ContextKind.String, InputMode.English);
            Xaml("XAML元素文本结束", "<TextBlock>中文</TextBlock>¦", ContextKind.Code, InputMode.English);
            Xaml("XAML缩进为代码", "<Grid>\r\n    ¦<Button/>", ContextKind.Code, InputMode.English);
            Xaml("XAML标签内空格", "<Button ¦/>", ContextKind.Code, InputMode.English);
            Xaml("XAML未闭合标签", "<Grid ¦", ContextKind.Code, InputMode.English);
            Xaml("XAMLCDATA中文", "<![CDATA[中文¦文本]]>", ContextKind.String, InputMode.Chinese);
            Xaml("XAMLCDATA英文", "<![CDATA[ab¦c]]>", ContextKind.String, InputMode.English);
            Xaml("XAML标记扩展英文", "<TextBlock Text=\"{Binding Name¦}\"/>", ContextKind.String, InputMode.English);
            Xaml("XAML标记扩展中文", "<TextBlock Text=\"{Binding 名¦称}\"/>", ContextKind.String, InputMode.Chinese);
            Xaml("XAML XML声明后", "<?xml version=\"1.0\" encoding=\"utf-8\"?>¦", ContextKind.Code, InputMode.English);
            Xaml("XAML多个属性中文值", "<TextBlock Text=\"中文\" Tag=\"标签¦\"/>", ContextKind.String, InputMode.Chinese);
            Xaml("XAML自闭合后换行", "<Button/>\r\n¦<Grid/>", ContextKind.Code, InputMode.English);
        }

        private static void ImeResyncTests()
        {
            Check("重同步：未切换输入法不强制写入", () =>
            {
                var policy = new ImeResyncPolicy();
                Assert(!policy.ShouldForceWrite(InputMode.English, InputMode.English));
                Assert(!policy.ShouldForceWrite(InputMode.Chinese, InputMode.English));
            });
            Check("重同步：切换输入法后连续写入三次", () =>
            {
                var policy = new ImeResyncPolicy();
                policy.NoteActiveInputMethodChanged();
                Assert(policy.IsPending);
                Assert(policy.ShouldForceWrite(InputMode.English, InputMode.English));
                Assert(policy.ShouldForceWrite(InputMode.English, InputMode.English));
                Assert(policy.ShouldForceWrite(InputMode.English, InputMode.English));
                Assert(!policy.IsPending);
                Assert(!policy.ShouldForceWrite(InputMode.English, InputMode.English));
            });
            Check("重同步：切换输入法后状态不一致照常写入", () =>
            {
                var policy = new ImeResyncPolicy();
                policy.NoteActiveInputMethodChanged();
                Assert(policy.ShouldForceWrite(InputMode.English, InputMode.Chinese));
            });
            Check("重同步：未知状态不消耗待处理标记", () =>
            {
                var policy = new ImeResyncPolicy();
                policy.NoteActiveInputMethodChanged();
                Assert(!policy.ShouldForceWrite(InputMode.Unknown, InputMode.Chinese));
                Assert(policy.IsPending);
                Assert(policy.ShouldForceWrite(InputMode.English, InputMode.Chinese));
            });
            Check("重同步：Reset 清除待处理标记", () =>
            {
                var policy = new ImeResyncPolicy();
                policy.NoteActiveInputMethodChanged();
                policy.Reset();
                Assert(!policy.IsPending);
                Assert(!policy.ShouldForceWrite(InputMode.English, InputMode.English));
            });
            Check("重同步：连续两次切换各自写入三次", () =>
            {
                var policy = new ImeResyncPolicy();
                policy.NoteActiveInputMethodChanged();
                Assert(policy.ShouldForceWrite(InputMode.Chinese, InputMode.Chinese));
                policy.NoteActiveInputMethodChanged();
                Assert(policy.ShouldForceWrite(InputMode.Chinese, InputMode.Chinese));
                Assert(policy.ShouldForceWrite(InputMode.Chinese, InputMode.Chinese));
                Assert(policy.ShouldForceWrite(InputMode.Chinese, InputMode.Chinese));
                Assert(!policy.ShouldForceWrite(InputMode.Chinese, InputMode.Chinese));
            });
        }

        private static void PolicyTests()
        {
            var comment = new InputContext(ContextKind.Comment, 2, 20, InputMode.Chinese);
            var code = new InputContext(ContextKind.Code, 22, 40, InputMode.English);
            Check("状态机：注释自动中文", () => { var p = Policy(comment); Assert(p.Request(InputMode.English, false, true) == InputMode.Chinese); });
            Check("状态机：相同状态不重复写入", () => { var p = Policy(comment); Assert(p.Request(InputMode.Chinese, false, true) == null); });
            Check("状态机：手动英文变红", () => { var p = Policy(comment); p.ObserveManualChange(InputMode.English); Assert(p.IsRed(InputMode.English)); Assert(p.Request(InputMode.English, false, true) == null); });
            Check("状态机：连续输入不抢回", () => { var p = Policy(comment); p.ObserveManualChange(InputMode.English); p.SetContext(new InputContext(ContextKind.Comment, 2, 30, InputMode.Chinese)); Assert(p.Desired == InputMode.English); });
            Check("状态机：切回恢复", () => { var p = Policy(comment); p.ObserveManualChange(InputMode.English); p.ObserveManualChange(InputMode.Chinese); Assert(!p.HasManualOverride && !p.IsRed(InputMode.Chinese)); });
            Check("状态机：跨区域清除覆盖", () => { var p = Policy(comment); p.ObserveManualChange(InputMode.English); p.SetContext(code); Assert(!p.HasManualOverride && p.Desired == InputMode.English); });
            Check("状态机：代码手动中文变红", () => { var p = Policy(code); p.ObserveManualChange(InputMode.Chinese); Assert(p.IsRed(InputMode.Chinese)); });
            Check("状态机：未知状态不操作", () => Assert(Policy(comment).Request(InputMode.Unknown, false, true) == null));
            Check("状态机：组词中不切换", () => Assert(Policy(code).Request(InputMode.Chinese, true, true) == null));
            Check("状态机：失焦不操作", () => Assert(Policy(comment).Request(InputMode.English, false, false) == null));
            Check("状态机：暂停不操作", () => { var p = Policy(comment); p.Paused = true; Assert(p.Request(InputMode.English, false, true) == null); });
            Check("状态机：切换文档清除状态", () => { var p = Policy(comment); p.ObserveManualChange(InputMode.English); p.Reset(); p.SetContext(comment); Assert(!p.HasManualOverride); });
            Check("状态机：文本变中文消除覆盖", () => { var p = Policy(new InputContext(ContextKind.String, 1, 3, InputMode.English)); p.ObserveManualChange(InputMode.Chinese); p.SetContext(new InputContext(ContextKind.String, 1, 4, InputMode.Chinese)); Assert(!p.HasManualOverride); });
            Check("状态机：无上下文不切换", () => Assert(new SwitchPolicy().Request(InputMode.Chinese, false, true) == null));
            Check("状态机：尚未确认真实状态不显示红色", () => { var p = Policy(code); p.ObserveManualChange(InputMode.Chinese); Assert(!p.IsRed(InputMode.Unknown)); });
        }

        private static void RobustnessTests()
        {
            Check("取消过期分析", () =>
            {
                var cancellation = new CancellationTokenSource(); cancellation.Cancel();
                try { ContextAnalyzer.Analyze(new string('x', 10000), SourceLanguage.Cpp, cancellation.Token); throw new Exception("not cancelled"); }
                catch (OperationCanceledException) { }
                finally { cancellation.Dispose(); }
            });
            Check("随机文本所有插入位置安全", () =>
            {
                var random = new Random(2026);
                const string alphabet = "abc123中/\\\"'{}()$@*-[]\r\n";
                for (int sample = 0; sample < 300; sample++)
                {
                    var chars = new char[random.Next(1, 160)];
                    for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[random.Next(alphabet.Length)];
                    foreach (SourceLanguage language in Enum.GetValues(typeof(SourceLanguage)))
                    {
                        var map = ContextAnalyzer.Analyze(new string(chars), language);
                        for (int position = 0; position <= chars.Length; position++)
                        {
                            var context = map.At(position);
                            Assert(context.Start <= position && context.End >= position, "invalid region bounds");
                        }
                    }
                }
            });
            Check("大文件分析与光标查询性能", () =>
            {
                var text = new System.Text.StringBuilder();
                for (int i = 0; i < 20000; i++) text.Append("int n = 1; // comment 中文\r\n");
                var watch = Stopwatch.StartNew();
                var map = ContextAnalyzer.Analyze(text.ToString(), SourceLanguage.Cpp);
                long scanMs = watch.ElapsedMilliseconds;
                for (int i = 0; i < 100000; i++) map.At(i % text.Length);
                Console.WriteLine($"  PERF {text.Length} chars: scan={scanMs} ms, scan+100000 lookups={watch.ElapsedMilliseconds} ms");
                Assert(watch.Elapsed < TimeSpan.FromSeconds(10), "unexpectedly slow");
            });
        }

        private static void InputMethodProfileTests()
        {
            var microsoftClass = new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E");
            var microsoftProfile = new Guid("FA550B04-5AD7-411F-A5AC-CA038EC515D7");
            var sogouClass = new Guid("E7EA138E-69F8-11D7-A6EA-00065B844310");
            var sogouProfile = new Guid("E7EA138F-69F8-11D7-A6EA-00065B844311");
            Check("Profile匹配：微软拼音", () => Assert(SmartInput.VisualStudio.SupportedInputMethodProfile.IsMicrosoftPinyin(
                1, 0x0804, microsoftClass, microsoftProfile)));
            Check("Profile匹配：搜狗拼音", () => Assert(SmartInput.VisualStudio.SupportedInputMethodProfile.IsSogouPinyin(
                1, 0x0804, sogouClass, sogouProfile)));
            Check("Profile匹配：不同输入法不误判", () => Assert(!SmartInput.VisualStudio.SupportedInputMethodProfile.IsSogouPinyin(
                1, 0x0804, microsoftClass, microsoftProfile)));
            Check("Profile匹配：不同语言不误判", () => Assert(!SmartInput.VisualStudio.SupportedInputMethodProfile.IsSogouPinyin(
                1, 0x0404, sogouClass, sogouProfile)));
            Check("Profile匹配：非输入处理器不误判", () => Assert(!SmartInput.VisualStudio.SupportedInputMethodProfile.IsSogouPinyin(
                0, 0x0804, sogouClass, sogouProfile)));
        }

        private static SwitchPolicy Policy(InputContext context) { var p = new SwitchPolicy(); p.SetContext(context); return p; }
        private static void Assert(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
        private static void Check(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
        }
    }
}