#include <cstdio>

int main()
{
    int count = 0; // 当前数量
    // 使用输入法当前配置的中英文切换键输入 TODO，预期显示配置的覆盖光标颜色且不抢回。
    /* 多行注释
       中文正文
    */
    const char* english = "https://example.com";
    const char* chinese = "加载成功";
    const char* empty = "";
    const char* raw = R"tag(包含 "引号" 和 // 的中文文本)tag";
    std::printf("数量：%d\n", count);
    return 0;
}
