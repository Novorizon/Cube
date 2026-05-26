# Game Common Charset

用于 `Assets/Arts/Font/NotoSansSC-Regular SDF.asset` 的常用游戏 UI 字符集。

当前 `GameCommonCharset.txt` 包含：

- ASCII 可见字符：空格、英文大小写、数字、常用半角标点。
- 中文 UI 常用符号：中文标点、书名号、引号、括号、顿号、省略号、破折号。
- 全角字符：全角数字、全角标点、全角括号等。
- 游戏常见符号：方向箭头、星级、勾叉、牌面符号、货币、数学比较符、百分号、温度符号。
- GB2312 / CP936 常用集合：简体中文常用汉字、符号、希腊字母、俄文字母、日文假名等。
- 当前项目文本中已经出现的中文、全角符号和中文标点。

在 Unity 编辑器中执行：

`Cube/Font/Apply Game Common Charset To NotoSansSC`

该工具会读取 `GameCommonCharset.txt`，并尝试把字符加入 `NotoSansSC-Regular SDF.asset`。
