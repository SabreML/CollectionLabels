using MoreSlugcats;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CollectionLabels
{
	public static class LinearChatlogHelper
	{
		// Dictionary of game regions to a list of every 'linear' chatlog inside them.
		public static Dictionary<string, List<ChatlogData.ChatlogID>> AllChatlogs;
		// Dictionary of game regions to a list of uncollected 'linear' chatlogs inside them.
		public static Dictionary<string, List<ChatlogData.ChatlogID>> UncollectedChatlogs;

		// Has the player talked to Five Pebbles yet?
		public static bool PlayerIsPostPebbles;

		// Have all of the above fields been successfully loaded?
		public static bool Loaded;

		public static bool Load()
		{
			Loaded = false;

			Debug.Log("(CollectionLabels) Attempting to load Spearmaster save data...");
			if (!TryLoadSaveData(out DeathPersistentSaveData deathPersistentSaveData, out MiscWorldSaveData miscWorldSaveData))
			{
				Debug.Log("(CollectionLabels) Load failed. :("); // :(
				return false;
			}
			// Just in case something's wrong with the token cache, since it sometimes needs to be regenerated.
			if (RWCustom.Custom.rainWorld.regionGreyTokens.Count == 0)
			{
				// Log the exception to `exceptionLog.txt` since this is a problem, but don't break anything by throwing it.
				Debug.LogException(new System.Exception("(CollectionLabels) Token cache error!"));
				return false;
			}

			ParseData(deathPersistentSaveData, miscWorldSaveData);
			Debug.Log("(CollectionLabels) Success!");
			Loaded = true;

			return true;
		}

		private static void ParseData(DeathPersistentSaveData deathPersistentSaveData, MiscWorldSaveData miscWorldSaveData)
		{
			PlayerIsPostPebbles = miscWorldSaveData.SSaiConversationsHad > 0;
			List<string> spearmasterRegions = SlugcatStats.SlugcatStoryRegions(MoreSlugcatsEnums.SlugcatStatsName.Spear);

			// Make a new dict based on `regionGreyTokens` with only the spearmaster regions, and with all 'unique' (coloured) chatlogs removed.
			AllChatlogs = RWCustom.Custom.rainWorld.regionGreyTokens
				// Filter out any non-spearmaster regions.
				.Where(pair => spearmasterRegions.Contains(pair.Key.ToUpper()))
				// Replace each pair with a new `KeyValuePair` with any 'unique' (coloured) chatlogs filtered out of the `pair.Value` list.
				// (`KeyValuePair.Value` is read-only so it can't be modified directly.)
				.Select(pair => new KeyValuePair<string, List<ChatlogData.ChatlogID>>(
					pair.Key,
					pair.Value.Where(id => !ChatlogData.HasUnique(id)).ToList()
				))
				// If any of the region token lists are empty now after the the `Select()`, filter them out.
				.Where(pair => pair.Value.Count > 0)
				// Convert the `IEnumerable<KeyValuePair<...>>` result of all of that into a `Dictionary`.
				.ToDictionary(pair => pair.Key, pair => pair.Value);

			// Copy that dictionary to a new one with any chatlogs that the player has collected removed from it,
			// then filter out any region entries that have been emptied as a result, so that only the uncollected chatlogs remain.
			UncollectedChatlogs = AllChatlogs
				// Make a new `KeyValuePair` for each pair with any collected chatlogs filtered out of the `pair.Value` list.
				// (Same as above, can't edit the pair directly so a new one has to be made.)
				.Select(pair => new KeyValuePair<string, List<ChatlogData.ChatlogID>>(
					pair.Key,
					pair.Value.Except(deathPersistentSaveData.chatlogsRead).ToList()
				))
				// Same as above.
				.Where(pair => pair.Value.Count > 0)
				// `IEnumerable` -> `Dictionary`.
				.ToDictionary(pair => pair.Key, pair => pair.Value);
		}

		private static bool TryLoadSaveData(out DeathPersistentSaveData deathPersistentSaveData, out MiscWorldSaveData miscWorldSaveData)
		{
			deathPersistentSaveData = new(MoreSlugcatsEnums.SlugcatStatsName.Spear);
			miscWorldSaveData = new(MoreSlugcatsEnums.SlugcatStatsName.Spear);

			// Get the save data currently stored in memory.
			string[] progressionLines = RWCustom.Custom.rainWorld.progression.GetProgLinesFromMemory();
			string spearmasterSaveData = null;
			foreach (string line in progressionLines)
			{
				// Split each line to its 'category' `splitLine[0]`, and that category's data `splitLine[1]`.
				string[] splitLine = Regex.Split(line, "<progDivB>");

				// If this category has data, is a 'SAVE STATE', and specifically is Spearmaster's save state.
				if (splitLine.Length == 2 && splitLine[0] == "SAVE STATE"
					&& BackwardsCompatibilityRemix.ParseSaveNumber(splitLine[1]) == MoreSlugcatsEnums.SlugcatStatsName.Spear
				)
				{
					spearmasterSaveData = splitLine[1];
				}
			}
			// If the save doesn't have a spearmaster entry. (The player hasn't started the campaign yet)
			if (spearmasterSaveData == null)
			{
				return false;
			}

			// Has `deathPersistentSaveData` been successfully found and loaded?
			bool loadedPersistentData = false;
			// Has `miscWorldSaveData` been successfully found and loaded?
			bool loadedMiscWorldData = false;

			// Split the big text blob of save data into separate chunks.
			foreach (string line in Regex.Split(spearmasterSaveData, "<svA>"))
			{
				// Same idea as above. Split each line of the save data into a 'category' `[0]`, and its contents `[1]`.
				string[] splitLine = Regex.Split(line, "<svB>");
				if (splitLine.Length < 2)
				{
					continue;
				}
				if (splitLine[0] == "DEATHPERSISTENTSAVEDATA")
				{
					deathPersistentSaveData.FromString(splitLine[1]);
					loadedPersistentData = true;
				}
				else if (splitLine[0] == "MISCWORLDSAVEDATA")
				{
					miscWorldSaveData.FromString(splitLine[1]);
					loadedMiscWorldData = true;
				}
			}

			// Return `true` if both of these bools are `true` themselves.
			return loadedPersistentData && loadedMiscWorldData;
		}
	}
}
