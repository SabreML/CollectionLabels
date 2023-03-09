using BepInEx;
using Menu;
using MoreSlugcats;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace CollectionLabels
{
	[BepInPlugin("sabreml.collectionlabels", "CollectionLabels", "1.0.0")]
	public class CollectionLabelsMod : BaseUnityPlugin
	{
		private List<string> pearlEntryNames;
		private List<string> chatlogEntryNames;

		private MenuLabel entryNameLabel;

		public void OnEnable()
		{
			On.MoreSlugcats.CollectionsMenu.ctor += CollectionsMenuHK;
			On.MoreSlugcats.CollectionsMenu.ShutDownProcess += ShutDownProcessHK;
			On.MoreSlugcats.CollectionsMenu.Singal += SingalHK;
		}

		private void CollectionsMenuHK(On.MoreSlugcats.CollectionsMenu.orig_ctor orig, CollectionsMenu self, ProcessManager manager)
		{
			orig(self, manager);

			// Centered horizontally.
			float labelX = self.textBoxBorder.pos.x + self.textBoxBorder.size.x / 2f;
			// Near the top vertically.
			float labelY = self.textBoxBorder.pos.y + self.textBoxBorder.size.y - 30f;
			entryNameLabel = new(self, self.pages[0], "", new Vector2(labelX, labelY), Vector2.zero, true);
			self.pages[0].subObjects.Add(entryNameLabel);

			if (pearlEntryNames == null || chatlogEntryNames == null)
			{
				LoadEntryNames(self);
			}
		}

		private void ShutDownProcessHK(On.MoreSlugcats.CollectionsMenu.orig_ShutDownProcess orig, CollectionsMenu self)
		{
			orig(self);
			entryNameLabel = null;
		}

		private void LoadEntryNames(CollectionsMenu self)
		{
			LoadPearlNames(self);
			LoadChatlogNames(self);

			Dictionary<string, int> entryOccurrences = new();

			for (int i = 0; i < pearlEntryNames.Count; i++)
			{
				string pearlName = pearlEntryNames[i];
				Debug.Log(pearlName);
				if (!entryOccurrences.TryGetValue(pearlName, out int value))
				{
					entryOccurrences[pearlName] = 1;
					pearlEntryNames[i] = $"{pearlName}]";
				}
				else
				{
					entryOccurrences[pearlName]++;
					pearlEntryNames[i] = $"{pearlName} {value + 1}]";
				}
			}
			for (int i = 0; i < chatlogEntryNames.Count; i++)
			{
				string chatlogName = chatlogEntryNames[i];
				Debug.Log(chatlogName);
				if (!entryOccurrences.TryGetValue(chatlogName, out int value))
				{
					entryOccurrences[chatlogName] = 1;
				}
				else
				{
					entryOccurrences[chatlogName]++;
				}
				chatlogEntryNames[i] = $"{chatlogName} {value + 1}]";
			}
		}

		private void LoadPearlNames(CollectionsMenu self)
		{
			pearlEntryNames = new();

			foreach (DataPearl.AbstractDataPearl.DataPearlType pearlType in self.usedPearlTypes)
			{
				// The region this pearl is found in. ("SL_moon" > "SL" > "Shoreline")
				// (Unless it's a campaign pearl inside a slugcat, then it gets set to their name.)
				string pearlName = pearlType.value switch
				{
					"Red_stomach" => "Hunter",
					"Spearmasterpearl" => "Spearmaster",
					"Rivulet_stomach" => "Rivulet",
					_ => Region.GetRegionFullName(pearlType.value.Split('_')[0], null)
				};

				pearlEntryNames.Add($"[{pearlName} pearl");
			}
		}

		private void LoadChatlogNames(CollectionsMenu self)
		{
			chatlogEntryNames = new();

			for (int i = 0; i < self.prePebsBroadcastChatlogs.Count; i++)
			{
				chatlogEntryNames.Add($"[Live broadcast (Pre-event)");
			}
			for (int i = 0; i < self.postPebsBroadcastChatlogs.Count; i++)
			{
				chatlogEntryNames.Add("[Live broadcast (Post-event)");
			}

			foreach (ChatlogData.ChatlogID chatlogID in self.usedChatlogs)
			{
				string regionAcronym = chatlogID.value.Substring(chatlogID.value.Length - 3, 2);
				string chatlogName = Region.GetRegionFullName(regionAcronym, null);

				chatlogEntryNames.Add($"[{chatlogName} transmission");
			}
		}

		private void SingalHK(On.MoreSlugcats.CollectionsMenu.orig_Singal orig, CollectionsMenu self, MenuObject sender, string message)
		{
			orig(self, sender, message);

			// Pearl button or iterator button.
			if (message.Contains("PEARL") || message.Contains("TYPE"))
			{
				DataPearl.AbstractDataPearl.DataPearlType selectedPearl = self.usedPearlTypes[self.selectedPearlInd];

				// Set the label's colour to the pearl's in-game colour (or highlight colour).
				Color labelColor = DataPearl.UniquePearlMainColor(selectedPearl);

				// Lazy "Is this colour too dark to be used in text" check.
				if (labelColor.r + labelColor.g + labelColor.b <= 0.3f)
				{
					// Try to use the highlight colour instead (if it exists), since those are usually brighter.
					labelColor = DataPearl.UniquePearlHighLightColor(selectedPearl) ?? labelColor;
				}
				entryNameLabel.label.color = labelColor;

				// Set the label's text based on the selected pearl type.
				entryNameLabel.text = pearlEntryNames[self.selectedPearlInd];
			}

			// Chatlog button.
			else if (message.Contains("CHATLOG"))
			{
				// The singal message contains the chatlog's index in its respective list.
				int chatlogIndex = int.Parse(message.Substring(message.LastIndexOf("_") + 1));

				// Set the colour of the text to whatever the icon is using.

				// Get the `ChatlogID` value from whichever list the chatlog is from, and set the label's text based on that.
				if (message.Contains("POSTPEB"))
				{
					chatlogIndex += self.prePebsBroadcastChatlogs.Count;
				}
				else if (message.Contains("NORMAL"))
				{
					chatlogIndex += self.prePebsBroadcastChatlogs.Count + self.postPebsBroadcastChatlogs.Count;
				}
				entryNameLabel.label.color = self.chatlogSprites[chatlogIndex].color;
				entryNameLabel.text = chatlogEntryNames[chatlogIndex];
			}
		}
	}
}
