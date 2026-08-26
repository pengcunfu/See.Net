using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace See.Services;

/// <summary>
/// 注册自定义语法高亮定义（JSON、TOML、YAML、Log）。
/// 使用纯代码创建高亮定义，不依赖 XSHD 文件解析。
/// </summary>
public static class SyntaxHighlightingService
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            RegisterJson();
            RegisterToml();
            RegisterYaml();
            RegisterLog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register syntax highlightings: {ex}");
        }
    }

    #region 辅助方法

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static HighlightingColor Color(string name, Color fg, bool bold = false, bool italic = false)
    {
        var c = new HighlightingColor { Name = name };
        c.Foreground = new SimpleHighlightingBrush(fg);
        if (bold) c.FontWeight = FontWeights.Bold;
        if (italic) c.FontStyle = FontStyles.Italic;
        return c;
    }

    private static HighlightingRule Rule(string name, Color fg, string pattern,
        bool multiline = false, bool bold = false, bool italic = false)
    {
        var opts = RegexOptions.Compiled;
        if (multiline) opts |= RegexOptions.Multiline;
        return new HighlightingRule
        {
            Color = Color(name, fg, bold, italic),
            Regex = new Regex(pattern, opts)
        };
    }

    /// <summary>创建字符串 Span（必须有 begin 和 end）。</summary>
    private static HighlightingSpan StringSpan(string begin, string end, Color fg, bool multiline = false)
    {
        var opts = RegexOptions.Compiled;
        return new HighlightingSpan
        {
            StartExpression = new Regex(Regex.Escape(begin), opts),
            EndExpression = new Regex(Regex.Escape(end), opts),
            SpanColor = Color("String", fg),
            RuleSet = new HighlightingRuleSet(),
            SpanColorIncludesStart = true,
            SpanColorIncludesEnd = false
        };
    }

    private static void RegisterDef(string name, string[] extensions, HighlightingRuleSet ruleSet)
    {
        var def = new MyHighlightingDefinition(name, extensions, ruleSet);
        HighlightingManager.Instance.RegisterHighlighting(name, extensions, def);
        System.Diagnostics.Debug.WriteLine($"Registered syntax highlighting: {name} [{string.Join(", ", extensions)}]");
    }

    #endregion

    #region JSON

    private static void RegisterJson()
    {
        var rs = new HighlightingRuleSet();

        // 字符串
        rs.Spans.Add(StringSpan("\"", "\"", Hex("#CE9178")));

        // 数字
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?"));
        // 布尔值
        rs.Rules.Add(Rule("Boolean", Hex("#569CD6"), @"\b(?:true|false)\b"));
        // null
        rs.Rules.Add(Rule("Null", Hex("#569CD6"), @"\bnull\b"));
        // 标点
        rs.Rules.Add(Rule("Punctuation", Hex("#D4D4D4"), @"[{}\[\],;:]"));

        RegisterDef("JSON", [".json"], rs);
    }

    #endregion

    #region TOML

    private static void RegisterToml()
    {
        var rs = new HighlightingRuleSet();

        // 字符串（Span 优先匹配，# 在字符串内不会被当作注释）
        rs.Spans.Add(StringSpan("\"\"\"", "\"\"\"", Hex("#CE9178"), multiline: true));
        rs.Spans.Add(StringSpan("'''", "'''", Hex("#CE9178"), multiline: true));
        rs.Spans.Add(StringSpan("\"", "\"", Hex("#CE9178")));
        rs.Spans.Add(StringSpan("'", "'", Hex("#CE9178")));

        // 表名
        rs.Rules.Add(Rule("TableName", Hex("#4EC9B0"), @"^\s*\[\[[\w\.\-]+\]\]", multiline: true, bold: true));
        rs.Rules.Add(Rule("TableName", Hex("#4EC9B0"), @"^\s*\[[\w\.\-]+\]", multiline: true, bold: true));

        // 日期时间
        rs.Rules.Add(Rule("DateTime", Hex("#4EC9B0"),
            @"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?"));

        // 数字
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"0x[0-9a-fA-F_]+"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"0o[0-7_]+"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"0b[01_]+"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"[+-]?(?:inf|nan)"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"),
            @"[+-]?\d[0-9_]*(?:\.\d[0-9_]*)?(?:[eE][+-]?\d[0-9_]*)?"));

        // 布尔值
        rs.Rules.Add(Rule("Boolean", Hex("#569CD6"), @"\b(?:true|false)\b"));

        // 键
        rs.Rules.Add(Rule("Key", Hex("#9CDCFE"), @"[a-zA-Z_][\w\-\.]*(?=\s*=)"));

        // 注释（放在最后，Span 优先级高于 Rule，字符串内的 # 已被 Span 消费）
        rs.Rules.Add(Rule("Comment", Hex("#6A9955"), @"#[^\n]*", italic: true));

        RegisterDef("TOML", [".toml"], rs);
    }

    #endregion

    #region YAML

    private static void RegisterYaml()
    {
        var rs = new HighlightingRuleSet();

        // 字符串
        rs.Spans.Add(StringSpan("\"\"\"", "\"\"\"", Hex("#CE9178"), multiline: true));
        rs.Spans.Add(StringSpan("'''", "'''", Hex("#CE9178"), multiline: true));
        rs.Spans.Add(StringSpan("\"", "\"", Hex("#CE9178")));
        rs.Spans.Add(StringSpan("'", "'", Hex("#CE9178")));

        // 文档分隔符
        rs.Rules.Add(Rule("DocumentSeparator", Hex("#808080"), @"^---\s*$", multiline: true, bold: true));
        rs.Rules.Add(Rule("DocumentSeparator", Hex("#808080"), @"^\.\.\.\s*$", multiline: true, bold: true));

        // 标签
        rs.Rules.Add(Rule("Tag", Hex("#C586C0"), @"![\w]+(?:\.[\w]+)*"));
        // 锚点 & 别名
        rs.Rules.Add(Rule("Anchor", Hex("#DCDCAA"), @"&[\w\-]+"));
        rs.Rules.Add(Rule("Alias", Hex("#DCDCAA"), @"\*[\w\-]+"));

        // 日期时间
        rs.Rules.Add(Rule("Date", Hex("#4EC9B0"),
            @"\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?"));
        rs.Rules.Add(Rule("Date", Hex("#4EC9B0"), @"\d{4}-\d{2}-\d{2}"));

        // 数字
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"0x[0-9a-fA-F_]+"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"0o[0-7_]+"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"0b[01_]+"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"[+-]?\.(?:inf|Inf|INF)"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"), @"[+-]?\.(?:nan|NaN|NAN)"));
        rs.Rules.Add(Rule("Number", Hex("#B5CEA8"),
            @"[+-]?\d[0-9_]*(?:\.\d[0-9_]*)?(?:[eE][+-]?\d[0-9_]*)?"));

        // 布尔值
        rs.Rules.Add(Rule("Boolean", Hex("#569CD6"),
            @"\b(?:true|false|True|False|TRUE|FALSE|yes|no|Yes|No|YES|NO|on|off|On|Off|ON|OFF)\b"));
        // Null
        rs.Rules.Add(Rule("Null", Hex("#569CD6"), @"\b(?:null|Null|NULL)\b"));

        // 键
        rs.Rules.Add(Rule("Key", Hex("#9CDCFE"), @"^\s*[\w][\w\-\.]*(?=\s*:)", multiline: true));

        // 注释（最后，Span 优先级高于 Rule）
        rs.Rules.Add(Rule("Comment", Hex("#6A9955"), @"#[^\n]*", italic: true));

        RegisterDef("YAML", [".yaml", ".yml"], rs);
    }

    #endregion

    #region Log

    private static void RegisterLog()
    {
        var rs = new HighlightingRuleSet();

        // 时间戳
        rs.Rules.Add(Rule("Timestamp", Hex("#90CAF9"),
            @"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?\b"));
        rs.Rules.Add(Rule("Timestamp", Hex("#90CAF9"),
            @"\b\d{4}/\d{2}/\d{2} \d{2}:\d{2}:\d{2}(?:\.\d+)?\b"));

        // 日志级别
        rs.Rules.Add(Rule("Error", Hex("#EF9A9A"), @"\b(?:ERROR|FATAL|CRITICAL)\b", bold: true));
        rs.Rules.Add(Rule("Warning", Hex("#FFE082"), @"\bWARN(?:ING)?\b"));
        rs.Rules.Add(Rule("Info", Hex("#A5D6A7"), @"\bINFO\b"));
        rs.Rules.Add(Rule("Debug", Hex("#B0BEC5"), @"\bDEBUG\b"));
        rs.Rules.Add(Rule("Trace", Hex("#B0BEC5"), @"\b(?:TRACE|VERBOSE)\b", italic: true));

        // 异常
        rs.Rules.Add(Rule("Exception", Hex("#EF9A9A"), @"\b\w+Exception\b", italic: true));

        // 堆栈
        rs.Rules.Add(Rule("StackTrace", Hex("#B0BEC5"), @"^\s+at\s+.*$", multiline: true));

        RegisterDef("Log", [".log"], rs);
    }

    #endregion
}

/// <summary>自定义高亮定义实现。</summary>
file sealed class MyHighlightingDefinition : IHighlightingDefinition
{
    public MyHighlightingDefinition(string name, string[] extensions, HighlightingRuleSet mainRuleSet)
    {
        Name = name;
        Extensions = extensions;
        MainRuleSet = mainRuleSet;
    }

    public string Name { get; }
    public string[] Extensions { get; }
    public HighlightingRuleSet MainRuleSet { get; }
    public IEnumerable<HighlightingColor> NamedHighlightingColors { get; } = [];
    public IDictionary<string, string> Properties => new Dictionary<string, string>();

    public HighlightingColor? GetNamedColor(string name) => null;
    public HighlightingRuleSet? GetNamedRuleSet(string name) => null;
}
