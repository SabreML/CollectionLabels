using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using System.Linq;
using UnityEngine;

namespace CollectionLabels
{
	public class ChatlogRegionList : RoundedRect
	{
		// The number of entries in each column of the region list.
		private const int labelColumnLength = 5;
		// The default text for the 'available' state of the button.
		private const string defaultText = "[SHOW REMAINING BROADCAST LOCATIONS]";

		// A button which is used to hide the actual region list until it's held down.
		// Also used for unavailable chatlogs, greyed out and with its text set to '[UNAVAILABLE]'.
		private readonly OpHoldButton showListButton;

		// The `MenuLabel`s in the left column of the region list.
		private readonly MenuLabel[] leftRegionLabels;
		// The `MenuLabel`s in the right column of the region list.
		private readonly MenuLabel[] rightRegionLabels;

		// Every `MenuLabel` from the above two arrays concatenated into one, for easier looping.
		private readonly MenuLabel[] allRegionlabels;

		public ChatlogRegionList(Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size, bool filled) : base(menu, owner, pos, size, filled)
		{
			// Set the colour of the list's borders to a medium grey.
			this.borderColor = Menu.Menu.MenuColor(Menu.Menu.MenuColors.MediumGrey);

			// Create the hold button inside the region list of the same size and with no position offset, so that it acts as a sort of overlay.
			showListButton = new(Vector2.zero, size, defaultText, 50f)
			{
				description = "Hold to display all regions with uncollected broadcast tokens", // Hover text.
				colorEdge = Color.white
			};
			// After the button has been held down, (indirectly) call `ShowRegionNames()`.
			showListButton.OnPressDone += (_) =>
			{
				showListButton.Hide();
				ShowRegionNames();
			};

			// Because `OpHoldButton` is designed for an `OptionInterface` (indicated by the 'Op' in its name, I think), it needs a
			// `UIelementWrapper` in order to function outside of one, and that itself needs a `MenuTabWrapper`.

			// Create a new `MenuTabWrapper`, and a new `UIelementWrapper` assigned to it containing the `showListButton`.
			MenuTabWrapper menuTabWrapper = new(menu, this);
			new UIelementWrapper(menuTabWrapper, showListButton);
			// Then add the `MenuTabWrapper` to the region list's `subObjects` so that it's rendered.
			subObjects.Add(menuTabWrapper);

			// Initialise both `MenuLabel` arrays with blank labels.
			InitLabelArray(out leftRegionLabels, -100f); // `xOffset` arg of `-100f`, or 100 to the left.
			InitLabelArray(out rightRegionLabels, 100f); // 100 to the right.

			// Set `allRegionLabels` to a combined array of them both.
			allRegionlabels = leftRegionLabels.Concat(rightRegionLabels).ToArray();
		}

		// Enable or disable the 'available' state of the region list. Used for linear chatlogs that can't be collected by the player.
		public void SetAvailable(bool available)
		{
			if (available)
			{
				// Set the button back to normal.
				showListButton.greyedOut = false;
				showListButton.text = defaultText;
			}
			else
			{
				// Hide the region labels, grey-out, reset, and set the text of the button, and make it visible again.
				HideRegionNames();
				showListButton.greyedOut = true;
				showListButton.text = "[UNAVAILABLE]";
				showListButton._glow.Hide();
				showListButton.Show();
			}
		}

		// Fills the region labels with the names of every region that contains linear chatlog collectibles.
		// Called by `showListButton`'s `OnPressDone` event.
		private void ShowRegionNames()
		{
			// The name acronyms of every region that has a white/grey 'linear' chatlog inside of it.
			string[] regionAcronyms = LinearChatlogHelper.AllChatlogs.Keys.ToArray();

			// For each region to display:
			for (int i = 0; i < regionAcronyms.Length; i++)
			{
				// If the index is within the length of `leftRegionLabels`.
				if (i < leftRegionLabels.Length)
				{
					// Add it to the left column.
					FillRegionLabel(leftRegionLabels[i], regionAcronyms[i]);
				}
				// Else, if the index is within the maximum number of entries. (Left array + right array)
				else if (i < allRegionlabels.Length)
				{
					// Since `i` is higher than `leftRegionLabels.Length` here and both arrays have the same length,
					// subtracting `labelColumnLength` will give the index of where it should go in `rightRegionLabels`.
					int rightArrayIndex = i - labelColumnLength;
					FillRegionLabel(rightRegionLabels[rightArrayIndex], regionAcronyms[i]);
				}
				// Else, if the index is higher than the max number of entries.
				else
				{
					break;
				}
			}
		}

		// Sets the `text` of all region labels to an empty string.
		// Called by `SetAvailable()`, when the selected linear chatlog isn't collectable by the player.
		private void HideRegionNames()
		{
			foreach (MenuLabel label in allRegionlabels)
			{
				label.text = "";
			}
		}

		private void FillRegionLabel(MenuLabel label, string regionAcronym)
		{
			// 'su' -> 'Outskirts', 'lf' -> 'Farm Arrays', etc.
			string fullName = Region.GetRegionFullName(regionAcronym, MoreSlugcatsEnums.SlugcatStatsName.Spear);

			// If the region contains linear chatlogs that the player hasn't collected yet.
			if (LinearChatlogHelper.UncollectedChatlogs.TryGetValue(regionAcronym, out _))
			{
				// Set the label to the region name with an unticked box.
				label.text = "[ ] " + fullName;
			}
			// If the player has collected every linear chatlog in the region.
			else
			{
				// Set the label to the region name with a ticked box.
				label.text = "[x] " + fullName;
				// Set the label's colour to a darker colour so that it looks properly crossed out.
				label.label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.DarkGrey);
			}
		}

		// Create and fill a 'label array' (`leftRegionLabels`/`rightRegionLabels`) with blank labels.
		private void InitLabelArray(out MenuLabel[] labelArray, float xOffset)
		{
			// Initialise the array with a max size of `labelColumnLength`.
			labelArray = new MenuLabel[labelColumnLength];

			// How much extra space to add above each label.
			// (Given an initial value of 20 so that the first one isn't touching the top of the region list.)
			float posYSpacing = 20f;

			// For each empty space in the array:
			for (int i = 0; i < labelArray.Length; i++)
			{
				// Create a new blank label halfway across, and `posYSpacing` down from the top of the region list.
				labelArray[i] = new(menu, this, "", new Vector2((this.size.x / 2f) + xOffset, this.size.y - posYSpacing), Vector2.zero, false);
				subObjects.Add(labelArray[i]);

				// Increase the spacing by 20 so that each label is placed below the previous one.
				posYSpacing += 20f;
			}
		}
	}
}
