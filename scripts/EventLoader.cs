using Godot;
using System.Collections.Generic;

public static class EventLoader
{
	public static List<EventData> Load(string path)
	{
		var events = new List<EventData>();

		if (!FileAccess.FileExists(path))
		{
			GD.PrintErr($"[EventLoader] File not found: {path}");
			return events;
		}

		var parser = new XmlParser();
		if (parser.Open(path) != Error.Ok)
		{
			GD.PrintErr($"[EventLoader] Cannot open XML: {path}");
			return events;
		}

		EventData? current = null;
		string? currentChoice = null;
		string? currentElement = null;

		while (parser.Read() == Error.Ok)
		{
			switch (parser.GetNodeType())
			{
				case XmlParser.NodeType.Element:
					currentElement = parser.GetNodeName();
					switch (currentElement)
					{
						case "event":
							current = new EventData
							{
								Id = Attr(parser, "id"),
								Positions = ParsePositions(Attr(parser, "positions")),
								CharacterName = Attr(parser, "char_name"),
								CharacterRole = Attr(parser, "char_role"),
							};
							currentChoice = null;
							break;
						case "left":
							currentChoice = "left";
							if (current != null)
							{
								current.LeftChoiceText    = Attr(parser, "text");
								current.LeftConsequenceId = Attr(parser, "consequence");
							}
							break;
						case "right":
							currentChoice = "right";
							if (current != null)
							{
								current.RightChoiceText    = Attr(parser, "text");
								current.RightConsequenceId = Attr(parser, "consequence");
							}
							break;
						case "effect":
							if (current != null && currentChoice != null)
							{
								string attrName = Attr(parser, "attribute");
								if (int.TryParse(Attr(parser, "value"), out int val))
								{
									var fx = currentChoice == "left" ? current.LeftEffects : current.RightEffects;
									fx[attrName] = val;
								}
							}
							if (parser.IsEmpty()) currentElement = null;
							break;
					}
					break;

				case XmlParser.NodeType.Text:
					if (current == null) break;
					string text = parser.GetNodeData().Trim();
					if (string.IsNullOrEmpty(text)) break;
					switch (currentElement)
					{
						case "title":      current.Title = text; break;
						case "text":       current.Text = text; break;
						case "background": current.BackgroundPath = text; break;
					}
					break;

				case XmlParser.NodeType.ElementEnd:
					switch (parser.GetNodeName())
					{
						case "event":
							if (current != null) { events.Add(current); current = null; }
							break;
						case "left":
						case "right":
							currentChoice = null;
							break;
						case "title":
						case "text":
						case "background":
						case "effect":
							currentElement = null;
							break;
					}
					break;
			}
		}

		GD.Print($"[EventLoader] Loaded {events.Count} events from {path}");
		return events;
	}

	private static string Attr(XmlParser p, string name)
	{
		for (int i = 0; i < p.GetAttributeCount(); i++)
			if (p.GetAttributeName(i) == name)
				return p.GetAttributeValue(i);
		return "";
	}

	private static List<string> ParsePositions(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
		return new List<string>(raw.Split(' ', System.StringSplitOptions.RemoveEmptyEntries));
	}
}
