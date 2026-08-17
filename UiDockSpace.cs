using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SimpleUI
{
	/// <summary>Named zone of a UiDockSpace that holds a tab group of dock panels.</summary>
	public enum DockZone
	{
		/// <summary>Left sidebar.</summary>
		Left = 0,
		/// <summary>Center workspace / viewport.</summary>
		Center = 1,
		/// <summary>Right sidebar.</summary>
		Right = 2,
		/// <summary>Bottom panel strip.</summary>
		Bottom = 3
	}

	/// <summary>Serializable snapshot of one dock zone: size weight, tab order, and selected panel.</summary>
	[Serializable]
	public sealed class DockZoneSnapshot
	{
		/// <summary>Which zone this snapshot describes.</summary>
		public DockZone Zone;
		/// <summary>Relative size weight of this zone (normalized against sibling zones on the same axis).</summary>
		public float Size;
		/// <summary>Panel ids in tab order.</summary>
		public string[] PanelIds;
		/// <summary>Currently selected panel id, or null if the zone is empty.</summary>
		public string SelectedPanelId;
	}

	/// <summary>Serializable snapshot of a UiDockSpace layout. The consumer owns persistence (e.g. layout.json); SimpleUI never writes this itself.</summary>
	[Serializable]
	public sealed class DockLayoutSnapshot
	{
		/// <summary>One entry per zone that currently has a size or panels.</summary>
		public DockZoneSnapshot[] Zones;
	}

	/// <summary>Root dockable container owning Left/Right/Bottom/Center zones of tabbed panels.</summary>
	/// <remarks>
	/// Panels are always docked — there is no floating/undocked state. Dragging a tab redocks it
	/// into a different zone's tab group. Layout is not auto-persisted; call
	/// <see cref="CaptureLayout"/> / <see cref="ApplyLayout"/> and let the consumer write the
	/// snapshot wherever it stores editor preferences.
	/// </remarks>
	public sealed class UiDockSpace : UiElement<UiDockSpace>, ITabDragHost
	{
		private const float MinPanePx = 80f;
		private const float CollapsedDropStrip = 0.08f;

		private readonly Dictionary<DockZone, UiTabGroup> tabGroups = new Dictionary<DockZone, UiTabGroup>();
		private readonly Dictionary<DockZone, RectTransform> zoneRects = new Dictionary<DockZone, RectTransform>();
		private readonly Dictionary<string, UiDockPanel> panels = new Dictionary<string, UiDockPanel>();
		private readonly Dictionary<string, DockZone> zoneOf = new Dictionary<string, DockZone>();
		private readonly Dictionary<DockZone, Image> dropOverlays = new Dictionary<DockZone, Image>();

		private readonly RectTransform bodyRect;
		private readonly UiSplitter leftSplitter;
		private readonly UiSplitter rightSplitter;
		private readonly UiSplitter bottomSplitter;
		private readonly GameObject dropOverlayRoot;
		private readonly GameObject ghost;

		private float leftSize = 0.2f;
		private float centerSize = 0.55f;
		private float rightSize = 0.25f;
		private float bottomSize = 0.22f;
		private DockZone highlightedZone = DockZone.Center;
		private bool dragging;

		/// <summary>Wraps an already-built dock space.</summary>
		private UiDockSpace(GameObject gameObject, UiTheme theme, RectTransform bodyRect,
			UiSplitter leftSplitter, UiSplitter rightSplitter, UiSplitter bottomSplitter,
			GameObject dropOverlayRoot, GameObject ghost)
			: base(gameObject, theme)
		{
			this.bodyRect = bodyRect;
			this.leftSplitter = leftSplitter;
			this.rightSplitter = rightSplitter;
			this.bottomSplitter = bottomSplitter;
			this.dropOverlayRoot = dropOverlayRoot;
			this.ghost = ghost;
		}

		/// <summary>Creates an empty dock space filling its parent, with four empty zones.</summary>
		public static UiDockSpace Create(Transform parent, UiTheme theme = null)
		{
			theme = theme ?? UiTheme.Default;

			GameObject root = new GameObject("UiDockSpace", typeof(Image));
			root.transform.SetParent(parent, false);
			UiLayoutUtil.Stretch(root.GetComponent<RectTransform>());
			root.GetComponent<Image>().color = Color.clear;

			GameObject body = new GameObject("Body", typeof(RectTransform));
			body.transform.SetParent(root.transform, false);

			RectTransform leftRect = CreateZoneRect(body.transform, "Left");
			RectTransform centerRect = CreateZoneRect(body.transform, "Center");
			RectTransform rightRect = CreateZoneRect(body.transform, "Right");
			RectTransform bottomRect = CreateZoneRect(root.transform, "Bottom");

			UiTabGroup leftTabs = UiTabGroup.Create(leftRect, theme);
			UiTabGroup centerTabs = UiTabGroup.Create(centerRect, theme);
			UiTabGroup rightTabs = UiTabGroup.Create(rightRect, theme);
			UiTabGroup bottomTabs = UiTabGroup.Create(bottomRect, theme);

			UiSplitter leftSplitter = UiSplitter.Create(body.transform, UiOrientation.Horizontal, theme);
			UiSplitter rightSplitter = UiSplitter.Create(body.transform, UiOrientation.Horizontal, theme);
			UiSplitter bottomSplitter = UiSplitter.Create(root.transform, UiOrientation.Vertical, theme);

			GameObject overlayRoot = new GameObject("DropOverlays", typeof(RectTransform));
			overlayRoot.transform.SetParent(root.transform, false);
			UiLayoutUtil.Stretch(overlayRoot.GetComponent<RectTransform>());
			overlayRoot.SetActive(false);

			Image leftDrop = CreateDropOverlay(overlayRoot.transform, "LeftDrop", theme);
			Image centerDrop = CreateDropOverlay(overlayRoot.transform, "CenterDrop", theme);
			Image rightDrop = CreateDropOverlay(overlayRoot.transform, "RightDrop", theme);
			Image bottomDrop = CreateDropOverlay(overlayRoot.transform, "BottomDrop", theme);

			GameObject ghostObject = new GameObject("DragGhost", typeof(Image));
			ghostObject.transform.SetParent(root.transform, false);
			RectTransform ghostRect = ghostObject.GetComponent<RectTransform>();
			ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
			ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
			ghostRect.sizeDelta = new Vector2(140f, theme.TabHeight);
			ghostObject.GetComponent<Image>().color = theme.TabActiveBackground;
			ghostObject.GetComponent<Image>().raycastTarget = false;
			GameObject ghostLabelObject = new GameObject("Label", typeof(Text));
			ghostLabelObject.transform.SetParent(ghostObject.transform, false);
			UiLayoutUtil.Stretch(ghostLabelObject.GetComponent<RectTransform>());
			Text ghostLabel = ghostLabelObject.GetComponent<Text>();
			ghostLabel.font = theme.Font;
			ghostLabel.fontSize = theme.BodyFontSize;
			ghostLabel.alignment = TextAnchor.MiddleCenter;
			ghostLabel.color = theme.LabelColor;
			ghostLabel.raycastTarget = false;
			ghostObject.SetActive(false);

			UiDockSpace space = new UiDockSpace(root, theme, body.GetComponent<RectTransform>(),
				leftSplitter, rightSplitter, bottomSplitter, overlayRoot, ghostObject);

			space.tabGroups[DockZone.Left] = leftTabs;
			space.tabGroups[DockZone.Center] = centerTabs;
			space.tabGroups[DockZone.Right] = rightTabs;
			space.tabGroups[DockZone.Bottom] = bottomTabs;
			space.zoneRects[DockZone.Left] = leftRect;
			space.zoneRects[DockZone.Center] = centerRect;
			space.zoneRects[DockZone.Right] = rightRect;
			space.zoneRects[DockZone.Bottom] = bottomRect;
			space.dropOverlays[DockZone.Left] = leftDrop;
			space.dropOverlays[DockZone.Center] = centerDrop;
			space.dropOverlays[DockZone.Right] = rightDrop;
			space.dropOverlays[DockZone.Bottom] = bottomDrop;

			foreach (KeyValuePair<DockZone, UiTabGroup> pair in space.tabGroups)
			{
				pair.Value.SetDragHost(space);
				pair.Value.OnClose(id => space.ClosePanel(id));
				pair.Value.OnPinChanged((id, pinned) =>
				{
					if (space.panels.TryGetValue(id, out UiDockPanel panel))
					{
						panel.SetPinned(pinned);
					}
				});
			}

			leftSplitter.OnFractionChanged(space.OnLeftSplit);
			rightSplitter.OnFractionChanged(space.OnRightSplit);
			bottomSplitter.OnFractionChanged(space.OnBottomSplit);
			leftSplitter.SetMinPixels(MinPanePx, MinPanePx);
			rightSplitter.SetMinPixels(MinPanePx, MinPanePx);
			bottomSplitter.SetMinPixels(MinPanePx, MinPanePx);

			space.Relayout();
			return space;
		}

		/// <summary>Adds an existing dock panel to a zone, reparenting it into that zone's tab group.</summary>
		public UiDockSpace AddPanel(UiDockPanel panel, DockZone zone)
		{
			if (panel == null)
			{
				throw new ArgumentNullException(nameof(panel));
			}
			if (panels.ContainsKey(panel.Id))
			{
				throw new ArgumentException("A panel with id '" + panel.Id + "' is already in this dock space.", nameof(panel));
			}

			panels[panel.Id] = panel;
			zoneOf[panel.Id] = zone;
			panel.SetTitleBarVisible(false);
			panel.OnClose(closed => ClosePanel(closed.Id));
			tabGroups[zone].AddTab(panel.Id, panel.Title, panel, panel.Closable, panel.Pinnable);
			Relayout();
			return this;
		}

		/// <summary>Moves a panel into another zone (or to a specific tab index in that zone).</summary>
		public UiDockSpace MovePanel(string panelId, DockZone zone, int tabIndex = -1)
		{
			if (!panels.TryGetValue(panelId, out UiDockPanel panel))
			{
				return this;
			}

			DockZone oldZone = zoneOf[panelId];
			bool pinned = tabGroups[oldZone].IsPinned(panelId);
			tabGroups[oldZone].RemoveTab(panelId);
			zoneOf[panelId] = zone;
			int insertAt = tabIndex < 0 ? tabGroups[zone].TabCount : tabIndex;
			tabGroups[zone].InsertTab(panelId, panel.Title, panel, insertAt, panel.Closable, panel.Pinnable);
			if (pinned)
			{
				tabGroups[zone].SetPinned(panelId, true);
			}
			tabGroups[zone].Select(panelId);
			Relayout();
			return this;
		}

		/// <summary>Shows the named panel in whichever zone currently holds it. Returns false if the id is unknown.</summary>
		public bool SelectPanel(string panelId)
		{
			if (!zoneOf.TryGetValue(panelId, out DockZone zone))
			{
				return false;
			}

			tabGroups[zone].Select(panelId);
			return true;
		}

		/// <summary>Removes a panel from the layout without destroying it. Returns the panel, or null if unknown.</summary>
		public UiDockPanel RemovePanel(string panelId)
		{
			if (!panels.TryGetValue(panelId, out UiDockPanel panel))
			{
				return null;
			}
			DockZone zone = zoneOf[panelId];
			tabGroups[zone].RemoveTab(panelId);
			panels.Remove(panelId);
			zoneOf.Remove(panelId);
			Relayout();
			return panel;
		}

		/// <summary>Closes a panel: removes it from the layout and deactivates its GameObject. Pinned panels are ignored.</summary>
		public UiDockSpace ClosePanel(string panelId)
		{
			if (!panels.TryGetValue(panelId, out UiDockPanel panel))
			{
				return this;
			}
			if (panel.IsPinned)
			{
				return this;
			}
			RemovePanel(panelId);
			panel.GameObject.SetActive(false);
			return this;
		}

		/// <summary>Looks up a panel previously added to this dock space.</summary>
		public bool TryGetPanel(string panelId, out UiDockPanel panel)
		{
			return panels.TryGetValue(panelId, out panel);
		}

		/// <summary>Captures the current zone sizes, tab orders, and selection as a serializable snapshot.</summary>
		public DockLayoutSnapshot CaptureLayout()
		{
			return new DockLayoutSnapshot
			{
				Zones = new[]
				{
					CaptureZone(DockZone.Left, leftSize),
					CaptureZone(DockZone.Center, centerSize),
					CaptureZone(DockZone.Right, rightSize),
					CaptureZone(DockZone.Bottom, bottomSize)
				}
			};
		}

		/// <summary>Applies a previously captured snapshot (unknown panel ids are skipped).</summary>
		public UiDockSpace ApplyLayout(DockLayoutSnapshot snapshot)
		{
			if (snapshot == null || snapshot.Zones == null)
			{
				return this;
			}

			foreach (DockZoneSnapshot zoneSnap in snapshot.Zones)
			{
				if (zoneSnap == null)
				{
					continue;
				}
				switch (zoneSnap.Zone)
				{
					case DockZone.Left:
						leftSize = Mathf.Max(0.05f, zoneSnap.Size);
						break;
					case DockZone.Center:
						centerSize = Mathf.Max(0.05f, zoneSnap.Size);
						break;
					case DockZone.Right:
						rightSize = Mathf.Max(0.05f, zoneSnap.Size);
						break;
					case DockZone.Bottom:
						bottomSize = Mathf.Clamp(zoneSnap.Size, 0.05f, 0.7f);
						break;
				}

				if (zoneSnap.PanelIds == null)
				{
					continue;
				}
				for (int i = 0; i < zoneSnap.PanelIds.Length; i++)
				{
					string id = zoneSnap.PanelIds[i];
					if (string.IsNullOrEmpty(id) || !panels.ContainsKey(id))
					{
						continue;
					}
					MovePanel(id, zoneSnap.Zone, i);
				}
				if (!string.IsNullOrEmpty(zoneSnap.SelectedPanelId) && panels.ContainsKey(zoneSnap.SelectedPanelId)
					&& zoneOf.TryGetValue(zoneSnap.SelectedPanelId, out DockZone selectedZone)
					&& selectedZone == zoneSnap.Zone)
				{
					tabGroups[zoneSnap.Zone].Select(zoneSnap.SelectedPanelId);
				}
			}

			Relayout();
			return this;
		}

		void ITabDragHost.OnTabDragBegin(UiTabGroup source, string tabId)
		{
			dragging = true;
			dropOverlayRoot.SetActive(true);
			ghost.SetActive(true);
			Text ghostLabel = ghost.transform.Find("Label").GetComponent<Text>();
			if (panels.TryGetValue(tabId, out UiDockPanel panel))
			{
				ghostLabel.text = panel.Title;
			}
			else
			{
				ghostLabel.text = tabId;
			}
			RefreshDropOverlayLayout();
			HighlightZone(DockZone.Center);
		}

		void ITabDragHost.OnTabDrag(UiTabGroup source, string tabId, PointerEventData eventData)
		{
			PositionGhost(eventData);
			HighlightZone(ZoneAtPointer(eventData));
		}

		bool ITabDragHost.OnTabDragEnd(UiTabGroup source, string tabId, PointerEventData eventData)
		{
			dragging = false;
			dropOverlayRoot.SetActive(false);
			ghost.SetActive(false);

			if (source.ContainsScreenPoint(eventData.position, eventData.pressEventCamera))
			{
				return false;
			}

			DockZone target = ZoneAtPointer(eventData);
			if (zoneOf.TryGetValue(tabId, out DockZone current) && current == target)
			{
				return false;
			}
			MovePanel(tabId, target);
			return true;
		}

		private DockZoneSnapshot CaptureZone(DockZone zone, float size)
		{
			List<string> ids = new List<string>(tabGroups[zone].TabIds);
			return new DockZoneSnapshot
			{
				Zone = zone,
				Size = size,
				PanelIds = ids.ToArray(),
				SelectedPanelId = tabGroups[zone].SelectedId
			};
		}

		private void OnLeftSplit(float fraction)
		{
			bool leftOpen = IsZoneOpen(DockZone.Left);
			bool rightOpen = IsZoneOpen(DockZone.Right);
			float remaining = 1f - fraction;
			if (leftOpen)
			{
				leftSize = fraction;
			}
			if (rightOpen)
			{
				float centerShare = centerSize / Mathf.Max(0.0001f, centerSize + rightSize);
				centerSize = remaining * centerShare;
				rightSize = remaining * (1f - centerShare);
			}
			else
			{
				centerSize = remaining;
			}
			Relayout();
		}

		private void OnRightSplit(float fraction)
		{
			bool leftOpen = IsZoneOpen(DockZone.Left);
			float total = Mathf.Max(0.0001f, leftSize + centerSize + rightSize);
			float leftFrac = leftOpen ? leftSize / total : 0f;
			float centerFrac = Mathf.Max(0.05f, fraction - leftFrac);
			float rightFrac = Mathf.Max(0.05f, 1f - fraction);
			if (leftOpen)
			{
				leftSize = leftFrac;
				centerSize = centerFrac;
				rightSize = rightFrac;
				float sum = leftSize + centerSize + rightSize;
				leftSize /= sum;
				centerSize /= sum;
				rightSize /= sum;
			}
			else
			{
				centerSize = centerFrac;
				rightSize = rightFrac;
				float sum = centerSize + rightSize;
				centerSize /= sum;
				rightSize /= sum;
			}
			Relayout();
		}

		private void OnBottomSplit(float fraction)
		{
			bottomSize = 1f - fraction;
			Relayout();
		}

		private bool IsZoneOpen(DockZone zone)
		{
			if (zone == DockZone.Center)
			{
				return true;
			}
			return tabGroups[zone].TabCount > 0;
		}

		private void Relayout()
		{
			bool leftOpen = IsZoneOpen(DockZone.Left);
			bool rightOpen = IsZoneOpen(DockZone.Right);
			bool bottomOpen = IsZoneOpen(DockZone.Bottom);

			float bottomFrac = bottomOpen ? Mathf.Clamp(bottomSize, 0.12f, 0.6f) : 0f;
			float half = Theme.SplitterThickness * 0.5f;

			bodyRect.anchorMin = new Vector2(0f, bottomFrac);
			bodyRect.anchorMax = Vector2.one;
			bodyRect.offsetMin = new Vector2(0f, bottomOpen ? half : 0f);
			bodyRect.offsetMax = Vector2.zero;

			RectTransform bottomRect = zoneRects[DockZone.Bottom];
			bottomRect.anchorMin = Vector2.zero;
			bottomRect.anchorMax = new Vector2(1f, bottomFrac);
			bottomRect.offsetMin = Vector2.zero;
			bottomRect.offsetMax = new Vector2(0f, bottomOpen ? -half : 0f);
			bottomRect.gameObject.SetActive(bottomOpen);

			float l = leftOpen ? leftSize : 0f;
			float c = centerSize;
			float r = rightOpen ? rightSize : 0f;
			float total = Mathf.Max(0.0001f, l + c + r);
			float leftFrac = l / total;
			float centerFrac = c / total;

			ApplyColumn(zoneRects[DockZone.Left], 0f, leftFrac, leftOpen, half, true);
			ApplyColumn(zoneRects[DockZone.Center], leftFrac, leftFrac + centerFrac, true, half, leftOpen || rightOpen);
			ApplyColumn(zoneRects[DockZone.Right], leftFrac + centerFrac, 1f, rightOpen, half, true);

			leftSplitter.GameObject.SetActive(leftOpen);
			rightSplitter.GameObject.SetActive(rightOpen);
			bottomSplitter.GameObject.SetActive(bottomOpen);

			if (leftOpen)
			{
				leftSplitter.SetFraction(leftFrac);
			}
			if (rightOpen)
			{
				rightSplitter.SetFraction(leftFrac + centerFrac);
			}
			if (bottomOpen)
			{
				bottomSplitter.SetFraction(1f - bottomFrac);
			}

			if (dragging)
			{
				RefreshDropOverlayLayout();
			}
		}

		private static void ApplyColumn(RectTransform rect, float minX, float maxX, bool visible, float half, bool inset)
		{
			rect.anchorMin = new Vector2(minX, 0f);
			rect.anchorMax = new Vector2(maxX, 1f);
			rect.offsetMin = new Vector2(inset && minX > 0.001f ? half : 0f, 0f);
			rect.offsetMax = new Vector2(inset && maxX < 0.999f ? -half : 0f, 0f);
			rect.gameObject.SetActive(visible);
		}

		private DockZone ZoneAtPointer(PointerEventData eventData)
		{
			if (Hit(DockZone.Bottom, eventData, IsZoneOpen(DockZone.Bottom) ? 0f : CollapsedDropStrip))
			{
				return DockZone.Bottom;
			}
			if (Hit(DockZone.Left, eventData, IsZoneOpen(DockZone.Left) ? 0f : CollapsedDropStrip))
			{
				return DockZone.Left;
			}
			if (Hit(DockZone.Right, eventData, IsZoneOpen(DockZone.Right) ? 0f : CollapsedDropStrip))
			{
				return DockZone.Right;
			}
			return DockZone.Center;
		}

		private bool Hit(DockZone zone, PointerEventData eventData, float collapsedStrip)
		{
			RectTransform rect = zoneRects[zone];
			if (collapsedStrip <= 0f)
			{
				return RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera);
			}

			if (!UiLayoutUtil.ScreenToLocal(RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
			{
				return false;
			}
			Rect bounds = RectTransform.rect;
			float nx = bounds.width > 0f ? (local.x - bounds.xMin) / bounds.width : 0.5f;
			float ny = bounds.height > 0f ? (local.y - bounds.yMin) / bounds.height : 0.5f;
			switch (zone)
			{
				case DockZone.Left:
					return nx <= collapsedStrip;
				case DockZone.Right:
					return nx >= 1f - collapsedStrip;
				case DockZone.Bottom:
					return ny <= collapsedStrip;
				default:
					return false;
			}
		}

		private void HighlightZone(DockZone zone)
		{
			highlightedZone = zone;
			foreach (KeyValuePair<DockZone, Image> pair in dropOverlays)
			{
				Color color = Theme.DockDropHighlight;
				color.a = pair.Key == zone ? Theme.DockDropHighlight.a : 0.08f;
				pair.Value.color = color;
			}
		}

		private void RefreshDropOverlayLayout()
		{
			CopyRect(zoneRects[DockZone.Left], dropOverlays[DockZone.Left].rectTransform, IsZoneOpen(DockZone.Left), new Vector2(0f, 0.12f), new Vector2(CollapsedDropStrip, 1f));
			CopyRect(zoneRects[DockZone.Center], dropOverlays[DockZone.Center].rectTransform, true, Vector2.zero, Vector2.one);
			CopyRect(zoneRects[DockZone.Right], dropOverlays[DockZone.Right].rectTransform, IsZoneOpen(DockZone.Right), new Vector2(1f - CollapsedDropStrip, 0.12f), Vector2.one);
			CopyRect(zoneRects[DockZone.Bottom], dropOverlays[DockZone.Bottom].rectTransform, IsZoneOpen(DockZone.Bottom), new Vector2(0f, 0f), new Vector2(1f, CollapsedDropStrip));
			dropOverlayRoot.transform.SetAsLastSibling();
			ghost.transform.SetAsLastSibling();
		}

		private void CopyRect(RectTransform source, RectTransform dest, bool sourceOpen, Vector2 collapsedMin, Vector2 collapsedMax)
		{
			if (sourceOpen)
			{
				dest.anchorMin = source.anchorMin;
				dest.anchorMax = source.anchorMax;
				dest.offsetMin = source.offsetMin;
				dest.offsetMax = source.offsetMax;
				if (source.parent == bodyRect)
				{
					dest.anchorMin = new Vector2(source.anchorMin.x, bottomSize);
					dest.anchorMax = new Vector2(source.anchorMax.x, 1f);
				}
			}
			else
			{
				dest.anchorMin = collapsedMin;
				dest.anchorMax = collapsedMax;
				dest.offsetMin = Vector2.zero;
				dest.offsetMax = Vector2.zero;
			}
		}

		private void PositionGhost(PointerEventData eventData)
		{
			if (!UiLayoutUtil.ScreenToLocal(RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
			{
				return;
			}
			RectTransform ghostRect = ghost.GetComponent<RectTransform>();
			ghostRect.anchoredPosition = local;
		}

		private static RectTransform CreateZoneRect(Transform parent, string name)
		{
			GameObject go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, false);
			return go.GetComponent<RectTransform>();
		}

		private static Image CreateDropOverlay(Transform parent, string name, UiTheme theme)
		{
			GameObject go = new GameObject(name, typeof(Image));
			go.transform.SetParent(parent, false);
			Image image = go.GetComponent<Image>();
			image.color = theme.DockDropHighlight;
			image.raycastTarget = false;
			return image;
		}
	}
}
