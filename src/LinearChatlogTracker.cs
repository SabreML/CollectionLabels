using MoreSlugcats;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

#nullable enable annotations

namespace CollectionLabels
{
	public class LinearChatlogTracker
	{
		public static Dictionary<string, List<ChatlogData.ChatlogID>> AllChatlogs;
		public static Dictionary<string, List<ChatlogData.ChatlogID>> UncollectedChatlogs;

		public static bool PlayerIsPostPebbles;

		private LinearChatlogTracker(DeathPersistentSaveData persistentSaveData, MiscWorldSaveData miscWorldSaveData)
		{
			PlayerIsPostPebbles = miscWorldSaveData.SSaiConversationsHad > 0;

			string[] spearmasterRegions = SlugcatStats.getSlugcatStoryRegions(MoreSlugcatsEnums.SlugcatStatsName.Spear);

			// Make a new dict based on `regionGreyTokens` with only the spearmaster regions, and with all 'unique' (coloured) chatlogs removed.
			AllChatlogs = RWCustom.Custom.rainWorld.regionGreyTokens
				.Where(pair => spearmasterRegions.Contains(pair.Key.ToUpper()))
				.ToDictionary(
					pair => pair.Key,
					pair => pair.Value
						.Where(id => !ChatlogData.HasUnique(id))
						.ToList()
				);

			// Copy that dictionary to a new one with any chatlogs that the player has collected removed from it,
			// then filter out any region entries that have been emptied as a result, so that only the uncollected chatlogs remain.
			UncollectedChatlogs = AllChatlogs
				// Make a new `KeyValuePair` for each pair with any collected chatlogs filtered out of the `pair.Value` list.
				// (`KeyValuePair.Value` is read-only so it can't just be filtered directly.)
				.Select(pair => new KeyValuePair<string, List<ChatlogData.ChatlogID>>(
					pair.Key,
					pair.Value.Except(persistentSaveData.chatlogsRead).ToList()
				))
				// If any of the region token lists are empty now as a result of the `Except()`, filter them out.
				.Where(pair => pair.Value.Count > 0)
				// `IEnumerable` -> `Dictionary`.
				.ToDictionary(pair => pair.Key, pair => pair.Value);
		}

		public static LinearChatlogTracker? TryCreateTracker()
		{
			Debug.Log("(CollectionLabels) Attempting to load Spearmaster save data...");
			if (!TryLoadSaveData(out DeathPersistentSaveData deathPersistentSaveData, out MiscWorldSaveData miscWorldSaveData))
			{
				Debug.Log("(CollectionLabels) Load failed. :(");
				return null;
			}
			// Just in case something's wrong with the token cache, since it sometimes needs to be regenerated.
			if (RWCustom.Custom.rainWorld.regionGreyTokens.Count == 0)
			{
				// Log to `exceptionLog.txt`, but don't actually throw an error.
				Debug.LogException(new System.Exception("(CollectionLabels) Token cache error!"));
				return null;
			}

			Debug.Log("(CollectionLabels) Success!");
			return new LinearChatlogTracker(deathPersistentSaveData, miscWorldSaveData);
		}

		private static bool TryLoadSaveData(out DeathPersistentSaveData deathPersistentSaveData, out MiscWorldSaveData miscWorldSaveData)
		{
			deathPersistentSaveData = new(MoreSlugcatsEnums.SlugcatStatsName.Spear);
			miscWorldSaveData = new(MoreSlugcatsEnums.SlugcatStatsName.Spear);

			string[] progressionLines = RWCustom.Custom.rainWorld.progression.GetProgLinesFromMemory();

			string spearmasterSaveData = null;
			foreach (string line in progressionLines)
			{
				string[] splitLine = Regex.Split(line, "<progDivB>");
				if (splitLine.Length == 2
					&& splitLine[0] == "SAVE STATE"
					&& BackwardsCompatibilityRemix.ParseSaveNumber(splitLine[1]) == MoreSlugcatsEnums.SlugcatStatsName.Spear
				)
				{
					spearmasterSaveData = splitLine[1];
				}
			}
			if (spearmasterSaveData == null)
			{
				// Todo: Check if this is actually an issue (new saves without a sm game?)
				return false;
			}

			// Load categories
			bool loadedPersistentData = false;
			bool loadedMiscWorldData = false;
			string[] saveA = Regex.Split(spearmasterSaveData, "<svA>");
			foreach (string line in saveA)
			{
				string[] saveB = Regex.Split(line, "<svB>");
				if (saveB.Length < 2)
				{
					continue;
				}
				if (saveB[0] == "DEATHPERSISTENTSAVEDATA")
				{
					deathPersistentSaveData.FromString(saveB[1]);
					loadedPersistentData = true;
				}
				else if (saveB[0] == "MISCWORLDSAVEDATA")
				{
					miscWorldSaveData.FromString(saveB[1]);
					loadedMiscWorldData = true;
				}
			}

			return loadedPersistentData && loadedMiscWorldData;
		}
	}
}
