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
	[BepInPlugin("sabreml.collectionlabels", "CollectionLabels", VERSION)]
	public class CollectionLabelsMod : BaseUnityPlugin
	{
		public const string VERSION = "1.1.3";

		// A list of pearl names/locations. (E.g. "[Shoreline pearl 1]", "[Chimney Canopy pearl]", etc.)
		private List<string> pearlNames;
		// A list of chatlog names/locations. (E.g. "[Sky Islands transmission 2]", "[Garbage Wastes transmission 1]", etc.)
		private List<string> chatlogNames;

		// The label displaying the selected entry's name.
		private MenuLabel nameLabel;
		// A UI element that displays a list of regions which contain a white/grey 'linear' chatlog.
		private ChatlogRegionList chatlogRegionList;

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
			float labelX = self.textBoxBorder.pos.x + (self.textBoxBorder.size.x / 2f); // Centred horizontally.
			float labelY = self.textBoxBorder.pos.y + (self.textBoxBorder.size.y - 30f); // Near the top vertically.
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
					// They both make the button appear greyed out, but `inactive` keeps it clickable too.
					button.GetButtonBehavior.greyedOut = false;
					button.inactive = true;
				}
			}

			// Try to load the Spearmaster's save data into `LinearChatlogHelper` in order to track white/grey broadcasts.
			LinearChatlogHelper.Load();
			// If the load failed. (Usually because there's no SM savegame yet.)
			if (!LinearChatlogHelper.Loaded)
			{
				Debug.Log("(CollectionLabels) White/grey chatlog region list is unavailable.");
			}
		}

		private void ShutDownProcessHK(On.MoreSlugcats.CollectionsMenu.orig_ShutDownProcess orig, CollectionsMenu self)
		{
			orig(self);
			// Clean up the UI elements when the menu closes.
			nameLabel = null;
			RemoveChatlogRegionList(self);
		}

		private void LoadPearlNames(CollectionsMenu self)
		{
			pearlNames = new();

			foreach (DataPearl.AbstractDataPearl.DataPearlType pearlType in self.usedPearlTypes)
			{
				// The region this pearl is found in. ("SL_moon" > "SL" > "Shoreline")
				// (Some like the music pearl or scug starting pearls are set manually.)
				string pearlName = pearlType.value switch
				{
					"RM" => "Music",
					"Red_stomach" => "Hunter",
					"Spearmasterpearl" => "Spearmaster",
					"Rivulet_stomach" => "Rivulet",
					"MS" => "Garbage Wastes", // Interestingly, this one seems to be mislabeled in the game's code. (It appears in GW, not MS)
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
				chatlogNames.Add("[Live broadcast (Pre-event)");
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
			List<string> outputList = new();
			foreach (string name in inputList)
			{
				// The number of times this entry name occurs in `inputList`.
				int inputListOccurrences = inputList.Count(x => x == name);
				// The number of times an entry *containing* this name occurs in `outputList`. (The name is modified so just `==` won't work.)
				int outputListOccurrences = outputList.Count(x => x.Contains(name));

				// If there's more than 1 pearl/chatlog with this name, append a number onto the end.
				// This changes ("[Shoreline pearl", "[Shoreline pearl") into ("[Shoreline pearl 1]", "[Shoreline pearl 2]")
				if (inputListOccurrences > 1)
				{
					outputList.Add($"{name} {outputListOccurrences + 1}]");
				}
				// Otherwise, just leave it as-is and add the closing bracket.
				else
				{
					outputList.Add($"{name}]");
				}
			}

			return outputList;
		}

		private void SingalHK(On.MoreSlugcats.CollectionsMenu.orig_Singal orig, CollectionsMenu self, MenuObject sender, string message)
		{
			orig(self, sender, message);

			// Pearl button or iterator button.
			if (message.Contains("PEARL") || message.Contains("TYPE"))
			{
				HandlePearlSingal(self);
			}

			// Chatlog button.
			else if (message.Contains("CHATLOG"))
			{
				HandleChatlogSingal(self, sender);
			}

			// If the clicked button isn't unlocked.
			if (sender.inactive)
			{
				// Remove and reset the main text.
				self.ResetLabels();
				self.labels[0].text = self.Translate("[ Collection Empty ]");
				self.RefreshLabelPositions();
				// (Removing the pearl/chatlog text afterwards like this is easier than preventing it from loading in the first place)
			}
		}

		private void HandlePearlSingal(CollectionsMenu self)
		{
			// Remove the region list if it's there.
			RemoveChatlogRegionList(self);

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

		private void HandleChatlogSingal(CollectionsMenu self, MenuObject sender)
		{
			int chatlogIndex = self.chatlogButtons.IndexOf(sender);
			// The first `prePebsBroadcastChatlogs.Count + postPebsBroadcastChatlogs.Count` number of buttons are assigned
			// to the grey/white broadcasts, so if the index is within that range it's one of them.
			bool isLinearChatlog = chatlogIndex < self.prePebsBroadcastChatlogs.Count + self.postPebsBroadcastChatlogs.Count;

			// Set the text colour to grey if it isn't unlocked yet, or the colour of the button's sprite if it is.
			nameLabel.label.color = sender.inactive ? Color.grey : self.chatlogSprites[chatlogIndex].color;
			nameLabel.text = chatlogNames[chatlogIndex];

			// If `LinearChatlogHelper` couldn't load any Spearmaster save data, nothing past this point would work properly.
			if (!LinearChatlogHelper.Loaded)
			{
				// So just return instead.
				return;
			}

			// If it's a regular chatlog with a set location, or it's already been collected by the player, hide the region list.
			if (!isLinearChatlog || !sender.inactive)
			{
				// Don't show the region list.
				RemoveChatlogRegionList(self);
				return;
			}

			// Create the region list
			MakeChatlogRegionList(self);

			bool selectedLogIsPrePebs = chatlogIndex < self.prePebsBroadcastChatlogs.Count;
			bool selectedLogIsPostPebs = chatlogIndex >= self.prePebsBroadcastChatlogs.Count;

			// If it's a pre/post-pebbles exclusive chatlog and the player is in the other part of the story, then they can't collect it even if they want to.
			if ((LinearChatlogHelper.PlayerIsPostPebbles && selectedLogIsPrePebs) || (!LinearChatlogHelper.PlayerIsPostPebbles && selectedLogIsPostPebs))
			{
				// Disable the region list, and log the reason just in case anyone gets confused.
				chatlogRegionList.SetAvailable(false);
				Debug.Log($"(CollectionLables) Chatlog {chatlogNames[chatlogIndex]} is not able to be collected by the player due to story progression.");
			}
			else
			{
				// Otherwise, set the list back to its original state.
				chatlogRegionList.SetAvailable(true);
			}
		}

		private void MakeChatlogRegionList(CollectionsMenu menu)
		{
			// If it's already been made, return.
			if (chatlogRegionList != null)
			{
				return;
			}

			// Set the region list's initial position to halfway across the collections menu text box, and 60 down from the top of it.
			Vector2 regionListPos = new(
				menu.textBoxBorder.pos.x + (menu.textBoxBorder.size.x / 2f),
				menu.textBoxBorder.pos.y + menu.textBoxBorder.size.y - 60f
			);

			// Create the list.
			chatlogRegionList = new(menu, menu.pages[0], regionListPos, new Vector2(380f, 125f), false);
			// Adjust its position so that the centre of the list is at `regionListPos` rather than the corner.
			chatlogRegionList.pos.x -= chatlogRegionList.size.x / 2f;
			chatlogRegionList.pos.y -= chatlogRegionList.size.y;
			// Add it to the collections menu's `subObjects` list.
			menu.pages[0].subObjects.Add(chatlogRegionList);
		}

		private void RemoveChatlogRegionList(CollectionsMenu menu)
		{
			// If it isn't actually there to remove, return.
			if (chatlogRegionList == null)
			{
				return;
			}
			chatlogRegionList.RemoveSprites();
			menu.pages[0].RemoveSubObject(chatlogRegionList);
			chatlogRegionList = null;
		}
	}
}
