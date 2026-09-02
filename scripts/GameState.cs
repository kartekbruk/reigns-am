using Godot;
using System.Collections.Generic;

public partial class GameState : Node
{
    [Signal] public delegate void AttributeChangedEventHandler(string key, int value);
    [Signal] public delegate void GameOverEventHandler(string message, bool isWin);

    public static readonly string[] Keys = { "wellbeing", "morale", "prosperity", "codebase" };

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { "wellbeing",  "Your Wellbeing" },
        { "morale",     "Team's Morale" },
        { "prosperity", "Company Prosperity" },
        { "codebase",   "Codebase Health" },
    };

    private readonly Dictionary<string, int> _values = new()
    {
        { "wellbeing",  50 },
        { "morale",     50 },
        { "prosperity", 50 },
        { "codebase",   50 },
    };

    private bool _gameOver = false;

    public int Get(string key) => _values.TryGetValue(key, out int v) ? v : 50;

    public void Apply(Dictionary<string, int> effects)
    {
        if (_gameOver) return;

        foreach (var (key, delta) in effects)
        {
            if (!_values.ContainsKey(key)) continue;
            _values[key] = Mathf.Clamp(_values[key] + delta, 0, 100);
            EmitSignal(SignalName.AttributeChanged, key, _values[key]);
        }

        CheckEndConditions();
    }

    public void Reset()
    {
        _gameOver = false;
        foreach (var key in Keys)
            _values[key] = 50;
    }

    private void CheckEndConditions()
    {
        foreach (var key in Keys)
        {
            int v = _values[key];
            if (v <= 0)
            {
                _gameOver = true;
                EmitSignal(SignalName.GameOver, LossMessage(key), false);
                return;
            }
            if (v >= 100)
            {
                _gameOver = true;
                EmitSignal(SignalName.GameOver, MaxMessage(key), false);
                return;
            }
        }
    }

    private static string LossMessage(string key) => key switch
    {
        "wellbeing"  => "You burned out completely.\nTime to take a sabbatical.",
        "morale"     => "The team had enough.\nMass resignation emails are incoming.",
        "prosperity" => "The company went bankrupt.\nThe investors are not happy.",
        "codebase"   => "Production is on fire.\nThe codebase finally collapsed.",
        _            => "It all fell apart."
    };

    private static string MaxMessage(string key) => key switch
    {
        "wellbeing"  => "You're so well-rested you stopped caring about work.",
        "morale"     => "The team is so hyped they're ignoring all process. Chaos.",
        "prosperity" => "Uncontrolled growth caught up with you. Total chaos.",
        "codebase"   => "Perfect code, zero features. Customers moved on.",
        _            => "Too much of a good thing."
    };
}
