using System.Collections.Generic;

public class EventData
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string BackgroundPath { get; set; } = "";
    public string LeftChoiceText { get; set; } = "";
    public string RightChoiceText { get; set; } = "";
    public Dictionary<string, int> LeftEffects { get; set; } = new();
    public Dictionary<string, int> RightEffects { get; set; } = new();
}
