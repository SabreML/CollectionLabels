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
		// A list of pearl names/locations. (E.g. "[Shoreline pearl 1]", "[Chimney Canopy pearl]", etc.)
		private List<string> pearlNames;
		// A list of chatlog names/locations. (E.g. "[Sky Islands transmission 2]", "[Garbage Wastes transmission 1]", etc.)
		private List<string> chatlogNames;

		// The label displaying the selected entry's name.
		private MenuLabel nameLabel;

		public void OnEnable()
		{
			On.MoreSlugcats.CollectionsMenu.ctor += CollectionsMenuHK;
			On.MoreSlugcats.CollectionsMenu.ShutDownProcess += ShutDownProcessHK;
			On.MoreSlugcats.CollectionsMenu.Singal += SingalHK;
		}

		private void CollectionsMenuHK(On.MoreSlugcats.CollectionsMenu.orig_ctor orig, CollectionsMenu self, ProcessManager manager)
		{
			orig(self, manager);

			// Add `nameLabel` to the menu.
			float labelX = self.textBoxBorder.pos.x + self.textBoxBorder.size.x / 2f; // Centered horizontally.
			float labelY = self.textBoxBorder.pos.y + self.textBoxBorder.size.y - 30f; // Near the top vertically.
			nameLabel = new(self, self.pages[0], "", new Vector2(labelX, labelY), Vector2.zero, true);
			self.pages[0].subObjects.Add(nameLabel);

			// Create the name lists.
			LoadPearlNames(self);
			LoadChatlogNames(self);

			// Loop through every pearl and chatlog button.
			foreach (SimpleButton button in self.pearlButtons.Concat(self.chatlogButtons))
			{
				if (button.GetButtonBehavior.greyedOut)
				{
					// Switch the button from `greyedOut` to `inactive`.
					// They both make the button greyed out, but `inactive` keeps it clickable too.
					button.GetButtonBehavior.greyedOut = false;
					button.inactive = true;
				}
			}
		}

		private void ShutDownProcessHK(On.MoreSlugcats.CollectionsMenu.orig_ShutDownProcess orig, CollectionsMenu self)
		{
			orig(self);
			// Clean up the name label when the menu closes.
			nameLabel = null;
		}

		private void LoadPearlNames(CollectionsMenu self)
		{
			pearlNames = new();

			foreach (DataPearl.AbstractDataPearl.DataPearlType pearlType in self.usedPearlTypes)
			{
				// The region this pearl is found in. ("SL_moon" > "SL" > "Shoreline")
				// (Unless it's the music pearl or a scug starting pearl, then it gets set manually.)
				string pearlName = pearlType.value switch
				{
					"RM" => "Music",
					"Red_stomach" => "Hunter",
					"Spearmasterpearl" => "Spearmaster",
					"Rivulet_stomach" => "Rivulet",
					_ => Region.GetRegionFullName(pearlType.value.Split('_')[0], null)
				};

				pearlNames.Add($"[{pearlName} pearl");
			}
			pearlNames = FormatListDuplicates(pearlNames);
		}

		private void LoadChatlogNames(CollectionsMenu self)
		{
			chatlogNames = new();

			for (int i = 0; i < self.prePebsBroadcastChatlogs.Count; i++)
			{
				chatlogNames.Add($"[Live broadcast (Pre-event)");
			}
			for (int i = 0; i < self.postPebsBroadcastChatlogs.Count; i++)
			{
				chatlogNames.Add("[Live broadcast (Post-event)");
			}

			foreach (ChatlogData.ChatlogID chatlogID in self.usedChatlogs)
			{
				// Each of these has its region at the end of its name. (E.g. "Chatlog_CC4")
				string regionAcronym = chatlogID.value.Substring(chatlogID.value.Length - 3, 2);
				string regionFullName = Region.GetRegionFullName(regionAcronym, null);

				chatlogNames.Add($"[{regionFullName} transmission");
			}
			chatlogNames = FormatListDuplicates(chatlogNames);
		}

		private static List<string> FormatListDuplicates(List<string> inputList)
		{
			return inputList
				.GroupBy(x => x) // Create groups of each distinct name.
				.SelectMany(g => g.Select((name, index) =>
				// Go through each item name in each group (and grab the index of the name too).
				{
					if (g.Count() > 1)
					{
						// If there's more than one of the same name, append index+1 to the end of it.
						// This changes "[Shoreline pearl", "[Shoreline pearl" into "[Shoreline pearl 1]", "[Shoreline pearl 2]"
						return $"{name} {index + 1}]";
					}
					else
					{
						// If there's only one instance of the name, just add the closing bracket.
						return $"{name}]";
					}
				})).ToList();
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

				// Lazy "Is this colour too dark to be used in text" check. (Looking at you SI pearls)
				if (labelColor.r + labelColor.g + labelColor.b <= 0.3f)
				{
					// Try to use the highlight colour instead (if it exists), since those are usually brighter.
					labelColor = DataPearl.UniquePearlHighLightColor(selectedPearl) ?? labelColor;
				}
				nameLabel.label.color = labelColor;

				// Set the label's text based on the selected pearl type.
				nameLabel.text = pearlNames[self.selectedPearlInd];
			}

			// Chatlog button.
			else if (message.Contains("CHATLOG"))
			{
				// The singal message contains the chatlog's index in its respective list.
				int chatlogIndex = int.Parse(message.Substring(message.LastIndexOf("_") + 1));

				if (message.Contains("POSTPEB"))
				{
					chatlogIndex += self.prePebsBroadcastChatlogs.Count;
				}
				else if (message.Contains("NORMAL"))
				{
					// TODO: Write a comment actually explaining why it's doing this index stuff.
					chatlogIndex += self.prePebsBroadcastChatlogs.Count + self.postPebsBroadcastChatlogs.Count;
				}

				// Set the text colour to grey if it isn't unlocked yet, or the colour of the button sprite if it is.
				nameLabel.label.color = sender.inactive ? Color.grey : self.chatlogSprites[chatlogIndex].color;
				nameLabel.text = chatlogNames[chatlogIndex];
			}

			// If the clicked button isn't unlocked.
			if (sender.inactive)
			{
				// Remove and reset the text.
				self.ResetLabels();
				self.labels[0].text = self.Translate("[ Collection Empty ]");
				self.RefreshLabelPositions();
				// (Removing it afterwards like this is easier than preventing it from loading in the first place)
			}
		}
	}
}
