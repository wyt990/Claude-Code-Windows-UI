namespace ClaudeCodeGUI;

/// <summary>数据模型，绑定到技能列表中的每张卡片</summary>
public class SkillItem
{
    public string FilePath { get; set; } = "";
    public string CmdDisplay { get; set; } = "";   // /skill-name
    public string DescSnippet { get; set; } = "";  // first sentence
    public string GroupTag { get; set; } = "";    // folder name if nested
}
