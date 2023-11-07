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
		private readonly OpHoldButton showListButton;

		private readonly MenuLabel[] leftRegionLabels = new MenuLabel[7];
		private readonly MenuLabel[] rightRegionLabels = new MenuLabel[7];

		private MenuLabel[] AllRegionlabels => leftRegionLabels.Concat(rightRegionLabels).ToArray();

		public ChatlogRegionList(Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size, bool filled) : base(menu, owner, pos, size, filled)
		{
			borderColor = Menu.Menu.MenuColor(Menu.Menu.MenuColors.MediumGrey);

			MenuTabWrapper menuTabWrapper = new(menu, this);
			subObjects.Add(menuTabWrapper);

			showListButton = new(Vector2.zero, size, "[SHOW REMAINING BROADCAST LOCATIONS]", 50f)
			{
				description = "Hold to display all regions with broadcast tokens",
				colorEdge = Color.white
			};
			showListButton.OnPressDone += ShowRegionNames;
			new UIelementWrapper(menuTabWrapper, showListButton);

			for (int i = 0; i < leftRegionLabels.Length; i++)
			{
				leftRegionLabels[i] = new(menu, this, "", Vector2.zero, Vector2.zero, false);
				subObjects.Add(leftRegionLabels[i]);

				rightRegionLabels[i] = new(menu, this, "", Vector2.zero, Vector2.zero, false);
				subObjects.Add(rightRegionLabels[i]);
			}
		}

		private void ShowRegionNames(UIfocusable trigger)
		{
			string[] regionAcronyms = LinearChatlogTracker.AllChatlogs.Keys.ToArray();

			int allLabelsLength = AllRegionlabels.Length;
			if (regionAcronyms.Length > allLabelsLength)
			{
				Debug.Log($"(CollectionLabels) Attempted to display more than {allLabelsLength} region names! Excess will be skipped.");
			}

			for (int i = 0; i < regionAcronyms.Length; i++)
			{
				if (i < leftRegionLabels.Length)
				{
					GetLabelName(leftRegionLabels[i], regionAcronyms[i]);
				}
				else if (i < allLabelsLength)
				{
					int wrappedIndex = i % rightRegionLabels.Length;
					GetLabelName(rightRegionLabels[wrappedIndex], regionAcronyms[i]);
				}
				else
				{
					break;
				}
			}

			showListButton.Hide();
			RefreshLabelPositions();
		}

		private void GetLabelName(MenuLabel label, string regionAcronym)
		{
			string fullName = Region.GetRegionFullName(regionAcronym, MoreSlugcatsEnums.SlugcatStatsName.Spear);

			if (LinearChatlogTracker.UncollectedChatlogs.TryGetValue(regionAcronym, out _))
			{
				label.text = "[x] " + fullName;
				label.label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.DarkGrey);
			}
			else
			{
				label.text = "[ ] " + fullName;
			}
		}

		private void RefreshLabelPositions()
		{
			float posX = this.size.x / 2f;
			float posY = this.size.y - 20f;

			for (int i = 0; i < leftRegionLabels.Length; i++)
			{
				MenuLabel leftLabel = leftRegionLabels[i];
				leftLabel.pos = new Vector2(posX - 100f, posY);

				MenuLabel rightLabel = rightRegionLabels[i];
				rightLabel.pos = rightLabel.lastPos = new Vector2(posX + 100f, posY);

				posY -= 20f;
			}
		}
	}
}
