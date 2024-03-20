using JsonUtilities;
using Framework.Slides.JsonClasses;
using GameStateInventory;
using FrameworkItems;

namespace Framework.Slides;

public static class PositionPresets
{

}

// This is hopelessly overengineered
// public struct Action(string name, int numArgs, List<List<string>> possibleArgs)
// {
// 	public string Name = name;
// 	public int NumArgs = numArgs;
// 	// if empty, no finite number of possible args
// 	public List<List<string>> PossibleArgs = possibleArgs;
// }

// public static class Actions
// {
// 	public static Action Route { get; } = new(
// 		"Route", 1, [[]]
// 	);

// 	public static Action AddItem { get; } = new(
// 		"AddItem", 1, [[]]
// 	);

// 	public static Action RemoveItem { get; } = new(
// 		"RemoveItem", 1, [[]]
// 	);

// 	public static Action SetGameState { get; } = new(
// 		"SetGameState", 2, [[], ["true", "false", "toggle"]]
// 	);

// 	public static Action RequireItem { get; } = new(
// 		"RequireItem", 1, [[]]
// 	);

// 	public static Action RequireGameState { get; } = new(
// 		"RequireGameState", 1, [[]]
// 	);

// 	public static Action StartBlock
// }

public class SlideService(JsonUtility jsonUtility, GameState gameState, SlidesVerifier slidesVerifier)
{
	private readonly JsonUtility jsonUtility = jsonUtility;
	private readonly GameState gameState = gameState;
	private readonly SlidesVerifier slidesVerifier = slidesVerifier;

	public Dictionary<string, JsonSlide> Slides { get; private set; } = null!;

	public Dictionary<JsonSlide, string> InverseSlides { get; private set; } = null!;

	// private readonly TaskCompletionSource<bool> _initCompletionSource = new();
	// public Task Initialization => _initCompletionSource.Task;

	private async Task<Dictionary<string, JsonSlide>> FetchSlidesAsync(string url)
	{
		// assign return value from GetFromJsonAsync to slides if it is not null, otherwise throw an exception
		// var slides = await Http.GetFromJsonAsync<Dictionary<string, JsonSlide>>(url) ?? 
		// throw new Exception("Slides is null");
		var slides = await jsonUtility.LoadFromJsonAsync<Dictionary<string, JsonSlide>>(url);
		return slides;
	}

	public async Task Init(bool debugMode = false)
	{
		Slides = await FetchSlidesAsync("Slides.json");
		InverseSlides = Slides.ToDictionary(x => x.Value, x => x.Key);
		// _initCompletionSource.SetResult(true);
		CreateGameStateEntries();
		if (debugMode)
		{
			slidesVerifier.VerifySlides(Slides);
		}
	}

	public JsonSlide GetSlide(string slideId)
	{
		try
		{
			return Slides[slideId];
		}
		catch (KeyNotFoundException)
		{
			throw new KeyNotFoundException($"No Slide with Id `{slideId}` found");
		};
	}

	public string GetSlideId(JsonSlide slide)
	{
		try
		{
			// Works, but is slow
			// return Slides.FirstOrDefault(x => x.Value == slide).Key;
			// use inverted dictionary
			return InverseSlides[slide];
		}
		catch (KeyNotFoundException)
		{
			//TODO: Implement ToString in JsonSlide
			throw new KeyNotFoundException("No Id corresponding to given JsonSlide found");
		}
	}

	public bool CheckForSlide(string slideId)
	{
		try
		{
			GetSlide(slideId);
		}
		catch (KeyNotFoundException)
		{
			return false;
		}
		return true;
	}
	public bool CheckForSlideId(JsonSlide slide)
	{
		try
		{
			GetSlideId(slide);
		}
		catch (KeyNotFoundException)
		{
			return false;
		}
		return true;
	}

	// As of now, this is quite a useless function, but maybe we can add a flag 
	// to the Slides.json that makes a slide the first one no matter the order
	public string GetStartSlideId()
	{
		// Console.WriteLine(Slides);
		return Slides.Keys.First();
		// return "error";
	}

	public void CreateGameStateEntries()
	{
		// iterate over slides
		foreach (var slide in Slides)
		{
			// if slide is a minigame, skip it
			if (slide.Value.Type is string type)
			{
				if (type == "Minigame") { continue; }
			}
			foreach (var button in slide.Value.Buttons!)
			{
				if (button.Value.Visible is bool visible)
				{
					if (visible)
					{
						gameState.AddVisibility($"{slide.Key}.{button.Key}", true);
					}
					else
					{
						gameState.AddVisibility($"{slide.Key}.{button.Key}", false);
					}
				}
			}
		}
	}
}


// Couldn't resist my urge to overengineer again
// basically just for the name
public class SlidesJsonException : Exception
{
	public SlidesJsonException() { }
	public SlidesJsonException(string message) : base(message) { }
	public SlidesJsonException(string message, Exception inner) : base(message, inner) { }
}

public class SlidesVerifier(GameState gameState, Items items)
{
	private readonly GameState gameState = gameState;
	private readonly Items items = items;

	private Dictionary<string, JsonSlide>? CurrentState { get; set; }

	private static readonly string[] SetGameStateOptions = ["true", "false", "toggle"];
	private static readonly string[] ButtonTypeOptions = ["rect", "polygon", "image", "circle"];



	public void Init(Dictionary<string, JsonSlide> slides)
	{
		CurrentState = slides;
	}


	public void VerifySlides(Dictionary<string, JsonSlide> slides)
	{
		CurrentState = slides;
		foreach (var kvp in slides)
		{
			try
			{
				VerifySlide(kvp.Key, kvp.Value);
			}
			catch (SlidesJsonException e)
			{
				throw new SlidesJsonException($"Error in Slides.json: ", e);
			}
		}
	}

	public void VerifySlide(string id, JsonSlide slide)
	{
		// check if is minigame, if yes, other stuff applies
		if (slide.Type == "Minigame")
		{
			if (slide.MinigameDefClassName is null)
			{
				throw new SlidesJsonException($"At Slide \"{id}\": \"MinigameDefClassName\" undefined");
			}
			if (slide.FallbackSlide is null)
			{
				throw new SlidesJsonException($"At Slide \"{id}\": \"FallbackSlide\" undefined");
			}
		}
		else
		{
			// check important things
			// image can't be null
			if (slide.Image is null)
			{
				throw new SlidesJsonException($"At Slide \"{id}\": \"Image\" undefined");
			}
			// slide buttons can't be null, but can be empty
			if (slide.Buttons is null)
			{
				throw new SlidesJsonException($"At Slide \"{id}\": \"Buttons\" undefined");
			}
			// if buttons is empty, OnEnter can't be null or empty
			if (slide.Buttons.Count == 0)
			{
				if (slide.OnEnter is null)
				{
					throw new SlidesJsonException($"At Slide \"{id}\": \"Buttons\" emtpy and \"OnEnter\" undefined");
				}
				else
				{
					if (slide.OnEnter.Count == 0)
					{
						throw new SlidesJsonException($"At Slide \"{id}\": \"Buttons\" emtpy and \"OnEnter\" empty");
					}
				}
			}
			// iterate over buttons and pass on exceptions thrown in button verifier method
			foreach (var idAndButton in slide.Buttons)
			{
				try
				{
					VerifyButton(idAndButton.Key, idAndButton.Value);
				}
				catch (SlidesJsonException e)
				{
					throw new SlidesJsonException($"At Slide \"{id}\", in buttons: ", e);
				}
			}
		}

	}

	public void VerifyButton(string id, JsonButton button)
	{
		// type can't be null
		if (button.Type is null)
		{
			throw new SlidesJsonException($"At Button \"{id}\": \"Type\" undefined");
		}
		if (!ButtonTypeOptions.Contains(button.Type))
		{
			throw new SlidesJsonException($"At Button \"{id}\": \"{button.Type}\" is not a valid type option");
		}
		if (button.Points is null)
		{
			throw new SlidesJsonException($"At Button \"{id}\": \"Points\" undefined");
		}
		if (button.Type == "image")
		{
			if (button.Image is null)
			{
				throw new SlidesJsonException($"At Button \"{id}\": \"Type\" is \"image\" and \"Image\" undefined");
			}
		}
		if (button.Actions is null)
		{
			throw new SlidesJsonException($"At Button \"{id}\": \"Actions\" undefined");
		}
		try
		{
			VerifyActions(button.Actions);
		}
		catch (SlidesJsonException e)
		{
			throw new SlidesJsonException($"At button \"{id}\" in actions: ", e);
		}
	}

	public void VerifyActions(List<List<string>> actions)
	{
		// to make sure that current state is always set
		if (CurrentState is null)
		{
			throw new Exception("State must be set before calling VerifyActions. Consider calling Init before");
		}
		List<string> blockStarts = [];
		List<string> blockEnds = [];
		for (int i = 0; i < actions.Count; i++)
		{
			var action = actions[i];
			// there is no action that takes different number than 2/3 params
			if (action.Count > 3 || action.Count < 2)
			{
				throw new SlidesJsonException($"At action {i}: To many or to few params");
			}

			// check for the actions that require 1 param, throw ex if more params required
			if (action.Count == 2)
			{
				if (action[0] == "Route")
				{
					// if Slide to route to doesn't exist
					if (!CurrentState.ContainsKey(action[1]))
					{
						throw new SlidesJsonException($"At action {i}: Route: No Slide with id \"{action[1]}\" found");
					}
					continue;
				}
				else if (action[0] == "AddItem" || action[0] == "RemoveItem")
				{
					// check if item exists
					if (!items.DoesItemExist(action[1]))
					{
						throw new SlidesJsonException(
							$"At action {i}: {(action[0] == "AddItem" ? "AddItem" : "RemoveItem")}: "
							+ $"No item with id \"{action[1]}\" found"
						);
					}
					continue;
				}
				else if (action[0] == "RequireItem")
				{
					var x = action[1].StartsWith('!') ? action[1][1..] : action[1];
					if (!items.DoesItemExist(x))
					{
						throw new SlidesJsonException($"At action {i}: RequireItem: No item with id \"{x}\" found");
					}
					continue;
				}
				else if (action[0] == "RequireGameState")
				{
					var x = action[1].StartsWith('!') ? action[1][1..] : action[1];
					if (!gameState.CheckForVisibility(x))
					{
						throw new SlidesJsonException($"At action {i}: RequireGameState: No GameState with key \"{x}\" found");
					}
					continue;
				}
				else if (action[0] == "StartBlock")
				{
					blockStarts.Add(action[1]);
					continue;
				}
				else if (action[0] == "EndBlock")
				{
					blockEnds.Add(action[1]);
					continue;
				}
				else if (action[0] == "Exit")
				{
					// do nothing here, as the params don't actually matter
				}
				else
				{
					// if not found yet, it has to be invalid action
					throw new SlidesJsonException($"At action {i}: Unknown Action \"{action[0]}\"");
				}
			}
			// acitons with 3 params
			else if (action.Count == 3)
			{
				if (action[0] == "SetGameState")
				{
					// check if gamestate exists
					// var x = action[1].StartsWith('!') ? action[1][1..] : action[1];
					if (!gameState.CheckForVisibility(action[1]))
					{
						throw new SlidesJsonException($"At action {i}: SetGameState: No GameState with key \"{action[1]}\" found");
					}
					if (!SetGameStateOptions.Contains(action[2]))
					{
						throw new SlidesJsonException($"At action {i}: SetGameState: \"{action[2]}\" is not a possible param");
					}
					continue;
				}
				else
				{
					throw new SlidesJsonException($"At action {i}: Unknown Action \"{action[0]}\"");
				}
			}
		}
		// check if the StartBlock match the EndBlock
		if (!(blockStarts.Count == blockEnds.Count))
		{
			throw new SlidesJsonException($"Block mismatch: Not the same amount of StartBlock and EndBlock");
		}
		foreach (var x in blockStarts)
		{
			if (!blockEnds.Contains(x))
			{
				throw new SlidesJsonException($"Block mismatch: StartBlock \"{x}\" has no matching EndBlock");
			}
		}
		foreach (var x in blockEnds)
		{
			if (!blockStarts.Contains(x))
			{
				throw new SlidesJsonException($"Block mismatch: EndBlock \"{x}\" has no matching StartBlock");
			}
		}
	}
}
