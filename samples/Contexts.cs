// 此文件用于编辑器交互检查，不参与正式构建。
class Example
{
    /// <summary>中文文档注释</summary>
    void Demo(int count)
    {
        var english = "https://example.com";
        var chinese = "加载成功";
        var empty = "";
        var interpolated = $"数量：{count}个";
        var verbatim = @"第一行中文
第二行中文";
        var raw = """包含 "引号" 和 // 的中文文本""";
        var rawInterpolated = $$"""数量：{{count}}个""";
        // 手动切英文输入 TODO，检查自定义光标颜色与保持行为。
    }
}
