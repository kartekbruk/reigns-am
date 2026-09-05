using Godot;
using System;
using System.Collections.Generic;

public enum CareerLevel { Intern, Junior, Mid, Senior }

public partial class GameState : Node
{
	[Signal] public delegate void AttributeChangedEventHandler(string key, int value);
	[Signal] public delegate void GameOverEventHandler(string message, bool isWin);
	[Signal] public delegate void SavedByChanceEventHandler(string message);
	[Signal] public delegate void GoodMomentEventHandler(int total);

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
	private int _chancesUsed = 0;
	private int _goodMoments = 0;

	public int GoodMoments => _goodMoments;

	// One chance granted per completed year, never stacks above 1
	public int AvailableChances => Mathf.Min(1, _totalMonths / 12 - _chancesUsed);

	private static readonly DateTime StartDate = new(2025, 1, 1);
	private int _totalMonths = 0;

	public int CurrentSprint => _totalMonths + 1;
	public DateTime CurrentDate => StartDate.AddMonths(_totalMonths);

	// Career: Intern < 12 months (1 yr), Junior < 36 (2 more yrs), Mid < 60 (2 more yrs), then Senior
	public CareerLevel CurrentLevel => _totalMonths switch
	{
		< 12 => CareerLevel.Intern,
		< 36 => CareerLevel.Junior,
		< 60 => CareerLevel.Mid,
		_    => CareerLevel.Senior,
	};

	public string LevelName => CurrentLevel switch
	{
		CareerLevel.Intern => "Intern",
		CareerLevel.Junior => "Junior Developer",
		CareerLevel.Mid    => "Mid Developer",
		CareerLevel.Senior => "Senior Developer",
		_                  => "Senior Developer",
	};

	public string LevelKey => CurrentLevel.ToString().ToLower();

	public void AdvanceMonth() => _totalMonths += 1;

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
		_totalMonths = 0;
		_chancesUsed = 0;
		_goodMoments = 0;
		foreach (var key in Keys)
			_values[key] = 50;
	}

	private void CheckEndConditions()
	{
		// Good moments: every attribute that hit 100 drops to 75 and scores a point
		foreach (var key in Keys)
		{
			if (_values[key] >= 100)
			{
				_goodMoments++;
				_values[key] = 75;
				EmitSignal(SignalName.AttributeChanged, key, 75);
				EmitSignal(SignalName.GoodMoment, _goodMoments);
			}
		}

		// Loss conditions: first attribute at 0 triggers a chance or game over
		if (_gameOver) return;
		foreach (var key in Keys)
		{
			if (_values[key] <= 0)
			{
				if (AvailableChances > 0)
				{
					_chancesUsed++;
					foreach (var k in Keys)
					{
						_values[k] = 25;
						EmitSignal(SignalName.AttributeChanged, k, 25);
					}
					EmitSignal(SignalName.SavedByChance, ChanceMessage(key));
					return;
				}
				_gameOver = true;
				EmitSignal(SignalName.GameOver, LossMessage(key), false);
				return;
			}
		}
	}

	private static string ChanceMessage(string key) => key switch
	{
		"wellbeing"  => "You took a recharge.",
		"morale"     => "Your manager organised a CSGO LAN party.",
		"prosperity" => "Your company demerged — fresh start.",
		"codebase"   => "You refactored the codebase.",
		_            => "A second chance."
	};

	private static string LossMessage(string key) => key switch
	{
		"wellbeing"  => "You burned out completely.\nTime to take a sabbatical.",
		"morale"     => "The team had enough.\nMass resignation emails are incoming.",
		"prosperity" => "The company went bankrupt.\nThe investors are not happy.",
		"codebase"   => "Production is on fire.\nThe codebase finally collapsed.",
		_            => "It all fell apart."
	};

}
