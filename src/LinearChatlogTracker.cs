using MoreSlugcats;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CollectionLabels
{
	public readonly struct LinearChatlogTracker
	{
		public readonly Dictionary<string, List<ChatlogData.ChatlogID>> UncollectedChatlogs;

		public readonly bool PostPebbles;

		private LinearChatlogTracker(DeathPersistentSaveData persistentSaveData, MiscWorldSaveData miscWorldSaveData)
		{
			PostPebbles = miscWorldSaveData.SSaiConversationsHad > 0;

			string[] spearmasterRegions = SlugcatStats.getSlugcatStoryRegions(MoreSlugcatsEnums.SlugcatStatsName.Spear);
			// Make a new dict based on `regionGreyTokens`, only with spearmaster regions, and with all 'unique' (coloured) chatlogs removed.
			Dictionary<string, List<ChatlogData.ChatlogID>> regionLinearTokens = RWCustom.Custom.rainWorld.regionGreyTokens
				.Where(pair => spearmasterRegions.Contains(pair.Key))
				.ToDictionary(
					pair => pair.Key,
					pair => pair.Value
						.Where(id => !ChatlogData.HasUnique(id))
						.ToList()
				);

			// Copy that dictionary to a new one with any chatlogs the player has already collected removed from it,
			// so that only the uncollected entries remain.
			UncollectedChatlogs = regionLinearTokens
				.ToDictionary(
					pair => pair.Key,
					pair => pair.Value.Except(persistentSaveData.chatlogsRead).ToList()
				);
		}

		public static LinearChatlogTracker? TryCreateTracker()
		{
			if (!TryLoadSaveData(out DeathPersistentSaveData deathPersistentSaveData, out MiscWorldSaveData miscWorldSaveData))
			{
				return null;
			}
			// Just in case something's wrong with the token cache, since it sometimes needs to regenerate itself.
			if (RWCustom.Custom.rainWorld.regionGreyTokens.Count == 0)
			{
				return null;
			}

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
