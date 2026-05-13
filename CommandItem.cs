using System;

namespace ClaudeCodeGUI;

public class CommandItem
{
    public string Icon { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Action? Execute { get; set; }
}
