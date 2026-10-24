using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using BiltonsYoyoMod.UI;

namespace BiltonsYoyoMod.Content.Guidebook
{
	/// <summary>
	/// The guidebook UI: a leather-bound tome with a closed single-leaf cover,
	/// a printed contents leaf, a measured serif/ink page grid, optional
	/// two-page spread, clickable cross-references, search and ribbon bookmarks.
	/// The old catalogue skin was retired; "Better Book" adds chapter
	/// navigation, history, search snippets, wheel paging, a progress bar and a
	/// dimmed backdrop without changing the book's visual language.
	/// </summary>
	public class YoyoGuidebookUISystem : ModSystem
	{
		public static bool Open;
		public static string BookId = "General";
		public static int Page;

		/// <summary>Index of the open volume in the shelf order (used for "VOLUME II").</summary>
		public static int BookIndex
		{
			get
			{
				for (int i = 0; i < Volumes.Length; i++)
					if (Volumes[i].id == BookId)
						return i;
				return 0;
			}
		}

		private const float BookBodyScale = 0.85f;
		private const float BookBodyLine = 22f;
		private const int SearchRowHeight = 38;
		private const int TurnFrames = 20;
		private const int UnfoldFrames = 16;
		private const int OpenFrames = 22;
		private const int CloseFrames = 14;

		private static int turnTimer; // 0 = settled; >0 = turning.
		private static int turnDir = 1;
		private static int openTimer;
		private static int unfoldTimer;
		private static int closeTimer;
		private static bool closing;
		private static float leafAnim = 1f;
		private static bool twoPage;
		private static bool prevLeft;

		private static Rectangle prevBtn;
		private static Rectangle nextBtn;
		private static Rectangle backBtn;
		private static Rectangle closeBtn;
		private static Rectangle searchField;
		private static Rectangle searchClearRect;
		private static Rectangle edgeTab;
		private static Rectangle chapterBackRect;
		private static bool edgeTabOnRight;
		private static Rectangle lastRect;
		private enum LeftTab { Contents, Chapters, Search, Marks }

		private static LeftTab leftTab = LeftTab.Contents;
		// Fractional tab position for the sliding brass indicator. It is a
		// presentation value only; clicks and content use the integer tab.
		private static float leftTabGlide;
		private static bool chaptersOpen
		{
			get => leftTab == LeftTab.Chapters;
			set
			{
				if (value)
					leftTab = LeftTab.Chapters;
				else if (leftTab == LeftTab.Chapters)
					leftTab = LeftTab.Contents;
			}
		}

		private static bool searchActive
		{
			get => leftTab == LeftTab.Search;
			set
			{
				if (value)
					leftTab = LeftTab.Search;
				else if (leftTab == LeftTab.Search)
					leftTab = LeftTab.Contents;
			}
		}

		private static bool marksOpen => leftTab == LeftTab.Marks;
		private static int chapterScroll;
		private static int chapterMaxScroll;
		private static int marksScroll;
		private static int marksMaxScroll;
		private static readonly List<(Rectangle rect, string book, int sourceIndex)> chapterRects = new();
		private static readonly List<(string book, int page, bool twoPage)> history = new();

		private static readonly Rectangle[] leftTabRects = new Rectangle[4];
		private static readonly List<(Rectangle rect, int type)> iconRects = new();
		private static readonly List<(Rectangle rect, string book)> volumeTabs = new();
		private static readonly List<(Rectangle rect, string book, string title, string label)> linkRects = new();
		private static readonly List<(Rectangle rect, int pageIndex, string book, int sourceIndex)> bookmarkRibbonRects = new();
		private static readonly List<(Rectangle rect, string book, int sourceIndex)> bookmarkRowRects = new();
		private static readonly List<(Rectangle rect, string book, int sourceIndex)> bookmarkRemoveRects = new();
		private static readonly List<(Rectangle rect, SearchHit hit)> searchRects = new();
		private static readonly List<(Rectangle rect, string label, bool done)> stampRects = new();

		private static Rectangle resumeBtn;
		private static Rectangle shortcutBtn;
		private static Rectangle shortcutCardRect;
		private static Rectangle scrubberTrack;
		private static Rectangle scrubberThumb;
		private static Rectangle searchAllRect;
		private static Rectangle searchThisRect;
		private static Rectangle searchTextRect;
		private static Rectangle searchTitleRect;
		private static Rectangle marksClearRect;
		private static bool draggingScrubber;
		private static int scrubberGrabOffset;
		private static bool showShortcuts;
		private static bool showAllVolumes = true;
		private static bool searchTitlesOnly;
		private static string highlightQuery = "";
		private static bool escapeLatch; // one Escape closes a layer, not the whole book
		private static string searchText = "";
		private static string searchCacheKey;
		private static readonly List<SearchHit> searchHits = new();
		private static Keys[] prevKeys = Array.Empty<Keys>();
		private static int backspaceCooldown;

		// Baked leather/paper skin (primitives rendered once, not every frame).
		private static RenderTarget2D skinTarget;
		private static Color skinAccent = Color.Transparent;
		private static bool skinBakeFailed;

		// Baked closed cover, used to compress the board on its hinge when the
		// book opens instead of sliding a flat rectangle over the spread.
		private static RenderTarget2D coverTarget;
		private static Color coverAccent = Color.Transparent;
		private static string coverBook = "";
		private static int coverW;
		private static int coverH;
		private static bool coverBakeFailed;

		// Baked old-page print for the turning leaf. Without this target the
		// moving sheet is blank paper for the first half of a turn, which reads
		// as a white card wiping the new page instead of a printed page folding.
		private static RenderTarget2D turnTarget;
		private static int turnTargetPage = -1;
		private static int turnTargetW;
		private static int turnTargetH;
		private static string turnTargetBook = "";
		private static bool turnTargetFailed;
		private static int turnFromPage = -1;
		private static string turnFromBook = "";
		private static bool turnFromLeft;

		private static readonly (string id, string label)[] Volumes = {
			("General", "Gen"), ("Yoyos", "Yoyo"), ("Accessories", "Acc"),
			("Others", "Misc"), ("Consumables", "Use"), ("BlocksBosses", "Boss"),
		};

		/// <summary>Editorial cross-references, in clickable phrase form.</summary>
		private static readonly (string Phrase, string Book, string Title)[] CrossLinks = {
			("Yoyo Master", "Others", "Town NPCs"),
			("Ancient Yoyoist", "Others", "Town NPCs"),
			("Yoyo Tinkerer", "Others", "Mods (Tinkerer)"),
			("Tinkerer", "Others", "Mods (Tinkerer)"),
			("Six Stringer", "Others", "Night Visitors & Pets"),
			("Yoyo Cultist", "Others", "Night Visitors & Pets"),
			("Trick books", "Accessories", "Tricks & Emblems"),
			("Tricks", "Accessories", "Tricks & Emblems"),
			("Emblems", "Accessories", "Tricks & Emblems"),
			("Cases", "Accessories", "Cases & Duplicators"),
			("Bearings", "Accessories", "Bearings & Axles"),
			("Strings", "Accessories", "Strings"),
			("Spells", "Others", "Spells (Ancient)"),
			("Mods", "Others", "Mods (Tinkerer)"),
			("Cowboys", "BlocksBosses", "Cowboys & Bearings"),
			("Giant Bearing", "BlocksBosses", "Cowboys & Bearings"),
			("Ancient Duelist", "BlocksBosses", "Duelist & Guardian"),
			("Yoyo Guardian", "BlocksBosses", "Duelist & Guardian"),
			("Trial of the Guardian", "BlocksBosses", "Duelist & Guardian"),
			("Summon Papers", "Consumables", "Summon Papers"),
			("Insight", "Consumables", "Insight & Wisdom"),
			("Tonics", "Consumables", "Tonics (Drink Up)"),
			("Shimmer", "General", "Tips That Save Runs"),
		};

		private sealed class SearchHit
		{
			public string Book = "";
			public int SourceIndex;
			public int DisplayIndex;
			public string Title = "";
			public string Snippet = "";
			public int Rank;
		}

		/// <summary>Reading aids layered onto the book. Config defaults to on.</summary>
		private static bool BetterBook
		{
			get
			{
				try { return ModContent.GetInstance<Configuration.YoyoConfig>().BetterBook; }
				catch { return true; }
			}
		}

		// Layout and input share one stable virtual viewport. Main.screenWidth
		// and Main.MouseScreen change zoom contexts between update and draw;
		// UiViewport reconstructs InterfaceScaleType.UI coordinates from raw
		// pixels so a tab can never be painted in one place and clicked in
		// another.
		private static int UiWidth => UiViewport.Width;
		private static int UiHeight => UiViewport.Height;
		private static Vector2 UiMouse => UiViewport.Mouse;

		private const int MinimumBookWidth = 320;
		private const int MinimumBookHeight = 260;
		private const int SpreadMinWidth = 760;

		// Prefer a normal margin, but never force the book wider than a tiny
		// window. The fallback keeps a few pixels of shadow/edge breathing room
		// instead of centering a clipped page off both sides of the screen.
		private static int BookWidth()
		{
			int available = UiWidth - 24;
			return available >= MinimumBookWidth
				? Math.Min(980, available)
				: Math.Max(240, UiWidth - 8);
		}

		private static int BookHeight()
		{
			int available = UiHeight - 24;
			return available >= MinimumBookHeight
				? Math.Min(600, available)
				: Math.Max(220, UiHeight - 8);
		}

		private static int SideWidth(int pw) => pw / 2;

		/// <summary>Two pages need real columns. Below this width the tome
		/// automatically folds to the single-leaf reader rather than squeezing
		/// two unreadable strips onto the paper.</summary>
		private static bool CanTwoPage => BookWidth() >= SpreadMinWidth;

		private static int PageInset(int pw) => pw >= 640 ? 34 : 26;

		/// <summary>
		/// One canonical reading width for both the contents leaf and the two-page
		/// spread, so pagination does not change when the reader toggles spreads.
		/// The inset shrinks with the book instead of forcing a 150 px column
		/// that overflowed narrow pages.
		/// </summary>
		private static int ReadingWidth(int pw)
		{
			int side = SideWidth(pw);
			int inset = PageInset(pw);
			// Keep the next-page arrow inside the board even at the compact
			// floor, where the symmetric inset column would otherwise collide
			// with the 26 px arrow at the fore-edge.
			int width = Math.Min(side - inset * 2, side - inset - 28);
			return Math.Max(60, width);
		}

		private static int PageCount => layout.Count;

		// Typeset layout, rebuilt only when the book or window size changes.
		private static readonly List<GuidebookDisplayPage> layout = new();
		private static bool layoutValid;
		private static string layoutBook = "";
		private static int layoutW;
		private static int layoutH;

		private static List<GuidebookDisplayPage> BuildDisplayLayout(string bookId, int pw, int ph)
		{
			float width = ReadingWidth(pw);
			float scale = BookBodyScale;
			float lineHeight = BookBodyLine;
			int textTop = 96;
			int plainBottom = ph - 64;
			int iconBottom = ph - 126;

			int plain = Math.Max(1, (int)((plainBottom - textTop) / lineHeight));
			int withIcons = Math.Max(1, (int)((iconBottom - textTop) / lineHeight));

			return YoyoGuidebookLayout.Build(
				YoyoGuidebookData.PagesOf(bookId), width, scale, plain, withIcons);
		}

		/// <summary>
		/// Wraps and paginates the current volume for the current skin and window.
		/// Called from input as well as drawing because navigation needs PageCount.
		/// </summary>
		private static void EnsureLayout()
		{
			int pw = BookWidth();
			int ph = BookHeight();

			// A resized window can strand the reader in a spread that no longer
			// fits. Fold to one leaf first; the canonical reading width means
			// pagination itself does not change.
			if (twoPage && pw < SpreadMinWidth)
				twoPage = false;

			if (layoutValid && layoutBook == BookId && layoutW == pw && layoutH == ph)
				return;

			layout.Clear();
			layout.AddRange(BuildDisplayLayout(BookId, pw, ph));

			layoutBook = BookId;
			layoutW = pw;
			layoutH = ph;
			layoutValid = true;
		}

		private static List<string> OwnedVolumes(Player player)
		{
			var owned = new List<string>();
			foreach (var (id, _) in Volumes) {
				int t = VolumeItemType(id);
				if (t <= 0)
					continue;
				bool has = false;
				for (int i = 0; i < player.inventory.Length; i++) {
					Item it = player.inventory[i];
					if (it != null && !it.IsAir && it.type == t) {
						has = true;
						break;
					}
				}
				if (has)
					owned.Add(id);
			}
			// Never hide the open volume mid-read.
			if (!owned.Contains(BookId))
				owned.Add(BookId);
			return owned;
		}

		private static string ShortVolumeTitle(string bookId)
		{
			return bookId switch {
				"General" => "General",
				"Yoyos" => "Yoyos",
				"Accessories" => "Gadgets",
				"Others" => "Others",
				"Consumables" => "Tonics",
				"BlocksBosses" => "Bosses",
				_ => bookId,
			};
		}

		private static int VolumeItemType(string bookId)
		{
			try {
				return bookId switch {
					"General" => ModContent.ItemType<Content.Items.Guidebooks.YoyoGuidebookGeneral>(),
					"Yoyos" => ModContent.ItemType<Content.Items.Guidebooks.YoyoGuidebookYoyos>(),
					"Accessories" => ModContent.ItemType<Content.Items.Guidebooks.YoyoGuidebookAccessories>(),
					"Others" => ModContent.ItemType<Content.Items.Guidebooks.YoyoGuidebookOthers>(),
					"Consumables" => ModContent.ItemType<Content.Items.Guidebooks.YoyoGuidebookConsumables>(),
					"BlocksBosses" => ModContent.ItemType<Content.Items.Guidebooks.YoyoGuidebookBlocksBosses>(),
					_ => 0,
				};
			}
			catch { return 0; }
		}

		// --------------------------------------------------------------- opening

		public static void OpenBook(string bookId)
		{
			BookId = string.IsNullOrEmpty(bookId) ? "General" : bookId;
			Page = -1; // Cover first: click or arrow to open.
			turnTimer = 0;
			unfoldTimer = 0;
			openTimer = OpenFrames;
			closeTimer = 0;
			closing = false;
			leafAnim = 1f;
			Open = true;
			leftTab = LeftTab.Contents;
			leftTabGlide = 0f;
			escapeLatch = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			highlightQuery = "";
			showAllVolumes = true;
			searchTitlesOnly = false;
			chapterScroll = 0;
			marksScroll = 0;
			chapterRects.Clear();
			showShortcuts = false;
			draggingScrubber = false;
			turnFromPage = -1;
			turnTargetPage = -1;
			history.Clear();
			prevLeft = Main.mouseLeft;
			prevKeys = Array.Empty<Keys>();
			// Modal reading: fold the inventory away so the book owns the screen.
			Main.playerInventory = false;
			YoyoGuidebookIcons.Ensure();
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void CloseBook()
		{
			if (!Open || closing)
				return;
			SaveReadingPosition();
			// Closing is animated: keep Open true until the cover has sunk and
			// faded, but stop accepting input immediately.
			closing = true;
			closeTimer = CloseFrames;
			leftTab = LeftTab.Contents;
			leftTabGlide = 0f;
			escapeLatch = false;
			searchText = "";
			draggingScrubber = false;
			showShortcuts = false;
			turnFromPage = -1;
			turnTargetPage = -1;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		private static void SaveReadingPosition()
		{
			if (Page < 0 || layout.Count == 0)
				return;
			try {
				Bookmarks()?.RememberPage(BookId, Math.Clamp(Page, 0, layout.Count - 1),
					twoPage && CanTwoPage);
			}
			catch { }
		}

		private static void ResumeFromCover()
		{
			EnsureLayout();
			int saved = -1;
			bool savedSpread = false;
			try {
				var pl = Bookmarks();
				if (pl != null) {
					saved = pl.RememberedPage(BookId);
					savedSpread = pl.RememberedSpread(BookId);
				}
			}
			catch { }
			if (saved < 0) {
				NextPage();
				return;
			}
			Page = Math.Clamp(saved, 0, Math.Max(0, PageCount - 1));
			twoPage = savedSpread && CanTwoPage;
			unfoldTimer = UnfoldFrames;
			turnTimer = 0;
			turnDir = 1;
			BeginTurn(-1, false);
			leftTab = LeftTab.Contents;
			SaveReadingPosition();
			PaperTurn();
		}

		/// <summary>Remembers where a jump started so Backspace can return there.</summary>
		private static void RememberPosition()
		{
			if (!BetterBook || Page < 0)
				return;
			if (history.Count > 0) {
				var last = history[history.Count - 1];
				if (last.book == BookId && last.page == Page && last.twoPage == twoPage)
					return;
			}
			history.Add((BookId, Page, twoPage));
			if (history.Count > 32)
				history.RemoveAt(0);
		}

		private static void GoBack()
		{
			if (!BetterBook || history.Count == 0)
				return;
			var target = history[history.Count - 1];
			history.RemoveAt(history.Count - 1);
			int oldPage = Page;
			string oldBook = BookId;
			bool oldTwoPage = twoPage;
			BookId = target.book;
			Page = target.page;
			twoPage = target.twoPage;
			unfoldTimer = 0;
			turnDir = -1;
			if (oldPage >= 0 && !oldTwoPage && !twoPage && oldBook == BookId) {
				turnTimer = TurnFrames;
				BeginTurn(oldPage, false, oldBook);
			}
			else {
				turnTimer = 0;
				BeginTurn(-1, false);
			}
			chaptersOpen = false;
			chapterScroll = 0;
			searchActive = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			EnsureLayout();
			Page = Math.Clamp(Page, -1, Math.Max(0, PageCount - 1));
			PaperTurn();
		}

		private static void PaperTurn()
		{
			// There is no page-turn sound in vanilla; a quiet grass rustle plus a
			// very soft menu tick reads as dry paper without new assets.
			SoundEngine.PlaySound(SoundID.Grass with {
				Volume = 0.24f, Pitch = 0.42f, PitchVariance = 0.22f, MaxInstances = 3,
			});
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.14f, Pitch = 0.32f, PitchVariance = 0.08f, MaxInstances = 2,
			});
		}

		/// <summary>Marks the printed page that becomes the moving leaf and
		/// invalidates the previous baked turn target.</summary>
		private static void BeginTurn(int page, bool fromLeft, string book = null)
		{
			turnFromPage = page;
			turnFromBook = string.IsNullOrEmpty(book) ? BookId : book;
			turnFromLeft = fromLeft;
			turnTargetPage = -1;
			turnTargetFailed = false;
		}

		public static void NextPage()
		{
			EnsureLayout();
			if (turnTimer > 0)
				return;
			SaveReadingPosition();
			CancelSearch();
			chaptersOpen = false;
			if (Page < 0) {
				// Opening the cover is a spread unfolding, not a page turn. A
				// sheet turn here would slide blank paper over the fresh spread
				// while the board is still hinging open.
				Page = 0;
				unfoldTimer = UnfoldFrames;
				turnTimer = 0;
				turnDir = 1;
				BeginTurn(-1, false);
				SaveReadingPosition();
				PaperTurn();
				return;
			}
			int step = twoPage ? 2 : 1;
			if (Page + step >= layout.Count)
				return;
			int oldPage = twoPage ? Math.Min(Page + 1, layout.Count - 1) : Page;
			Page += step;
			turnTimer = TurnFrames;
			turnDir = 1;
			BeginTurn(oldPage, false);
			SaveReadingPosition();
			PaperTurn();
		}

		public static void PrevPage()
		{
			EnsureLayout();
			if (turnTimer > 0)
				return;
			SaveReadingPosition();
			CancelSearch();
			chaptersOpen = false;
			if (Page < 0)
				return;
			if (Page == 0) {
				Page = -1;
				turnTimer = 0;
				BeginTurn(-1, false);
				PaperTurn();
				return;
			}
			int step = twoPage ? 2 : 1;
			int oldPage = Page;
			bool oldLeft = twoPage;
			Page = Math.Max(0, Page - step);
			turnTimer = TurnFrames;
			turnDir = -1;
			BeginTurn(oldPage, oldLeft);
			SaveReadingPosition();
			PaperTurn();
		}

		/// <summary>Chapter jump from the contents leaf: open on the first page, not the cover.</summary>
		private static void SwitchVolume(string bookId)
		{
			if (string.IsNullOrEmpty(bookId))
				return;

			SaveReadingPosition();
			RememberPosition();
			leftTab = LeftTab.Contents;
			leftTabGlide = 0f;
			chapterScroll = 0;
			BookId = bookId;
			Page = 0;
			turnTimer = 0;
			turnDir = 1;
			unfoldTimer = 0;
			BeginTurn(-1, false);
			searchActive = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			EnsureLayout();
			if (Page > PageCount - 1)
				Page = Math.Max(0, PageCount - 1);
			SaveReadingPosition();
			PaperTurn();
		}

		private static void ToggleTwoPage()
		{
			if (Page < 0)
				return;
			// Turning a spread off is always allowed. Turning one on requires
			// enough width for two genuine reading columns.
			if (!twoPage && !CanTwoPage)
				return;
			twoPage = !twoPage;
			chaptersOpen = false;
			chapterScroll = 0;
			if (twoPage && PageCount > 0)
				Page = Math.Min(Math.Max(0, Page & ~1), Math.Max(0, (PageCount - 1) & ~1));
			turnTimer = 0;
			turnFromPage = -1;
			searchActive = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			SaveReadingPosition();
			PaperTurn();
		}

		private static void ToggleChapters()
		{
			if (!BetterBook || Page < 0 || twoPage)
				return;
			SetLeftTab(chaptersOpen ? LeftTab.Contents : LeftTab.Chapters);
		}

		private static void ToggleMarks()
		{
			if (!BetterBook || Page < 0 || twoPage)
				return;
			SetLeftTab(marksOpen ? LeftTab.Contents : LeftTab.Marks);
		}

		private static void CycleLeftTab(int direction)
		{
			if (Page < 0 || twoPage)
				return;
			int value = ((int)leftTab + direction) % 4;
			if (value < 0)
				value += 4;
			SetLeftTab((LeftTab)value);
		}

		private static void SetLeftTab(LeftTab tab)
		{
			if (Page < 0 || twoPage) {
				tab = LeftTab.Contents;
			}
			if (leftTab == tab) {
				if (tab != LeftTab.Contents)
					tab = LeftTab.Contents;
			}
			leftTab = tab;
			chapterScroll = 0;
			if (tab == LeftTab.Marks)
				marksScroll = 0;
			leafAnim = 0f;
			if (tab != LeftTab.Search) {
				// Keep the query in the field when leaving Search, but stop the
				// caret and clear the result hitboxes: they belong to the old tab.
				searchRects.Clear();
			}
			if (tab == LeftTab.Search && Page >= 0 && !twoPage && searchText.Length == 0)
				searchCacheKey = null;
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.18f,
				Pitch = tab == LeftTab.Search ? 0.35f : tab == LeftTab.Marks ? 0.25f : 0.12f,
				MaxInstances = 2,
			});
		}

		private static int IndexOfSource(int sourceIndex)
		{
			for (int i = 0; i < layout.Count; i++) {
				if (layout[i].SourceIndex == sourceIndex)
					return i;
			}
			return 0;
		}

		private static void JumpTo(string book, string title)
		{
			if (string.IsNullOrEmpty(book) || string.IsNullOrEmpty(title))
				return;

			int oldPage = Page;
			string oldBook = BookId;
			bool oldTwoPage = twoPage;
			RememberPosition();
			chaptersOpen = false;
			BookId = book;
			EnsureLayout();
			int found = 0;
			for (int i = 0; i < layout.Count; i++) {
				string t = layout[i].Title;
				if (t == title || t.StartsWith(title + " (cont.)", StringComparison.Ordinal)) {
					found = i;
					break;
				}
			}
			Page = Math.Clamp(found, 0, Math.Max(0, PageCount - 1));
			twoPage = false;
			unfoldTimer = 0;
			turnDir = 1;
			if (oldPage >= 0 && !oldTwoPage && oldBook == BookId) {
				turnTimer = TurnFrames;
				BeginTurn(oldPage, false, oldBook);
			}
			else {
				turnTimer = 0;
				BeginTurn(-1, false);
			}
			searchActive = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			PaperTurn();
		}

		private static void JumpToSearch(SearchHit hit)
		{
			if (hit == null)
				return;
			int oldPage = Page;
			string oldBook = BookId;
			bool oldTwoPage = twoPage;
			highlightQuery = searchText.Trim();
			RememberPosition();
			chaptersOpen = false;
			BookId = hit.Book;
			EnsureLayout();
			Page = IndexOfSource(hit.SourceIndex);
			twoPage = false;
			unfoldTimer = 0;
			turnDir = 1;
			if (oldPage >= 0 && !oldTwoPage && oldBook == BookId) {
				turnTimer = TurnFrames;
				BeginTurn(oldPage, false, oldBook);
			}
			else {
				turnTimer = 0;
				BeginTurn(-1, false);
			}
			searchActive = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			PaperTurn();
		}

		private static void JumpToBookmark(string book, int sourceIndex)
		{
			int oldPage = Page;
			string oldBook = BookId;
			bool oldTwoPage = twoPage;
			RememberPosition();
			chaptersOpen = false;
			BookId = book;
			EnsureLayout();
			Page = IndexOfSource(sourceIndex);
			twoPage = false;
			unfoldTimer = 0;
			turnDir = 1;
			if (oldPage >= 0 && !oldTwoPage && oldBook == BookId) {
				turnTimer = TurnFrames;
				BeginTurn(oldPage, false, oldBook);
			}
			else {
				turnTimer = 0;
				BeginTurn(-1, false);
			}
			searchActive = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			PaperTurn();
		}

		private static YoyoGuidebookPlayer Bookmarks()
		{
			try { return Main.LocalPlayer?.GetModPlayer<YoyoGuidebookPlayer>(); }
			catch { return null; }
		}

		private static void CancelSearch()
		{
			if (!searchActive && searchText.Length == 0)
				return;
			searchActive = false;
			searchText = "";
			searchCacheKey = null;
			searchHits.Clear();
			highlightQuery = "";
			leafAnim = 0f;
		}

		private static void ToggleBookmark(string book, int sourceIndex)
		{
			var pl = Bookmarks();
			if (pl == null)
				return;
			bool on = pl.ToggleBookmark(book, sourceIndex);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.26f, Pitch = on ? 0.55f : 0.15f, MaxInstances = 2,
			});
		}

		private static void ToggleBookmarkHere()
		{
			if (Page < 0 || layout.Count == 0)
				return;
			int idx = twoPage ? Math.Min(Page + 1, layout.Count - 1) : Page;
			ToggleBookmark(BookId, layout[idx].SourceIndex);
		}

		private static void ClearVolumeBookmarks()
		{
			var pl = Bookmarks();
			if (pl == null)
				return;
			var remove = new List<string>();
			foreach (string entry in pl.Bookmarks) {
				if (entry.StartsWith(BookId + "|", StringComparison.Ordinal))
					remove.Add(entry);
			}
			if (remove.Count == 0)
				return;
			foreach (string entry in remove)
				pl.Bookmarks.Remove(entry);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.24f, Pitch = -0.12f, MaxInstances = 2,
			});
		}

		private static void SetPageFromScrubber(int mouseX)
		{
			if (layout.Count == 0 || scrubberTrack.Width <= 2)
				return;
			float t = MathHelper.Clamp((mouseX - scrubberTrack.X) / (float)scrubberTrack.Width, 0f, 1f);
			int page = (int)Math.Round(t * (layout.Count - 1));
			if (twoPage)
				page &= ~1;
			Page = Math.Clamp(page, 0, layout.Count - 1);
			turnTimer = 0;
			SaveReadingPosition();
		}

		// ------------------------------------------------------------------ input

		private static bool KeyPressed(Keys[] now, Keys key)
		{
			if (!Main.keyState.IsKeyDown(key))
				return false;
			foreach (Keys old in prevKeys) {
				if (old == key)
					return false;
			}
			return true;
		}

		private static bool ShiftDown()
			=> Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);

		private static void SuppressGameplayInput(Player player)
		{
			player.mouseInterface = true;
			player.controlLeft = false;
			player.controlRight = false;
			player.controlUp = false;
			player.controlDown = false;
			player.controlJump = false;
			player.controlUseItem = false;
			player.controlUseTile = false;
			player.controlHook = false;
			player.controlThrow = false;
			player.controlMount = false;
			player.controlTorch = false;
			player.controlSmart = false;
			player.controlQuickHeal = false;
			player.controlQuickMana = false;

			// Player.Update copies the trigger set into the controls after this
			// hook, so clearing the player fields alone is not enough. Clear the
			// triggers too, otherwise held movement keys still walk the player and
			// B (bookmark) would drink a buff potion in CopyInto.
			try {
				var t = Terraria.GameInput.PlayerInput.Triggers.Current;
				t.Up = false;
				t.Down = false;
				t.Left = false;
				t.Right = false;
				t.Jump = false;
				t.Throw = false;
				t.Inventory = false;
				t.Grapple = false;
				t.SmartSelect = false;
				t.SmartCursor = false;
				t.QuickMount = false;
				t.QuickHeal = false;
				t.QuickMana = false;
				t.QuickBuff = false;
				t.Loadout1 = false;
				t.Loadout2 = false;
				t.Loadout3 = false;
				t.MapFull = false;
				t.MapStyle = false;
				t.OpenCreativePowersMenu = false;
				t.LockOn = false;
				t.MouseLeft = false;
				t.MouseRight = false;
				t.Hotbar1 = false;
				t.Hotbar2 = false;
				t.Hotbar3 = false;
				t.Hotbar4 = false;
				t.Hotbar5 = false;
				t.Hotbar6 = false;
				t.Hotbar7 = false;
				t.Hotbar8 = false;
				t.Hotbar9 = false;
				t.Hotbar10 = false;
				t.HotbarMinus = false;
				t.HotbarPlus = false;
				// The book's scroll handler reads the delta before this helper;
				// consume it so the hotbar cannot scroll behind the modal page.
				PlayerInput.ScrollWheelDelta = 0;
			}
			catch { }
		}

		private static char KeyToChar(Keys key, bool shift)
		{
			if (key >= Keys.A && key <= Keys.Z) {
				char c = (char)('a' + (key - Keys.A));
				return shift ? char.ToUpperInvariant(c) : c;
			}
			if (key >= Keys.D0 && key <= Keys.D9)
				return (char)('0' + (key - Keys.D0));
			if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
				return (char)('0' + (key - Keys.NumPad0));
			return key switch {
				Keys.Space => ' ',
				Keys.OemMinus => '-',
				Keys.OemPeriod => '.',
				Keys.OemComma => ',',
				Keys.OemQuestion => '/',
				Keys.OemPlus => '+',
				Keys.OemOpenBrackets => '(',
				Keys.OemCloseBrackets => ')',
				_ => '\0',
			};
		}

		private static void UpdateSearchTyping(Keys[] keys)
		{
			// Backspace repeats while held.
			if (Main.keyState.IsKeyDown(Keys.Back)) {
				if (backspaceCooldown <= 0) {
					if (searchText.Length > 0)
						searchText = searchText.Substring(0, searchText.Length - 1);
					backspaceCooldown = 5;
				}
				else {
					backspaceCooldown--;
				}
			}
			else {
				backspaceCooldown = 0;
			}

			bool shift = ShiftDown();
			foreach (Keys key in keys) {
				if (!KeyPressed(keys, key))
					continue;
				char c = KeyToChar(key, shift);
				if (c != '\0' && searchText.Length < 30)
					searchText += c;
			}
		}

		private static void HandleClick(Point mp, Player player)
		{
			if (showShortcuts && shortcutCardRect.Width > 0) {
				if (shortcutCardRect.Contains(mp))
					return;
				if (!shortcutBtn.Contains(mp)) {
					showShortcuts = false;
					return;
				}
			}

			if (Page < 0) {
				// Closed cover: the X closes, a volume row re-opens elsewhere,
				// and a click on the board unlocks this volume.
				if (closeBtn.Contains(mp)) {
					CloseBook();
					return;
				}
				if (shortcutBtn.Contains(mp)) {
					showShortcuts = !showShortcuts;
					return;
				}
				if (resumeBtn.Contains(mp)) {
					ResumeFromCover();
					return;
				}
				foreach (var (tabRect, book) in volumeTabs) {
					if (tabRect.Contains(mp) && book != BookId) {
						SwitchVolume(book);
						return;
					}
				}
				if (lastRect.Contains(mp))
					NextPage();
				return;
			}

			if (closeBtn.Contains(mp)) {
				CloseBook();
				return;
			}
			if (shortcutBtn.Contains(mp)) {
				showShortcuts = !showShortcuts;
				SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.18f, MaxInstances = 2 });
				return;
			}
			if (scrubberTrack.Contains(mp) || scrubberThumb.Contains(mp)) {
				draggingScrubber = true;
				scrubberGrabOffset = mp.X - scrubberThumb.Center.X;
				SetPageFromScrubber(mp.X);
				return;
			}
			if (!twoPage) {
				for (int i = 0; i < leftTabRects.Length; i++) {
					if (leftTabRects[i].Width > 0 && leftTabRects[i].Contains(mp)) {
						SetLeftTab((LeftTab)i);
						return;
					}
				}
			}
			if (BetterBook && backBtn.Contains(mp)) {
				GoBack();
				return;
			}
			if (prevBtn.Contains(mp)) {
				PrevPage();
				return;
			}
			if (nextBtn.Contains(mp)) {
				NextPage();
				return;
			}
			if (edgeTab.Contains(mp)) {
				ToggleTwoPage();
				return;
			}
			if (BetterBook && searchActive) {
				if (searchAllRect.Contains(mp)) {
					if (!showAllVolumes) {
						showAllVolumes = true;
						searchCacheKey = null;
					}
					return;
				}
				if (searchThisRect.Contains(mp)) {
					if (showAllVolumes) {
						showAllVolumes = false;
						searchCacheKey = null;
					}
					return;
				}
				if (searchTextRect.Contains(mp)) {
					if (searchTitlesOnly) {
						searchTitlesOnly = false;
						searchCacheKey = null;
					}
					return;
				}
				if (searchTitleRect.Contains(mp)) {
					if (!searchTitlesOnly) {
						searchTitlesOnly = true;
						searchCacheKey = null;
					}
					return;
				}
			}
			if (BetterBook && marksOpen) {
				if (marksClearRect.Contains(mp)) {
					ClearVolumeBookmarks();
					return;
				}
				foreach (var (rect, book, source) in bookmarkRemoveRects) {
					if (rect.Contains(mp)) {
						ToggleBookmark(book, source);
						return;
					}
				}
			}
			if (BetterBook && chaptersOpen) {
				if (chapterBackRect.Contains(mp)) {
					chaptersOpen = false;
					chapterScroll = 0;
					leafAnim = 0f;
					return;
				}
				foreach (var (rect, book, source) in chapterRects) {
					if (rect.Contains(mp)) {
						JumpToBookmark(book, source);
						return;
					}
				}
			}
			if (searchActive && searchClearRect.Contains(mp)) {
				searchText = "";
				searchCacheKey = null;
				searchHits.Clear();
				leafAnim = 0f;
				return;
			}
			if (!twoPage && !chaptersOpen && searchField.Contains(mp)) {
				searchActive = true;
				leafAnim = 0f;
				return;
			}

			foreach (var link in linkRects) {
				if (link.rect.Contains(mp)) {
					JumpTo(link.book, link.title);
					return;
				}
			}
			foreach (var hit in searchRects) {
				if (hit.rect.Contains(mp)) {
					JumpToSearch(hit.hit);
					return;
				}
			}
			foreach (var row in bookmarkRowRects) {
				if (row.rect.Contains(mp)) {
					JumpToBookmark(row.book, row.sourceIndex);
					return;
				}
			}
			foreach (var (tabRect, book) in volumeTabs) {
				if (tabRect.Contains(mp)) {
					if (book != BookId)
						SwitchVolume(book);
					else if (BetterBook)
						ToggleChapters();
					return;
				}
			}
			foreach (var ribbon in bookmarkRibbonRects) {
				if (ribbon.rect.Contains(mp)) {
					ToggleBookmark(ribbon.book, ribbon.sourceIndex);
					return;
				}
			}
			foreach (var (iconRect, _) in iconRects) {
				if (iconRect.Contains(mp)) {
					// Inspecting an item never turns the page.
					return;
				}
			}
			foreach (var stamp in stampRects) {
				if (stamp.rect.Contains(mp)) {
					// The footer stamp is a progress mark, not a control.
					return;
				}
			}

			// A click away from the search box ends the search.
			if (searchActive) {
				searchActive = false;
				searchText = "";
				searchCacheKey = null;
				searchHits.Clear();
				leafAnim = 0f;
			}

			if (!lastRect.Contains(mp))
				return;

			// Clicking the page itself turns it, like a real book.
			int middle = lastRect.X + lastRect.Width / 2;
			if (mp.X >= middle)
				NextPage();
			else
				PrevPage();
		}

		public override void PostUpdateInput()
		{
			if (Main.gameMenu || Main.dedServ || !Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active) {
				prevLeft = Main.mouseLeft;
				prevKeys = Main.keyState.GetPressedKeys();
				return;
			}

			// While the book is closing, keep the world frozen but ignore input.
			if (closing) {
				SuppressGameplayInput(player);
				prevKeys = Main.keyState.GetPressedKeys();
				prevLeft = Main.mouseLeft;
				return;
			}

			Keys[] keys = Main.keyState.GetPressedKeys();

			// One Escape dismisses one layer (search, then chapters, then the
			// book). Keep the key swallowed until it is released so the same
			// press cannot fall through and close the tome behind it.
			if (escapeLatch) {
				SuppressGameplayInput(player);
				if (!Main.keyState.IsKeyDown(Keys.Escape))
					escapeLatch = false;
				prevKeys = keys;
				prevLeft = Main.mouseLeft;
				return;
			}

			// Escape dismisses one layer at a time: shortcut card, search, a
			// left-leaf tab, then the book itself.
			if (showShortcuts && KeyPressed(keys, Keys.Escape)) {
				showShortcuts = false;
				escapeLatch = true;
				SuppressGameplayInput(player);
				prevKeys = keys;
				prevLeft = Main.mouseLeft;
				return;
			}
			if (BetterBook && leftTab != LeftTab.Contents && KeyPressed(keys, Keys.Escape)) {
				CancelSearch();
				SetLeftTab(LeftTab.Contents);
				escapeLatch = true;
				SuppressGameplayInput(player);
				prevKeys = keys;
				prevLeft = Main.mouseLeft;
				return;
			}

			if (KeyPressed(keys, Keys.OemQuestion)) {
				showShortcuts = !showShortcuts;
				SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.18f, MaxInstances = 2 });
			}

			if (searchActive) {
				if (KeyPressed(keys, Keys.Tab)) {
					CycleLeftTab(ShiftDown() ? -1 : 1);
					prevKeys = keys;
					SuppressGameplayInput(player);
					prevLeft = Main.mouseLeft;
					return;
				}
				if (KeyPressed(keys, Keys.Enter) && searchHits.Count > 0) {
					JumpToSearch(searchHits[0]);
					prevKeys = keys;
					SuppressGameplayInput(player);
					prevLeft = Main.mouseLeft;
					return;
				}
				UpdateSearchTyping(keys);
			}
			else {
				if (KeyPressed(keys, Keys.Escape)) {
					// Consume the frame: otherwise Escape's Inventory trigger opens
					// the inventory as the book closes.
					SuppressGameplayInput(player);
					prevKeys = keys;
					prevLeft = Main.mouseLeft;
					CloseBook();
					return;
				}
				if (KeyPressed(keys, Keys.B))
					ToggleBookmarkHere();
				if (KeyPressed(keys, Keys.T))
					ToggleTwoPage();
				if (BetterBook && Page >= 0 && KeyPressed(keys, Keys.F)) {
					if (twoPage)
						ToggleTwoPage();
					SetLeftTab(LeftTab.Search);
				}
				if (BetterBook && KeyPressed(keys, Keys.C)) {
					if (twoPage)
						ToggleTwoPage();
					ToggleChapters();
				}
				if (BetterBook && KeyPressed(keys, Keys.M)) {
					if (twoPage)
						ToggleTwoPage();
					ToggleMarks();
				}
				if (BetterBook && KeyPressed(keys, Keys.Tab)) {
					if (twoPage)
						ToggleTwoPage();
					CycleLeftTab(ShiftDown() ? -1 : 1);
					prevKeys = keys;
					SuppressGameplayInput(player);
					prevLeft = Main.mouseLeft;
					return;
				}
				if (BetterBook && KeyPressed(keys, Keys.Back) && history.Count > 0)
					GoBack();
				// Arrows turn pages (repeat gated by turnTimer).
				if (Main.keyState.IsKeyDown(Keys.Right) && turnTimer <= 0)
					NextPage();
				else if (Main.keyState.IsKeyDown(Keys.Left) && turnTimer <= 0)
					PrevPage();

				// The wheel pages the book, or scrolls the chapter index.
				int wheel = PlayerInput.ScrollWheelDelta;
				if (BetterBook && wheel != 0) {
					int dir = Math.Sign(wheel);
					if (chaptersOpen) {
						chapterScroll = Math.Clamp(chapterScroll - dir, 0, chapterMaxScroll);
					}
					else if (marksOpen) {
						marksScroll = Math.Clamp(marksScroll - dir, 0, marksMaxScroll);
					}
					else if (turnTimer <= 0) {
						if (dir < 0)
							NextPage();
						else
							PrevPage();
					}
				}
			}

			bool left = Main.mouseLeft;
			if (draggingScrubber) {
				if (left)
					SetPageFromScrubber((int)UiMouse.X);
				else {
					draggingScrubber = false;
					PaperTurn();
				}
			}
			if (left && !prevLeft && !Main.blockMouse) {
				Point mp = UiMouse.ToPoint();
				HandleClick(mp, player);
			}

			// The book is a modal reader: no hotbar, inventory or world input
			// should leak through the pages.
			SuppressGameplayInput(player);
			prevLeft = left;
			prevKeys = keys;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!Open)
				return;
			if (closing) {
				if (closeTimer > 0)
					closeTimer--;
				if (closeTimer <= 0) {
					Open = false;
					closing = false;
					searchActive = false;
					escapeLatch = false;
					searchText = "";
					chaptersOpen = false;
				}
				return;
			}
			if (turnTimer > 0)
				turnTimer--;
			if (unfoldTimer > 0)
				unfoldTimer--;
			if (openTimer > 0)
				openTimer--;
			if (leafAnim < 1f)
				leafAnim = MathHelper.Min(1f, leafAnim + 0.16f);
			float tabTarget = (int)leftTab;
			leftTabGlide = MathHelper.Lerp(leftTabGlide, tabTarget, 0.30f);
			if (Math.Abs(leftTabGlide - tabTarget) < 0.004f)
				leftTabGlide = tabTarget;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				CloseBook();
		}

		public override void OnWorldUnload()
		{
			// A book left open when leaving a world must not paint over the menu.
			SaveReadingPosition();
			Open = false;
			turnTimer = 0;
			unfoldTimer = 0;
			openTimer = 0;
			closeTimer = 0;
			closing = false;
			leafAnim = 1f;
			prevLeft = false;
			searchActive = false;
			escapeLatch = false;
			searchText = "";
			searchClearRect = Rectangle.Empty;
			leftTabGlide = 0f;
			chaptersOpen = false;
			chapterScroll = 0;
			history.Clear();
			layoutValid = false;
			DisposeSkin();
		}

		public override void Unload()
		{
			DisposeSkin();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Yoyo Guidebook",
					() => {
						DrawBook(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		// --------------------------------------------------------------- skin bake

		private static void DisposeSkin()
		{
			try { skinTarget?.Dispose(); } catch { }
			try { coverTarget?.Dispose(); } catch { }
			try { turnTarget?.Dispose(); } catch { }
			skinTarget = null;
			coverTarget = null;
			turnTarget = null;
			skinBakeFailed = false;
			coverBakeFailed = false;
			turnTargetFailed = false;
			skinAccent = Color.Transparent;
			coverAccent = Color.Transparent;
			coverBook = "";
			coverW = 0;
			coverH = 0;
			turnTargetPage = -1;
			turnTargetW = 0;
			turnTargetH = 0;
			turnTargetBook = "";
			turnFromPage = -1;
			turnFromBook = "";
		}

		/// <summary>
		/// Renders the static leather/paper/grain layer into a render target once
		/// per size and accent. The batch has to be restarted around the target;
		/// the UI matrix is restored in finally so no later layer is displaced.
		/// </summary>
		private static void EnsureSkin(SpriteBatch sb, int pw, int ph, Color accent)
		{
			if (skinBakeFailed)
				return;
			if (skinTarget != null && !skinTarget.IsDisposed
				&& skinTarget.Width == pw && skinTarget.Height == ph && skinAccent == accent)
				return;

			RenderTarget2D fresh = null;
			bool batchEnded = false;
			bool rtBatchActive = false;
			try {
				GraphicsDevice gd = Main.instance.GraphicsDevice;
				fresh = new RenderTarget2D(gd, pw, ph, false, SurfaceFormat.Color,
					DepthFormat.None, 0, RenderTargetUsage.DiscardContents);

				sb.End();
				batchEnded = true;
				gd.SetRenderTarget(fresh);
				gd.Clear(Color.Transparent);
				// Match Main.DrawInterface exactly: PointClamp, AlphaBlend, no
				// depth, cull counter-clockwise, identity for the target.
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
					DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Matrix.Identity);
				rtBatchActive = true;
				YoyoGuidebookBookSkin.DrawSpread(sb, new Rectangle(0, 0, pw, ph), 1f, accent);
				sb.End();
				rtBatchActive = false;
				gd.SetRenderTarget(null);

				try { skinTarget?.Dispose(); } catch { }
				skinTarget = fresh;
				fresh = null;
				skinAccent = accent;
			}
			catch {
				skinBakeFailed = true;
				if (rtBatchActive) {
					try { sb.End(); } catch { }
				}
				if (fresh != null) {
					try { fresh.Dispose(); } catch { }
				}
				try { Main.instance.GraphicsDevice.SetRenderTarget(null); } catch { }
			}
			finally {
				if (batchEnded) {
					try {
						sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
							DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
					}
					catch { }
				}
			}
		}

		/// <summary>Bakes the closed cover once so the opening animation can
		/// compress the real board into a narrow hinged slab instead of sliding a
		/// flat overlay across the spread.</summary>
		private static void EnsureCoverSkin(SpriteBatch sb, int cw, int ch, Color accent)
		{
			if (coverBakeFailed || cw <= 0 || ch <= 0)
				return;
			if (coverTarget != null && !coverTarget.IsDisposed
				&& coverW == cw && coverH == ch && coverAccent == accent
				&& coverBook == BookId)
				return;

			RenderTarget2D fresh = null;
			bool batchEnded = false;
			bool rtBatchActive = false;
			try {
				GraphicsDevice gd = Main.instance.GraphicsDevice;
				fresh = new RenderTarget2D(gd, cw, ch, false, SurfaceFormat.Color,
					DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
				sb.End();
				batchEnded = true;
				gd.SetRenderTarget(fresh);
				gd.Clear(Color.Transparent);
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
					DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Matrix.Identity);
				rtBatchActive = true;
				DrawClosedBookCover(sb, new Rectangle(0, 0, cw, ch), 1f, accent, false);
				sb.End();
				rtBatchActive = false;
				gd.SetRenderTarget(null);
				try { coverTarget?.Dispose(); } catch { }
				coverTarget = fresh;
				fresh = null;
				coverW = cw;
				coverH = ch;
				coverAccent = accent;
				coverBook = BookId;
			}
			catch {
				coverBakeFailed = true;
				if (rtBatchActive) {
					try { sb.End(); } catch { }
				}
				if (fresh != null) {
					try { fresh.Dispose(); } catch { }
				}
				try { Main.instance.GraphicsDevice.SetRenderTarget(null); } catch { }
			}
			finally {
				if (batchEnded) {
					try {
						sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
							DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
					}
					catch { }
				}
			}
		}

		/// <summary>Bakes the printed content of the page that is about to turn.
		/// The target is transparent; the sheet supplies its own paper body and
		/// this layer gives the compressed columns real type instead of a blank
		/// white card.</summary>
		private static void EnsureTurnTarget(SpriteBatch sb, int x, int y, int pw, int ph, Color accent)
		{
			if (turnTimer <= 0 || turnFromPage < 0 || turnFromBook != BookId
				|| turnFromPage >= layout.Count || turnTargetFailed)
				return;

			int sideW = SideWidth(pw);
			Rectangle pageRect = turnFromLeft
				? new Rectangle(x + 16, y + 14, sideW - 20, ph - 27)
				: RightPageRect(x, y, pw, ph);
			if (turnTarget != null && !turnTarget.IsDisposed
				&& turnTargetPage == turnFromPage && turnTargetBook == BookId
				&& turnTargetW == pageRect.Width && turnTargetH == pageRect.Height)
				return;

			RenderTarget2D fresh = null;
			bool batchEnded = false;
			bool rtBatchActive = false;
			int links = linkRects.Count;
			int icons = iconRects.Count;
			int stamps = stampRects.Count;
			try {
				GraphicsDevice gd = Main.instance.GraphicsDevice;
				fresh = new RenderTarget2D(gd, pageRect.Width, pageRect.Height, false,
					SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
				sb.End();
				batchEnded = true;
				gd.SetRenderTarget(fresh);
				gd.Clear(Color.Transparent);
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
					DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Matrix.Identity);
				rtBatchActive = true;
				// The page block begins 14px below the book's text origin and has
				// 16px inset margins; baking with that offset keeps the running
				// head, prose and folio in the same physical places.
				DrawReadingPage(sb, turnFromPage, 30, ReadingWidth(pw), -14, ph, 1f,
					accent, turnFromLeft);
				sb.End();
				rtBatchActive = false;
				gd.SetRenderTarget(null);
				try { turnTarget?.Dispose(); } catch { }
				turnTarget = fresh;
				fresh = null;
				turnTargetPage = turnFromPage;
				turnTargetBook = BookId;
				turnTargetW = pageRect.Width;
				turnTargetH = pageRect.Height;
			}
			catch {
				turnTargetFailed = true;
				if (rtBatchActive) {
					try { sb.End(); } catch { }
				}
				if (fresh != null) {
					try { fresh.Dispose(); } catch { }
				}
				try { Main.instance.GraphicsDevice.SetRenderTarget(null); } catch { }
			}
			finally {
				if (linkRects.Count > links)
					linkRects.RemoveRange(links, linkRects.Count - links);
				if (iconRects.Count > icons)
					iconRects.RemoveRange(icons, iconRects.Count - icons);
				if (stampRects.Count > stamps)
					stampRects.RemoveRange(stamps, stampRects.Count - stamps);
				if (batchEnded) {
					try {
						sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
							DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
					}
					catch { }
				}
			}
		}

		// ------------------------------------------------------------------- draw

		private static Rectangle RightPageRect(int x, int y, int pw, int ph)
			=> new(x + pw / 2 + 4, y + 14, pw / 2 - 21, ph - 27);

		/// <summary>A quiet vignette behind the tome. It keeps the world readable
		/// at the edges while making the cream paper the brightest thing on screen.</summary>
		private static void DrawBackdrop(SpriteBatch sb, float alpha)
		{
			int w = UiWidth;
			int h = UiHeight;
			FillRect(sb, new Rectangle(0, 0, w, h), new Color(4, 7, 13) * (0.34f * alpha));

			Texture2D vignette = UiArt.Vignette;
			if (vignette != null) {
				sb.Draw(vignette, new Rectangle(0, 0, w, h), new Color(0, 0, 0) * (0.60f * alpha));
			}
			else {
				int edge = Math.Max(24, Math.Min(w, h) / 9);
				Color edgeColor = new Color(0, 0, 0) * (0.12f * alpha);
				FillRect(sb, new Rectangle(0, 0, w, edge), edgeColor);
				FillRect(sb, new Rectangle(0, h - edge, w, edge), edgeColor);
				FillRect(sb, new Rectangle(0, edge, edge, h - edge * 2), edgeColor);
				FillRect(sb, new Rectangle(w - edge, edge, edge, h - edge * 2), edgeColor);
			}

			// A warm pool of lamp light behind the book, not a flat black screen.
			UiArt.DrawGlow(sb, new Vector2(w * 0.5f, h * 0.52f), Math.Max(w, h) * 0.50f,
				new Color(255, 228, 176) * (0.065f * alpha));
		}

		/// <summary>Slow, deterministic dust in the lamplight. Cheap motes drawn
		/// behind the book so they read as atmosphere rather than screen dirt.</summary>
		private static void DrawMotes(SpriteBatch sb, Rectangle area, float alpha)
		{
			if (!BetterBook || alpha <= 0.01f)
				return;
			float time = Main.GlobalTimeWrappedHourly;
			Color mote = new Color(255, 236, 198);
			for (int i = 0; i < 18; i++) {
				float seed = i * 0.618034f;
				float x = area.X - 50f + ((seed * 7919f) % 1f) * (area.Width + 100f)
					+ (float)Math.Sin(time * (0.16f + seed * 0.22f) + seed * 6.28f) * 16f;
				float span = area.Height + 130f;
				float y = area.Y - 65f + ((time * (5f + seed * 9f) + seed * span) % span);
				float twinkle = 0.5f + 0.5f * (float)Math.Sin(time * 2.1f + seed * 13f);
				byte a = (byte)(16 + 26 * twinkle);
				FillRect(sb, new Rectangle((int)x, (int)y, i % 5 == 0 ? 2 : 1, 1),
					mote * (a / 255f * alpha));
			}
		}

		private static void DrawBook(SpriteBatch sb)
		{
			if (!Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;

			EnsureLayout();
			if (layout.Count == 0)
				return;

			if (twoPage && Page >= 0)
				Page = Math.Clamp(Page, 0, layout.Count - 1);
			else
				Page = Math.Clamp(Page, -1, layout.Count - 1);

			int pw = BookWidth();
			int ph = BookHeight();
			int x = (UiWidth - pw) / 2;

			// Open: fade + a short lift with an ease-out settle. Never a scale:
			// scaling only the boards left the type at full size hanging over a
			// shrunken page. Close mirrors it with a quicker sink and fade.
			float openT = openTimer > 0 ? 1f - openTimer / (float)OpenFrames : 1f;
			openT = EaseOutCubic(MathHelper.Clamp(openT, 0f, 1f));
			float closeT = closing
				? 1f - Math.Max(0, closeTimer - 1) / (float)Math.Max(1, CloseFrames - 1)
				: 0f;
			closeT = MathHelper.Clamp(closeT, 0f, 1f);
			float closeFade = 1f - closeT;
			float bookAlpha = openT * closeFade * closeFade;
			if (bookAlpha <= 0.002f) {
				lastRect = Rectangle.Empty;
				return;
			}
			int rise = (int)((1f - openT) * 26f + EaseInCubic(closeT) * 18f);
			int y = (UiHeight - ph) / 2 + rise;
			// Opening lifts the book by 26 px; near the minimum height that
			// would push the folio below the screen. Keep the whole board on
			// screen and let the fade carry the motion instead.
			y = Math.Clamp(y, 0, Math.Max(0, UiHeight - ph));

			bool closed = Page < 0;
			Rectangle spread = new(x, y, pw, ph);
			int closedW = Math.Min(pw, Math.Max(300, pw / 2));
			Rectangle cover = new(x + (pw - closedW) / 2, y, closedW, ph);
			Rectangle frame = closed ? cover : spread;
			lastRect = frame;
			if (frame.Contains(UiMouse.ToPoint()))
				player.mouseInterface = true;

			float unfold = unfoldTimer > 0 ? 1f - unfoldTimer / (float)UnfoldFrames : 1f;
			float contentAlpha = bookAlpha;
			if (!closed && unfoldTimer > 0)
				contentAlpha *= MathHelper.Lerp(0.45f, 1f, unfold);
			if (turnTimer > 0)
				contentAlpha *= MathHelper.Lerp(1f, 0.62f, turnTimer / (float)TurnFrames);

			Color accent = YoyoGuidebookIcons.BookAccent(BookId);

			// Bake first: the call may end/restart the UI batch, so nothing may
			// be drawn before it in this pass except the shadow (also after).
			if (!closed) {
				EnsureSkin(sb, pw, ph, accent);
				if (unfoldTimer > 0)
					EnsureCoverSkin(sb, cover.Width, cover.Height, accent);
				if (turnTimer > 0)
					EnsureTurnTarget(sb, x, y, pw, ph, accent);
			}

			// Better Book dims the world just enough to make the paper win the
			// contrast fight without hiding the player's surroundings entirely.
			if (BetterBook && !closed) {
				DrawBackdrop(sb, bookAlpha);
				DrawMotes(sb, spread, bookAlpha);
			}

			YoyoGuidebookBookSkin.DrawShadow(sb, frame, bookAlpha);

			if (!closed) {
				if (skinTarget != null && !skinTarget.IsDisposed && !skinBakeFailed)
					sb.Draw(skinTarget, new Vector2(x, y), Color.White * bookAlpha);
				else
					YoyoGuidebookBookSkin.DrawSpread(sb, spread, bookAlpha, accent);
			}

			// Reset all per-frame hit regions.
			volumeTabs.Clear();
			iconRects.Clear();
			linkRects.Clear();
			bookmarkRibbonRects.Clear();
			bookmarkRowRects.Clear();
			bookmarkRemoveRects.Clear();
			searchRects.Clear();
			stampRects.Clear();
			chapterRects.Clear();
			for (int i = 0; i < leftTabRects.Length; i++)
				leftTabRects[i] = Rectangle.Empty;
			prevBtn = Rectangle.Empty;
			nextBtn = Rectangle.Empty;
			backBtn = Rectangle.Empty;
			edgeTab = Rectangle.Empty;
			searchField = Rectangle.Empty;
			searchClearRect = Rectangle.Empty;
			chapterBackRect = Rectangle.Empty;
			resumeBtn = Rectangle.Empty;
			shortcutBtn = Rectangle.Empty;
			shortcutCardRect = Rectangle.Empty;
			scrubberTrack = Rectangle.Empty;
			scrubberThumb = Rectangle.Empty;
			searchAllRect = Rectangle.Empty;
			searchThisRect = Rectangle.Empty;
			searchTextRect = Rectangle.Empty;
			searchTitleRect = Rectangle.Empty;
			marksClearRect = Rectangle.Empty;
			chapterMaxScroll = 0;
			marksMaxScroll = 0;

			if (closed) {
				DrawClosedBookCover(sb, cover, bookAlpha, accent, true);
				closeBtn = new Rectangle(cover.Right - 42, cover.Y + 14, 26, 26);
				Button(sb, closeBtn, "X", false, bookAlpha);

				int saved = -1;
				try { saved = Bookmarks()?.RememberedPage(BookId) ?? -1; }
				catch { }
				if (saved >= 0 && saved < layout.Count) {
					resumeBtn = new Rectangle(cover.X + cover.Width / 2 - 88, cover.Bottom - 100, 176, 27);
					Button(sb, resumeBtn, $"RESUME  PAGE {saved + 1}", false, bookAlpha);
				}

				shortcutBtn = new Rectangle(cover.X + 15, cover.Y + 15, 26, 26);
				Button(sb, shortcutBtn, "?", false, bookAlpha);
				if (showShortcuts)
					DrawShortcutCard(sb, cover, bookAlpha, accent);
				return;
			}

			int sideW = SideWidth(pw);
			int pageInset = PageInset(pw);
			int rightPage = twoPage ? Page + 1 : Page;
			if (twoPage)
				DrawReadingPage(sb, Page, x + pageInset, ReadingWidth(pw), y, ph,
					contentAlpha, accent, true);
			DrawReadingPage(sb, rightPage, x + sideW + pageInset, ReadingWidth(pw), y, ph,
				contentAlpha, accent, false);

			if (!twoPage)
				DrawContentsLeaf(sb, player, x, y, sideW, ph, bookAlpha, accent);

			// Page-turn sheet: over the print, under the folio, arrows and
			// tooltips so those controls stay usable mid-turn. In a spread a
			// backward turn lifts the left page; everything else uses the right.
			if (turnTimer > 0) {
				Rectangle turningPage = twoPage && turnDir < 0
					? new Rectangle(x + 16, y + 14, sideW - 20, ph - 27)
					: RightPageRect(x, y, pw, ph);
				Texture2D leafPrint = turnTarget != null && !turnTarget.IsDisposed
					&& turnTargetPage == turnFromPage && turnTargetBook == BookId
					&& turnFromBook == BookId ? turnTarget : null;
				YoyoGuidebookBookSkin.DrawTurningSheet(sb, turningPage,
					turnTimer / (float)TurnFrames, turnDir, bookAlpha, leafPrint);
			}

			DrawBookmarkRibbons(sb, x, y, sideW, ph, bookAlpha, rightPage);
			DrawBookFooter(sb, player, x, y, pw, ph, sideW, bookAlpha, accent);

			// Cover hinging open over the fresh spread. The board first settles
			// from its centered closed position to the left page's hinge, then
			// compresses into the gutter. Its real baked face is sliced into
			// columns, so the title and tooling squeeze with the leather instead
			// of sliding off as a flat card.
			if (unfoldTimer > 0 && unfold < 1f) {
				float settle = EaseOutCubic(MathHelper.Clamp(unfold / 0.30f, 0f, 1f));
				float hinge = EaseInCubic(MathHelper.Clamp((unfold - 0.16f) / 0.84f, 0f, 1f));
				float a = bookAlpha * (1f - hinge);
				if (a > 0.01f) {
					int coverX = (int)MathHelper.Lerp(cover.X, x, settle);
					float widthNow = cover.Width * (1f - hinge);
					if (widthNow < 2f)
						widthNow = 2f;
					int freeX = coverX + (int)widthNow;
					float arc = (float)Math.Sin(Math.PI * hinge);
					bool canSlice = coverTarget != null && !coverTarget.IsDisposed && !coverBakeFailed;

					// Shadow cast by the lifting board onto the revealed leaf.
					Texture2D fade = UiArt.FadeH;
					if (fade != null && arc > 0.01f) {
						int shadowW = (int)(100f * (0.45f + 0.55f * arc));
						Rectangle shadow = new(freeX, cover.Y + 8, shadowW, cover.Height - 16);
						sb.Draw(fade, shadow, null, new Color(30, 19, 11) * (0.30f * arc * a),
							0f, Vector2.Zero, SpriteEffects.None, 0f);
					}

					// Page fan: thin paper silhouettes spring out of the gutter
					// behind the board, then settle into the spread.
					for (int i = 5; i >= 1; i--) {
						float k = i / 5f;
						int fanX = freeX + (int)(k * 18f * (0.4f + hinge));
						int fanY = cover.Y + 15 + i * 3;
						int fanH = cover.Height - 30 - i * 6;
						Color leaf = Color.Lerp(YoyoGuidebookBookSkin.PaperShade,
							YoyoGuidebookBookSkin.Paper, k) * (a * (0.22f + 0.28f * arc));
						FillRect(sb, new Rectangle(fanX, fanY, 3, Math.Max(1, fanH)), leaf);
						FillRect(sb, new Rectangle(fanX, fanY, 1, Math.Max(1, fanH)),
							YoyoGuidebookBookSkin.PaperEdge * (a * 0.55f));
					}

					if (canSlice) {
						for (int sx = 0; sx < cover.Width; sx += 2) {
							float u = (sx + 1f) / cover.Width;
							int destX = coverX + (int)(sx * (1f - hinge));
							int destW = Math.Max(1, (int)(2f * (1f - hinge)) + 1);
							int lift = (int)(Math.Sin(u * Math.PI) * 7f * hinge);
							int destY = cover.Y - lift;
							int destH = cover.Height + lift + (int)(3f * hinge * Math.Sin(u * Math.PI));
							Rectangle src = new(sx, 0, Math.Min(2, cover.Width - sx), cover.Height);
							sb.Draw(coverTarget, new Rectangle(destX, destY, destW, destH), src,
								Color.White * a, 0f, Vector2.Zero, SpriteEffects.None, 0f);
						}

						// The inner board becomes visible after the halfway point:
						// dark leather, a gold fillet, and the fore-edge page block.
						float back = MathHelper.Clamp((hinge - 0.42f) / 0.35f, 0f, 1f);
						if (back > 0.01f) {
							FillRect(sb, new Rectangle(coverX, cover.Y + 4,
								Math.Max(1, (int)widthNow), cover.Height - 8),
								new Color(52, 31, 20) * (0.62f * back * a));
							FillRect(sb, new Rectangle(freeX - 2, cover.Y + 8,
								1, cover.Height - 16),
								YoyoGuidebookBookSkin.Gold * (0.62f * back * a));
							FillRect(sb, new Rectangle(freeX - 5, cover.Y + 12,
								1, cover.Height - 24),
								YoyoGuidebookBookSkin.GoldDim * (0.6f * back * a));
						}
					}
					else {
						DrawClosedBookCover(sb, new Rectangle(coverX, cover.Y,
							Math.Max(1, (int)widthNow), cover.Height), a, accent, false);
					}

					// A hot leading edge keeps the moving board legible against
					// the dark backdrop.
					FillRect(sb, new Rectangle(freeX, cover.Y + 6, 1, cover.Height - 12),
						YoyoGuidebookBookSkin.Gold * (0.55f * a));
				}
			}

			if (showShortcuts)
				DrawShortcutCard(sb, spread, bookAlpha, accent);
		}

		private static float EaseOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return 1f - (1f - t) * (1f - t) * (1f - t);
		}

		private static float EaseInCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * t;
		}

		// ------------------------------------------------------------- contents leaf

		/// <summary>Chapter index for the open volume: every authored entry with
		/// its printed page, scrollable with the wheel. This is the browse
		/// counterpart to search and replaces the contents rows while open.</summary>
		private static void DrawChapterIndex(SpriteBatch sb, Player player, int x, int y, int sideW, int ph,
			float alpha, Color accent)
		{
			int leafX = x + 24;
			int leafW = sideW - 54;
			int footerTop = y + ph - 98;

			var authored = YoyoGuidebookData.PagesOf(BookId);
			if (authored.Count == 0) {
				DrawInk(sb, "No chapters in this volume.",
					new Vector2(leafX, y + 72), YoyoGuidebookBookSkin.InkDim * alpha, 0.68f);
				return;
			}

			int rowTop = y + 58;
			int rowH = 28;
			int maxRows = Math.Max(1, (footerTop - rowTop) / rowH);
			chapterMaxScroll = Math.Max(0, authored.Count - maxRows);
			chapterScroll = Math.Clamp(chapterScroll, 0, chapterMaxScroll);
			int currentSource = Page >= 0 && Page < layout.Count ? layout[Page].SourceIndex : -1;

			for (int i = chapterScroll; i < authored.Count && i < chapterScroll + maxRows; i++) {
				GuidePage page = authored[i];
				Rectangle row = new(leafX, rowTop, leafW, rowH - 2);
				bool sel = i == currentSource;
				bool hov = row.Contains(UiMouse.ToPoint());
				if (hov || sel) {
					FillRect(sb, row, (sel ? new Color(255, 255, 255, 48) : new Color(255, 255, 255, 26)) * alpha);
					if (sel)
						FillRect(sb, new Rectangle(row.X, row.Y, 2, row.Height), accent * alpha);
					player.mouseInterface = true;
				}

				string number = (i + 1).ToString("00");
				YoyoGuidebookBookSkin.DrawTracked(sb, number, new Vector2(row.X + 7, row.Y + 6),
					YoyoGuidebookBookSkin.InkDim * (0.8f * alpha), 0.5f, 1f);
				string title = FitText(page.Title, Math.Max(32, leafW - 82), 0.7f);
				DrawInk(sb, title, new Vector2(row.X + 34, row.Y + 3),
					(sel ? Color.Lerp(YoyoGuidebookBookSkin.Ink, accent, 0.25f)
						: YoyoGuidebookBookSkin.Ink) * alpha, 0.7f);

				int display = IndexOfSource(i);
				string pages = display >= 0 ? (display + 1).ToString() : "";
				float pw = YoyoGuidebookBookSkin.TrackWidth(pages, 0.48f, 1f);
				YoyoGuidebookBookSkin.DrawTracked(sb, pages,
					new Vector2(row.Right - pw - 5, row.Y + 6),
					YoyoGuidebookBookSkin.InkDim * (0.85f * alpha), 0.48f, 1f);
				FillRect(sb, new Rectangle(row.X + 30, row.Bottom - 1, row.Width - 36, 1),
					YoyoGuidebookBookSkin.GoldDim * (0.22f * alpha));
				chapterRects.Add((row, BookId, i));
				rowTop += rowH;
			}

			// Scrollbar on the fore-edge margin: a thin brass rail with a thumb,
			// so a long chapter list has a visible position instead of only a hint.
			if (chapterMaxScroll > 0) {
				int trackX = x + sideW - 12;
				int trackY = y + 62;
				int trackH = Math.Max(24, maxRows * rowH - 10);
				FillRect(sb, new Rectangle(trackX, trackY, 3, trackH),
					new Color(120, 100, 70, 40) * alpha);
				int visible = Math.Min(maxRows, authored.Count);
				int thumbH = Math.Max(20, trackH * visible / Math.Max(1, authored.Count));
				float frac = chapterScroll / (float)Math.Max(1, chapterMaxScroll);
				int thumbY = trackY + (int)((trackH - thumbH) * frac);
				FillRect(sb, new Rectangle(trackX, thumbY, 3, thumbH),
					YoyoGuidebookBookSkin.Gold * (0.58f * alpha));
				FillRect(sb, new Rectangle(trackX, thumbY, 1, thumbH),
					YoyoGuidebookBookSkin.Gold * (0.86f * alpha));
			}

			DrawInk(sb, chapterMaxScroll > 0
					? "Wheel: scroll  -  Esc: index"
					: "Click a chapter  -  Esc: index",
				new Vector2(leafX, footerTop - 24),
				YoyoGuidebookBookSkin.InkDim * (0.62f * alpha), 0.56f);
		}

		private static void DrawContentsLeaf(SpriteBatch sb, Player player, int x, int y, int sideW, int ph,
			float alpha, Color accent)
		{
			// Tabs slide in with a short paper settle instead of snapping. The
			// hit regions move with the art for those few frames.
			float la = EaseOutCubic(leafAnim);
			alpha *= MathHelper.Clamp(la, 0f, 1f);
			x += (int)((1f - la) * 8f);

			DrawLeftTabs(sb, player, x, y, sideW, alpha, accent);

			if (BetterBook && leftTab == LeftTab.Chapters) {
				DrawChapterIndex(sb, player, x, y, sideW, ph, alpha, accent);
				return;
			}
			if (BetterBook && leftTab == LeftTab.Search) {
				DrawSearchPage(sb, player, x, y, sideW, ph, alpha, accent);
				return;
			}
			if (BetterBook && leftTab == LeftTab.Marks) {
				DrawMarksPage(sb, player, x, y, sideW, ph, alpha, accent);
				return;
			}

			DrawVolumeContents(sb, player, x, y, sideW, ph, alpha, accent);
		}

		private static void DrawLeftTabs(SpriteBatch sb, Player player, int x, int y, int sideW,
			float alpha, Color accent)
		{
			// Four full words do not fit a half-width leaf on a narrow book.
			// Step down to short caps, then to single letters, instead of
			// letting adjacent labels overlap each other.
			bool tightTabs = sideW < 220;
			bool compactTabs = sideW < 300;
			string[] labels = tightTabs
				? new[] { "I", "C", "S", "M" }
				: compactTabs
					? new[] { "IDX", "CHAP", "FIND", "MARK" }
					: new[] { "INDEX", "CHAPTERS", "SEARCH", "MARKS" };
			int leafX = x + 24;
			int leafW = sideW - 54;
			int gap = 4;
			int tw = (leafW - gap * 3) / 4;
			Point mouse = UiMouse.ToPoint();
			for (int i = 0; i < 4; i++) {
				Rectangle tab = new(leafX + i * (tw + gap), y + 18, tw, 25);
				leftTabRects[i] = tab;
				bool active = (int)leftTab == i;
				bool hover = tab.Contains(mouse);
				if (active) {
					FillRect(sb, tab, new Color(246, 239, 219, 235) * alpha);
					FillRect(sb, new Rectangle(tab.X, tab.Y, tab.Width, 2), accent * (0.9f * alpha));
					FillRect(sb, new Rectangle(tab.X, tab.Bottom - 1, tab.Width, 1),
						YoyoGuidebookBookSkin.Gold * (0.75f * alpha));
				}
				else {
					FillRect(sb, tab, new Color(214, 203, 177, hover ? 150 : 92) * alpha);
					FillRect(sb, new Rectangle(tab.X, tab.Bottom - 1, tab.Width, 1),
						YoyoGuidebookBookSkin.GoldDim * ((hover ? 0.7f : 0.4f) * alpha));
				}
				if (hover)
					player.mouseInterface = true;
				float scale = compactTabs ? 0.42f : 0.47f;
				float tracking = compactTabs ? 1.0f : 1.3f;
				while (scale > 0.30f
					&& YoyoGuidebookBookSkin.TrackWidth(labels[i], scale, tracking) > tab.Width - 4f)
					scale -= 0.02f;
				float labelW = YoyoGuidebookBookSkin.TrackWidth(labels[i], scale, tracking);
				YoyoGuidebookBookSkin.DrawTracked(sb, labels[i],
					new Vector2(tab.X + (tab.Width - labelW) * 0.5f, tab.Y + 7),
					(active ? YoyoGuidebookBookSkin.Ink : YoyoGuidebookBookSkin.InkDim) * alpha,
					scale, tracking);
				if (i == 3) {
					int count = Bookmarks()?.Bookmarks.Count ?? 0;
					if (count > 0) {
						string badge = count > 9 ? "9+" : count.ToString();
						DrawInk(sb, badge, new Vector2(tab.Right - 9, tab.Y + 1),
							YoyoGuidebookBookSkin.Ribbon * (0.9f * alpha), 0.4f);
					}
				}
			}
			// Sliding brass rail under the active tab. leftTabGlide trails the
			// integer tab, so switching sections feels like the marker is being
			// pulled along the page instead of teleporting.
			float railX = leafX + leftTabGlide * (tw + gap);
			Rectangle rail = new((int)railX, y + 45, tw, 2);
			UiArt.DrawGlow(sb, new Vector2(railX + tw * 0.5f, y + 46f), tw * 0.52f,
				accent * (0.045f * alpha));
			FillRect(sb, new Rectangle(rail.X + 1, rail.Y + 1, rail.Width, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.55f * alpha));
			FillRect(sb, rail, Color.Lerp(YoyoGuidebookBookSkin.Gold, accent, 0.58f) * alpha);
			FillRect(sb, new Rectangle(rail.X + rail.Width / 2 - 5, rail.Y, 10, 1),
				new Color(250, 239, 210) * (0.75f * alpha));
			FillRect(sb, new Rectangle(x + 42, y + 49, sideW - 84, 1),
				YoyoGuidebookBookSkin.GoldDim * alpha);
		}

		private static void DrawVolumeContents(SpriteBatch sb, Player player, int x, int y, int sideW, int ph,
			float alpha, Color accent)
		{
			int leafX = x + 24;
			int leafW = sideW - 54;
			var owned = OwnedVolumes(player);
			int rowY = y + 60;
			int footerTop = y + ph - 98;
			int maxVolumeRows = Math.Max(1, (footerTop - rowY) / 40);
			int shownVolumes = 0;

			YoyoGuidebookBookSkin.DrawTracked(sb, "VOLUMES", new Vector2(leafX, rowY - 2),
				YoyoGuidebookBookSkin.InkDim * (0.85f * alpha), 0.46f, 2f);
			rowY += 18;

			foreach (string vol in owned) {
				if (shownVolumes >= maxVolumeRows)
					break;
				Rectangle row = new(leafX, rowY, leafW, 36);
				volumeTabs.Add((row, vol));
				bool sel = vol == BookId;
				bool hov = row.Contains(UiMouse.ToPoint());
				if (hov || sel)
					player.mouseInterface = true;
				int vtype = VolumeItemType(vol);
				string shortTitle = ShortVolumeTitle(vol);

				float titleScale = 0.78f;
				while (titleScale > 0.50f
					&& FontAssets.MouseText.Value.MeasureString(shortTitle).X * titleScale
						> leafW - 46f)
					titleScale -= 0.04f;
				float titleW = Math.Min(leafW - 46f,
					FontAssets.MouseText.Value.MeasureString(shortTitle).X * titleScale);
				YoyoGuidebookBookSkin.DrawContentsRow(sb, row, sel, hov, alpha, accent,
					row.X + 40 + Math.Max(0f, titleW) + 8f, row.Y + 23);
				if (vtype > 0 && vtype < TextureAssets.Item.Length) {
					try {
						Texture2D vtex = TextureAssets.Item[vtype].Value;
						float vs = Math.Min(1f, 22f / Math.Max(vtex.Width, vtex.Height));
						YoyoGuidebookBookSkin.DrawBookSlot(sb, new Rectangle(row.X + 3, row.Y + 4, 28, 28), hov, alpha);
						sb.Draw(vtex, new Vector2(row.X + 17, row.Y + 18), null, Color.White * alpha, 0f,
							vtex.Size() * 0.5f, vs, SpriteEffects.None, 0f);
					}
					catch { }
				}
				DrawInk(sb, FitText(shortTitle, leafW - 46f, titleScale),
					new Vector2(row.X + 40, row.Y + 8),
					(sel ? Color.Lerp(YoyoGuidebookBookSkin.Ink, accent, 0.28f) : YoyoGuidebookBookSkin.Ink) * alpha,
					titleScale);
				rowY += 40;
				shownVolumes++;
			}

			if (owned.Count > shownVolumes) {
				DrawInk(sb, $"+{owned.Count - shownVolumes} more volumes",
					new Vector2(leafX + 2, rowY + 2),
					YoyoGuidebookBookSkin.InkDim * (0.62f * alpha), 0.6f);
			}

			int bookmarkTop = Math.Max(rowY + 10, Math.Min(y + 338, footerTop - 166));
			int maxBookmarkRows = Math.Clamp((footerTop - bookmarkTop - 30) / 22, 0, 5);
			if (maxBookmarkRows > 0)
				DrawBookmarkList(sb, player, leafX, bookmarkTop, leafW, alpha, maxBookmarkRows);
		}

		private static void DrawSearchPage(SpriteBatch sb, Player player, int x, int y, int sideW, int ph,
			float alpha, Color accent)
		{
			int leafX = x + 24;
			int leafW = sideW - 54;
			int top = y + 60;

			searchField = new Rectangle(leafX, top, leafW, 27);
			DrawSearchField(sb, searchField, alpha);

			// The two-by-two scope chips need one-word labels once the leaf is
			// narrow; the full phrases would run into each other.
			bool compactChips = leafW < 220;
			int chipGap = 4;
			int chipW = (leafW - chipGap) / 2;
			searchAllRect = new Rectangle(leafX, top + 34, chipW, 20);
			searchThisRect = new Rectangle(leafX + chipW + chipGap, top + 34, chipW, 20);
			searchTextRect = new Rectangle(leafX, top + 58, chipW, 20);
			searchTitleRect = new Rectangle(leafX + chipW + chipGap, top + 58, chipW, 20);
			DrawFilterChip(sb, searchAllRect, compactChips ? "ALL" : "ALL VOLUMES",
				showAllVolumes, alpha, accent);
			DrawFilterChip(sb, searchThisRect, compactChips ? "THIS" : "THIS VOLUME",
				!showAllVolumes, alpha, accent);
			DrawFilterChip(sb, searchTextRect, compactChips ? "TEXT" : "TITLE + TEXT",
				!searchTitlesOnly, alpha, accent);
			DrawFilterChip(sb, searchTitleRect, compactChips ? "TITLE" : "TITLES ONLY",
				searchTitlesOnly, alpha, accent);

			EnsureSearch(BookWidth(), BookHeight());
			int resultsTop = top + 88;
			int maxRows = Math.Clamp((y + ph - 98 - resultsTop) / SearchRowHeight, 1, 9);
			DrawSearchResults(sb, leafX, resultsTop, leafW, alpha, maxRows);

			if (!searchAllRect.Contains(UiMouse.ToPoint())
				&& !searchThisRect.Contains(UiMouse.ToPoint())
				&& !searchTextRect.Contains(UiMouse.ToPoint())
				&& !searchTitleRect.Contains(UiMouse.ToPoint()))
				return;
			player.mouseInterface = true;
		}

		private static void DrawFilterChip(SpriteBatch sb, Rectangle r, string label, bool active,
			float alpha, Color accent)
		{
			bool hover = r.Contains(UiMouse.ToPoint());
			FillRect(sb, r, (active ? new Color(246, 239, 219, 225) : new Color(214, 203, 177, 92)) * alpha);
			FillRect(sb, new Rectangle(r.X, r.Y, r.Width, 1),
				(active ? accent : YoyoGuidebookBookSkin.GoldDim) * ((active ? 0.9f : hover ? 0.7f : 0.4f) * alpha));
			FillRect(sb, new Rectangle(r.X, r.Bottom - 1, r.Width, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.45f * alpha));
			string text = label;
			float scale = 0.44f;
			float track = 1.1f;
			while (scale > 0.30f
				&& YoyoGuidebookBookSkin.TrackWidth(text, scale, track) > r.Width - 6f)
				scale -= 0.02f;
			float tw = YoyoGuidebookBookSkin.TrackWidth(text, scale, track);
			YoyoGuidebookBookSkin.DrawTracked(sb, text,
				new Vector2(r.X + (r.Width - tw) * 0.5f, r.Y + 6),
				(active ? YoyoGuidebookBookSkin.Ink : YoyoGuidebookBookSkin.InkDim) * alpha, scale, track);
		}

		private static void DrawMarksPage(SpriteBatch sb, Player player, int x, int y, int sideW, int ph,
			float alpha, Color accent)
		{
			int leafX = x + 24;
			int leafW = sideW - 54;
			int top = y + 58;
			var entries = BookmarkEntries(player);
			int volumeMarks = 0;
			foreach (var entry in entries) {
				if (entry.book == BookId)
					volumeMarks++;
			}

			YoyoGuidebookBookSkin.DrawTracked(sb, "BOOKMARKS", new Vector2(leafX, top),
				YoyoGuidebookBookSkin.InkDim * alpha, 0.5f, 2f);
			string count = entries.Count == 0 ? "NONE"
				: entries.Count == 1 ? "1 MARK" : $"{entries.Count} MARKS";
			float cw = YoyoGuidebookBookSkin.TrackWidth(count, 0.46f, 1.3f);
			YoyoGuidebookBookSkin.DrawTracked(sb, count,
				new Vector2(leafX + leafW - cw, top + 1),
				YoyoGuidebookBookSkin.InkDim * (0.9f * alpha), 0.46f, 1.3f);

			int clearW = Math.Min(120, Math.Max(40, leafW));
			marksClearRect = new Rectangle(leafX + leafW - clearW, top + 22, clearW, 22);
			bool clearHover = marksClearRect.Contains(UiMouse.ToPoint());
			FillRect(sb, marksClearRect, new Color(214, 203, 177, clearHover ? 130 : 78) * alpha);
			FillRect(sb, new Rectangle(marksClearRect.X, marksClearRect.Y, marksClearRect.Width, 1),
				YoyoGuidebookBookSkin.Ribbon * ((clearHover ? 0.85f : 0.55f) * alpha));
			string clearText = volumeMarks > 0
				? (clearW < 100 ? "CLEAR" : "CLEAR THIS VOLUME")
				: (clearW < 120 ? "NONE" : "NO MARKS HERE");
			float cts = 0.42f;
			while (cts > 0.30f
				&& YoyoGuidebookBookSkin.TrackWidth(clearText, cts, 1.1f) > clearW - 8f)
				cts -= 0.02f;
			float ctw = YoyoGuidebookBookSkin.TrackWidth(clearText, cts, 1.1f);
			YoyoGuidebookBookSkin.DrawTracked(sb, clearText,
				new Vector2(marksClearRect.X + (marksClearRect.Width - ctw) * 0.5f, marksClearRect.Y + 7),
				(volumeMarks > 0 ? YoyoGuidebookBookSkin.Ink : YoyoGuidebookBookSkin.InkDim) * alpha,
				cts, 1.1f);
			if (clearHover)
				player.mouseInterface = true;

			if (entries.Count == 0) {
				DrawInk(sb, "Press B on any page to tie a ribbon here.",
					new Vector2(leafX, top + 62), YoyoGuidebookBookSkin.InkDim * (0.85f * alpha), 0.7f);
				DrawInk(sb, "Marks remember the authored page, so they survive a resize.",
					new Vector2(leafX, top + 84), YoyoGuidebookBookSkin.InkDim * (0.62f * alpha), 0.58f);
				return;
			}

			int rowTop = top + 56;
			int rowH = 34;
			int maxRows = Math.Max(1, (y + ph - 98 - rowTop - 20) / rowH);
			marksMaxScroll = Math.Max(0, entries.Count - maxRows);
			marksScroll = Math.Clamp(marksScroll, 0, marksMaxScroll);
			int shown = 0;
			for (int index = marksScroll; index < entries.Count && shown < maxRows; index++) {
				var (book, source, title, volume) = entries[index];
				Rectangle row = new(leafX, rowTop, leafW, rowH - 3);
				bool hov = row.Contains(UiMouse.ToPoint());
				bool current = book == BookId;
				if (hov)
					FillRect(sb, row, new Color(255, 255, 255, 34) * alpha);
				if (current)
					FillRect(sb, new Rectangle(row.X, row.Y, 2, row.Height), accent * (0.8f * alpha));
				FillRect(sb, new Rectangle(row.X + 7, row.Y + 5, 4, row.Height - 10),
					YoyoGuidebookBookSkin.Ribbon * alpha);
				bool roomyMark = leafW >= 190;
				string t = FitText(title, leafW - (roomyMark ? 128 : 54), 0.68f);
				DrawInk(sb, t, new Vector2(row.X + 18, row.Y + 3),
					YoyoGuidebookBookSkin.Ink * alpha, 0.68f);
				if (roomyMark) {
					string vol = ShortVolumeTitle(book).ToUpperInvariant();
					YoyoGuidebookBookSkin.DrawTracked(sb, vol,
						new Vector2(row.X + 18, row.Y + 19),
						YoyoGuidebookBookSkin.InkDim * (0.82f * alpha), 0.42f, 1f);
				}

				Rectangle remove = new(row.Right - 22, row.Y + 6, 18, 18);
				bookmarkRemoveRects.Add((remove, book, source));
				bool removeHover = remove.Contains(UiMouse.ToPoint());
				if (removeHover) {
					FillRect(sb, remove, new Color(202, 122, 92, 44) * alpha);
					player.mouseInterface = true;
				}
				DrawInk(sb, "x", new Vector2(remove.X + 5, remove.Y + 1),
					(removeHover ? new Color(140, 46, 38) : YoyoGuidebookBookSkin.InkDim) * alpha, 0.62f);

				FillRect(sb, new Rectangle(row.X + 18, row.Bottom, row.Width - 24, 1),
					YoyoGuidebookBookSkin.GoldDim * (0.22f * alpha));
				bookmarkRowRects.Add((row, book, source));
				rowTop += rowH;
				shown++;
			}
			if (marksMaxScroll > 0) {
				int trackX = x + sideW - 12;
				int trackY = top + 56;
				int trackH = Math.Max(24, maxRows * rowH - 8);
				FillRect(sb, new Rectangle(trackX, trackY, 3, trackH),
					new Color(120, 100, 70, 40) * alpha);
				int thumbH = Math.Max(18, trackH * maxRows / Math.Max(1, entries.Count));
				float frac = marksScroll / (float)Math.Max(1, marksMaxScroll);
				int thumbY = trackY + (int)((trackH - thumbH) * frac);
				FillRect(sb, new Rectangle(trackX, thumbY, 3, thumbH),
					YoyoGuidebookBookSkin.Gold * (0.58f * alpha));
				FillRect(sb, new Rectangle(trackX, thumbY, 1, thumbH),
					YoyoGuidebookBookSkin.Gold * (0.86f * alpha));
				DrawInk(sb, "Wheel: scroll marks",
					new Vector2(leafX, y + ph - 120),
					YoyoGuidebookBookSkin.InkDim * (0.62f * alpha), 0.56f);
			}
			else if (entries.Count > shown) {
				DrawInk(sb, $"+{entries.Count - shown} more marks",
					new Vector2(leafX, rowTop + 2),
					YoyoGuidebookBookSkin.InkDim * (0.65f * alpha), 0.58f);
			}
		}

		private static void DrawSearchField(SpriteBatch sb, Rectangle f, float alpha)
		{
			bool hover = f.Contains(UiMouse.ToPoint());
			FillRect(sb, f, new Color(232, 224, 200, 175) * alpha);
			Color edge = searchActive ? YoyoGuidebookBookSkin.Gold
				: hover ? Color.Lerp(YoyoGuidebookBookSkin.GoldDim, YoyoGuidebookBookSkin.Gold, 0.55f)
				: YoyoGuidebookBookSkin.GoldDim;
			FillRect(sb, new Rectangle(f.X, f.Y, f.Width, 1), edge * alpha);
			FillRect(sb, new Rectangle(f.X, f.Bottom - 1, f.Width, 1), edge * alpha);
			FillRect(sb, new Rectangle(f.X, f.Y, 1, f.Height), edge * alpha);
			FillRect(sb, new Rectangle(f.Right - 1, f.Y, 1, f.Height), edge * alpha);
			if (searchActive) {
				FillRect(sb, new Rectangle(f.X + 1, f.Bottom - 2, f.Width - 2, 1),
					YoyoGuidebookBookSkin.Gold * (0.45f * alpha));
				// A soft inner bloom, so the focused field reads as lit paper.
				for (int i = 0; i < 6; i++) {
					FillRect(sb, new Rectangle(f.X + 2 + i, f.Y + 2 + i, f.Width - 4 - i * 2, 1),
						YoyoGuidebookBookSkin.Gold * (0.05f * (1f - i / 6f) * alpha));
				}
			}

			// Magnifier glyph, then the text and caret.
			YoyoGuidebookBookSkin.DrawSearchGlyph(sb, new Vector2(f.X + 11f, f.Y + 13f), 4f,
				(searchActive ? YoyoGuidebookBookSkin.Gold : YoyoGuidebookBookSkin.InkDim) * alpha);
			int textX = f.X + 22;
			int textRight = f.Right - (searchActive && searchText.Length > 0 ? 24 : 7);

			bool empty = searchText.Length == 0;
			string shown = empty ? "Search the guidebook..." : FitText(searchText, textRight - textX, 0.7f);
			Color ink = empty ? YoyoGuidebookBookSkin.InkDim * (0.72f * alpha)
				: YoyoGuidebookBookSkin.Ink * alpha;
			DrawInk(sb, shown, new Vector2(textX, f.Y + 5), ink, 0.7f);

			if (searchActive && Main.GameUpdateCount / 20 % 2 == 0) {
				float tw = FontAssets.MouseText.Value.MeasureString(shown).X * 0.7f;
				FillRect(sb, new Rectangle((int)Math.Min(textX + tw + 1, textRight), f.Y + 5, 1, 16),
					YoyoGuidebookBookSkin.Ink * (0.8f * alpha));
			}

			// Clear button: only while there is something to clear.
			if (searchActive && searchText.Length > 0) {
				Rectangle clear = new(f.Right - 21, f.Y + 5, 16, 16);
				searchClearRect = clear;
				bool clearHover = clear.Contains(UiMouse.ToPoint());
				if (clearHover) {
					FillRect(sb, clear, new Color(222, 182, 102, 46) * alpha);
					Main.LocalPlayer.mouseInterface = true;
				}
				DrawInk(sb, "x", new Vector2(clear.X + 4, clear.Y + 1),
					(clearHover ? YoyoGuidebookBookSkin.Gold : YoyoGuidebookBookSkin.InkDim) * alpha, 0.66f);
			}

			if (hover || searchActive)
				Main.LocalPlayer.mouseInterface = true;
		}

		private static void DrawSearchResults(SpriteBatch sb, int x, int y, int width, float alpha, int maxRows)
		{
			if (searchText.Trim().Length == 0) {
				YoyoGuidebookBookSkin.DrawTracked(sb, "SEARCH", new Vector2(x, y),
					YoyoGuidebookBookSkin.InkDim * alpha, 0.5f, 1.5f);
				DrawInk(sb, "Type to search titles and page text.",
					new Vector2(x, y + 26), YoyoGuidebookBookSkin.InkDim * (0.85f * alpha), 0.72f);
				return;
			}

			string head = searchHits.Count == 0 ? "NO RESULTS"
				: searchHits.Count == 1 ? "1 RESULT" : $"{searchHits.Count} RESULTS";
			YoyoGuidebookBookSkin.DrawTracked(sb, head, new Vector2(x, y),
				YoyoGuidebookBookSkin.InkDim * alpha, 0.5f, 1.5f);

			if (searchHits.Count == 0) {
				DrawInk(sb, "No pages match that search.",
					new Vector2(x, y + 26), YoyoGuidebookBookSkin.InkDim * (0.85f * alpha), 0.72f);
				return;
			}

			int rowY = y + 26;
			int shown = 0;
			foreach (SearchHit hit in searchHits) {
				if (shown >= maxRows)
					break;
				Rectangle row = new(x, rowY, width, SearchRowHeight - 2);
				bool hov = row.Contains(UiMouse.ToPoint());
				if (hov) {
					FillRect(sb, row, new Color(255, 255, 255, 36) * alpha);
					FillRect(sb, new Rectangle(row.X, row.Y, 2, row.Height),
						Color.Lerp(YoyoGuidebookBookSkin.Gold, YoyoGuidebookBookSkin.GoldDim, 0.2f) * alpha);
					Main.LocalPlayer.mouseInterface = true;
				}

				bool roomyHit = width >= 180;
				DrawHighlightedInk(sb, hit.Title, new Vector2(row.X + 8, row.Y + 3),
					Math.Max(24, width - (roomyHit ? 108 : 16)),
					YoyoGuidebookBookSkin.Ink, 0.72f, alpha, searchText);
				if (roomyHit) {
					string vol = YoyoGuidebookData.VolumeTitle(hit.Book).ToUpperInvariant();
					const float vScale = 0.5f;
					const float vTrack = 1.2f;
					float vw = YoyoGuidebookBookSkin.TrackWidth(vol, vScale, vTrack);
					YoyoGuidebookBookSkin.DrawTracked(sb, vol,
						new Vector2(row.Right - vw - 4, row.Y + 7),
						YoyoGuidebookBookSkin.InkDim * (0.9f * alpha), vScale, vTrack);
				}

				if (BetterBook && !string.IsNullOrWhiteSpace(hit.Snippet)) {
					DrawHighlightedInk(sb, hit.Snippet, new Vector2(row.X + 8, row.Y + 20),
						width - 20, YoyoGuidebookBookSkin.InkDim * 0.78f, 0.58f, alpha, searchText);
				}

				FillRect(sb, new Rectangle(row.X + 4, row.Bottom - 1, row.Width - 8, 1),
					YoyoGuidebookBookSkin.GoldDim * (0.28f * alpha));

				searchRects.Add((row, hit));
				rowY += SearchRowHeight;
				shown++;
			}

			int remaining = searchHits.Count - shown;
			DrawInk(sb,
				remaining > 0 ? $"+{remaining} more - refine the search" : "Enter: open the first result",
				new Vector2(x, rowY + 6), YoyoGuidebookBookSkin.InkDim * (0.7f * alpha), 0.6f);
		}

		private static void DrawBookmarkList(SpriteBatch sb, Player player, int x, int y, int width, float alpha,
			int maxRows)
		{
			FillRect(sb, new Rectangle(x + 16, y, width - 32, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.65f * alpha));
			var entries = BookmarkEntries(player);
			const string Head = "BOOKMARKS";
			YoyoGuidebookBookSkin.DrawTracked(sb, Head, new Vector2(x, y + 7),
				YoyoGuidebookBookSkin.InkDim * alpha, 0.5f, 2f);
			if (entries.Count > 0) {
				string count = entries.Count.ToString();
				float cw = YoyoGuidebookBookSkin.TrackWidth(count, 0.5f, 1.2f);
				YoyoGuidebookBookSkin.DrawTracked(sb, count,
					new Vector2(x + width - cw - 2, y + 7),
					YoyoGuidebookBookSkin.InkDim * (0.9f * alpha), 0.5f, 1.2f);
			}

			if (entries.Count == 0) {
				DrawInk(sb, "Press B on a page to mark it.",
					new Vector2(x + 2, y + 27), YoyoGuidebookBookSkin.InkDim * (0.7f * alpha), 0.62f);
				return;
			}

			int rowY = y + 24;
			int shown = 0;
			foreach (var (book, source, title, volume) in entries) {
				if (shown >= maxRows)
					break;
				Rectangle row = new(x, rowY, width, 20);
				bool hov = row.Contains(UiMouse.ToPoint());
				if (hov) {
					FillRect(sb, row, new Color(255, 255, 255, 34) * alpha);
					player.mouseInterface = true;
				}
				FillRect(sb, new Rectangle(row.X + 1, row.Y + 3, 4, 14),
					YoyoGuidebookBookSkin.Ribbon * alpha);
				bool roomyMarkRow = width >= 190;
				string t = FitText(title, width - (roomyMarkRow ? 108 : 32), 0.66f);
				DrawInk(sb, t, new Vector2(row.X + 10, row.Y + 2),
					YoyoGuidebookBookSkin.Ink * alpha, 0.66f);
				if (roomyMarkRow) {
					const float vScale = 0.48f;
					const float vTrack = 1.1f;
					string vol = volume.ToUpperInvariant();
					float vw = YoyoGuidebookBookSkin.TrackWidth(vol, vScale, vTrack);
					YoyoGuidebookBookSkin.DrawTracked(sb, vol,
						new Vector2(row.Right - vw - 2, row.Y + 4),
						YoyoGuidebookBookSkin.InkDim * (0.85f * alpha), vScale, vTrack);
				}

				bookmarkRowRects.Add((row, book, source));
				rowY += 22;
				shown++;
			}
			if (entries.Count > shown)
				DrawInk(sb, $"+{entries.Count - shown} more",
					new Vector2(x + 2, rowY + 2), YoyoGuidebookBookSkin.InkDim * (0.65f * alpha), 0.58f);
		}

		private static List<(string book, int source, string title, string volume)> BookmarkEntries(Player player)
		{
			var list = new List<(string, int, string, string)>();
			var pl = Bookmarks();
			if (pl == null)
				return list;

			foreach (string entry in pl.Bookmarks) {
				int bar = entry.LastIndexOf('|');
				if (bar <= 0)
					continue;
				string book = entry.Substring(0, bar);
				if (!int.TryParse(entry.Substring(bar + 1), out int source))
					continue;
				if (!YoyoGuidebookData.Books.TryGetValue(book, out var pages))
					continue;
				if (source < 0 || source >= pages.Count)
					continue;
				list.Add((book, source, pages[source].Title, YoyoGuidebookData.VolumeTitle(book)));
			}

			list.Sort((a, b) => {
				int oa = VolumeOrder(a.Item1);
				int ob = VolumeOrder(b.Item1);
				if (oa != ob)
					return oa.CompareTo(ob);
				return a.Item2.CompareTo(b.Item2);
			});
			return list;
		}

		private static int VolumeOrder(string book)
		{
			for (int i = 0; i < Volumes.Length; i++) {
				if (Volumes[i].id == book)
					return i;
			}
			return 99;
		}

		// -------------------------------------------------------------- reading page

		private static void DrawReadingPage(SpriteBatch sb, int pageIndex, int textX, int textW,
			int y, int ph, float alpha, Color accent, bool leftPage)
		{
			// An empty verso at the end of an odd page count is a blank leaf.
			if (pageIndex < 0 || pageIndex >= layout.Count) {
				DrawBlankLeaf(sb, textX, y, textW, ph, alpha);
				return;
			}

			GuidebookDisplayPage page = layout[pageIndex];

			if (page.Divider) {
				DrawSectionBreak(sb, page, pageIndex, textX, textW, y, ph, alpha);
				return;
			}

			// A real book repeats the clean title and marks "continued" in the
			// running head; "Title (cont.)" in 42px serif looks like a draft.
			string pageTitle = page.Title;
			if (page.Continued && pageTitle.EndsWith(" (cont.)", StringComparison.Ordinal))
				pageTitle = pageTitle.Substring(0, pageTitle.Length - 8);

			string head = YoyoGuidebookData.VolumeTitle(BookId).ToUpperInvariant();
			if (page.Continued)
				head += "  -  CONTINUED";
			const float headScale = 0.56f;
			const float headTrack = 2.2f;
			float headW = YoyoGuidebookBookSkin.TrackWidth(head, headScale, headTrack);
			YoyoGuidebookBookSkin.DrawTracked(sb, head,
				new Vector2(textX + (textW - headW) / 2f, y + 22),
				YoyoGuidebookBookSkin.InkDim * alpha, headScale, headTrack);
			FillRect(sb, new Rectangle(textX + 42, y + 42, textW - 84, 1),
				YoyoGuidebookBookSkin.GoldDim * alpha);
			DrawDiamondMark(sb, textX + 38, y + 42, 2, accent * (0.8f * alpha));
			DrawDiamondMark(sb, textX + textW - 38, y + 42, 2, accent * (0.8f * alpha));

			YoyoGuidebookBookSkin.DrawSerifCentered(sb, pageTitle,
				new Vector2(textX + textW / 2f, y + 68),
				textW - 28, 42f, YoyoGuidebookBookSkin.Ink * alpha);

			// A tiny fleuron closes the title block without stealing space from
			// the first line of prose.
			FillRect(sb, new Rectangle(textX + textW / 2 - 23, y + 91, 17, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.7f * alpha));
			FillRect(sb, new Rectangle(textX + textW / 2 + 6, y + 91, 17, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.7f * alpha));
			DrawDiamondMark(sb, textX + textW / 2, y + 91, 2,
				YoyoGuidebookBookSkin.Gold * (0.75f * alpha));

			int textTop = y + 96;
			int textBottom = y + ph - (page.Icons.Length > 0 ? 126 : 64);

			// Editorial margin: a hairline rail with sparse tick marks. It gives
			// the page a typeset edge and makes long prose easier to track.
			FillRect(sb, new Rectangle(textX - 13, textTop + 2, 1, Math.Max(1, textBottom - textTop - 4)),
				YoyoGuidebookBookSkin.GoldDim * (0.28f * alpha));
			for (int i = 0; i < 28; i++) {
				int ty = textTop + 14 + i * 22;
				if (ty > textBottom - 8)
					break;
				FillRect(sb, new Rectangle(textX - 16, ty, i % 4 == 0 ? 5 : 3, 1),
					YoyoGuidebookBookSkin.GoldDim * ((i % 4 == 0 ? 0.38f : 0.2f) * alpha));
			}
			if (highlightQuery.Length > 0)
				DrawBodyHighlights(sb, page.Lines, textX, textTop, textBottom, textW,
					BookBodyScale, BookBodyLine, alpha, highlightQuery);
			DrawLinkedColumn(sb, page.Lines, textX, textTop, textBottom,
				BookBodyScale, BookBodyLine, alpha, textW, page.Title);

			if (page.Icons != null && page.Icons.Length > 0) {
				var valid = new List<int>();
				foreach (int t in page.Icons) {
					if (t > 0 && t < TextureAssets.Item.Length)
						valid.Add(t);
					if (valid.Count >= 10)
						break;
				}
				float iconY = y + ph - 112;
				float iconX = textX + (textW - valid.Count * 40) / 2f;
				if (valid.Count > 0) {
					int ruleW = Math.Min(textW - 48, valid.Count * 40 + 28);
					int ruleX = (int)(textX + (textW - ruleW) * 0.5f);
					FillRect(sb, new Rectangle(ruleX, (int)iconY - 9, ruleW, 1),
						YoyoGuidebookBookSkin.GoldDim * (0.55f * alpha));
					DrawDiamondMark(sb, ruleX + ruleW / 2, (int)iconY - 9, 2,
						accent * (0.7f * alpha));
					DrawInk(sb, "PLATE", new Vector2(ruleX, iconY - 25),
						YoyoGuidebookBookSkin.InkDim * (0.62f * alpha), 0.44f);
				}
				foreach (int itemType in valid) {
					Rectangle frame = new((int)iconX, (int)iconY, 34, 34);
					bool hovSlot = frame.Contains(UiMouse.ToPoint());
					YoyoGuidebookBookSkin.DrawBookSlot(sb, frame, hovSlot, alpha);
					try {
						Texture2D tex = TextureAssets.Item[itemType].Value;
						float s = Math.Min(1f, 26f / Math.Max(tex.Width, tex.Height));
						sb.Draw(tex, new Vector2(iconX + 17, iconY + 17), null, Color.White * alpha, 0f,
							tex.Size() * 0.5f, s, SpriteEffects.None, 0f);
						iconRects.Add((frame, itemType));
					}
					catch { }
					iconX += 40;
					if (iconX > textX + textW - 40)
						break;
				}
			}

			// Progress stamp: an honest check of the world flags for pages that
			// actually mention one of the mod's bosses.
			var progress = ProgressFor(page);
			if (progress.HasValue) {
				Rectangle stamp = new(textX + 32, y + ph - 51, 106, 19);
				YoyoGuidebookBookSkin.DrawStamp(sb, stamp,
					progress.Value.Downed ? "DEFEATED" : "UNBEATEN", progress.Value.Downed, alpha);
				stampRects.Add((stamp, progress.Value.Name, progress.Value.Downed));
			}

			string folio = (pageIndex + 1).ToString();
			const float folioScale = 0.64f;
			const float folioTrack = 1.4f;
			float folioW = YoyoGuidebookBookSkin.TrackWidth(folio, folioScale, folioTrack);
			float folioX = textX + (textW - folioW) / 2f;
			YoyoGuidebookBookSkin.DrawTracked(sb, folio,
				new Vector2(folioX, y + ph - 44),
				YoyoGuidebookBookSkin.InkDim * alpha, folioScale, folioTrack);
			FillRect(sb, new Rectangle((int)folioX - 28, y + ph - 40, 18, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.5f * alpha));
			FillRect(sb, new Rectangle((int)(folioX + folioW) + 10, y + ph - 40, 18, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.5f * alpha));
		}

		private static void DrawSectionBreak(SpriteBatch sb, GuidebookDisplayPage page, int pageIndex,
			int textX, int textW, int y, int ph, float alpha)
		{
			int cx = textX + textW / 2;

			// A quiet part-title leaf: overline, large clean title, rule and the
			// short "what follows" note. This is the spacer page readers see before
			// the item-by-item appendix, so the deeper notes have a clear door.
			string overline = (page.Overline ?? "").ToUpperInvariant();
			if (overline.Length > 0) {
				const float os = 0.58f;
				const float ot = 3.2f;
				float ow = YoyoGuidebookBookSkin.TrackWidth(overline, os, ot);
				YoyoGuidebookBookSkin.DrawTracked(sb, overline,
					new Vector2(cx - ow / 2f, y + 132),
					YoyoGuidebookBookSkin.GoldDim * alpha, os, ot);
			}

			YoyoGuidebookBookSkin.DrawSerifCentered(sb, page.Title,
				new Vector2(cx, y + 184), textW - 24, 44f,
				YoyoGuidebookBookSkin.Ink * alpha);

			int ruleY = y + 222;
			FillRect(sb, new Rectangle(cx - 92, ruleY, 76, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.85f * alpha));
			FillRect(sb, new Rectangle(cx + 16, ruleY, 76, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.85f * alpha));
			for (int d = -3; d <= 3; d++)
				FillRect(sb, new Rectangle(cx + d, ruleY - 3 + Math.Abs(d), 1, 1),
					YoyoGuidebookBookSkin.Gold * (0.9f * alpha));

			float cy = y + 258;
			foreach (string line in page.Lines) {
				if (string.IsNullOrEmpty(line)) {
					cy += 14f;
					continue;
				}
				DrawInkCentered(sb, line, cx, cy,
					YoyoGuidebookBookSkin.InkDim * (0.92f * alpha), 0.82f);
				cy += 25f;
			}

			// Tailpiece, then the same folio every other leaf carries.
			int tailY = y + ph - 112;
			FillRect(sb, new Rectangle(cx - 46, tailY, 34, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.6f * alpha));
			FillRect(sb, new Rectangle(cx + 12, tailY, 34, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.6f * alpha));
			for (int d = -2; d <= 2; d++)
				FillRect(sb, new Rectangle(cx + d, tailY - 2 + Math.Abs(d), 1, 1),
					YoyoGuidebookBookSkin.Gold * (0.7f * alpha));

			string folio = (pageIndex + 1).ToString();
			const float folioScale = 0.64f;
			const float folioTrack = 1.4f;
			float folioW = YoyoGuidebookBookSkin.TrackWidth(folio, folioScale, folioTrack);
			YoyoGuidebookBookSkin.DrawTracked(sb, folio,
				new Vector2(cx - folioW / 2f, y + ph - 44),
				YoyoGuidebookBookSkin.InkDim * alpha, folioScale, folioTrack);
		}

		private static void DrawBlankLeaf(SpriteBatch sb, int textX, int y, int textW, int ph, float alpha)
		{
			// A small fleuron on an otherwise empty leaf: the book's own endpaper.
			int cx = textX + textW / 2;
			int cy = y + ph / 2;
			FillRect(sb, new Rectangle(cx - 42, cy, 36, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.7f * alpha));
			FillRect(sb, new Rectangle(cx + 6, cy, 36, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.7f * alpha));
			YoyoGuidebookBookSkin.DrawSerifCentered(sb, "~", new Vector2(cx, cy - 2),
				30, 20, YoyoGuidebookBookSkin.GoldDim * alpha);
		}

		private static (string Name, bool Downed)? ProgressFor(GuidebookDisplayPage page)
		{
			// One stamp per page: the boss the page mentions first is its subject,
			// so "Cowboys & Bearings" stamps the Cowboys rather than the Bearing
			// just because it happens to be checked first.
			string text = page.Title + "\n" + string.Join("\n", page.Lines);
			int best = int.MaxValue;
			string name = null;
			bool downed = false;

			void Consider(string needle, string label, bool done) {
				int at = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
				if (at >= 0 && at < best) {
					best = at;
					name = label;
					downed = done;
				}
			}

			Consider("Yoyo Guardian", "Yoyo Guardian", Content.YoyoGuardianWorld.downedYoyoGuardian);
			Consider("Trial of the Guardian", "Yoyo Guardian", Content.YoyoGuardianWorld.downedYoyoGuardian);
			Consider("Giant Bearing", "Giant Bearing", Content.YoyoGuardianWorld.downedGiantBearing);
			Consider("Cowboys", "Cowboys", Content.YoyoGuardianWorld.downedCowboyOfYoyos);
			Consider("Ancient Duelist", "Ancient Duelist", Content.YoyoGuardianWorld.downedAncientDuelist);

			return name == null ? null : (name, downed);
		}

		private static void DrawBookmarkRibbons(SpriteBatch sb, int x, int y, int sideW, int ph,
			float alpha, int rightPage)
		{
			var pl = Bookmarks();
			if (pl == null)
				return;

			int gutter = x + sideW;

			if (twoPage && Page >= 0 && Page < layout.Count) {
				int src = layout[Page].SourceIndex;
				if (pl.HasBookmark(BookId, src)) {
					Rectangle r = new(gutter - 12, y + 14, 7, 44);
					bool hov = r.Contains(UiMouse.ToPoint());
					YoyoGuidebookBookSkin.DrawBookmarkRibbon(sb, r, alpha, hov);
					bookmarkRibbonRects.Add((r, Page, BookId, src));
					if (hov)
						Main.LocalPlayer.mouseInterface = true;
				}
			}

			if (rightPage >= 0 && rightPage < layout.Count) {
				int src = layout[rightPage].SourceIndex;
				if (pl.HasBookmark(BookId, src)) {
					Rectangle r = new(gutter + 5, y + 14, 7, 44);
					bool hov = r.Contains(UiMouse.ToPoint());
					YoyoGuidebookBookSkin.DrawBookmarkRibbon(sb, r, alpha, hov);
					bookmarkRibbonRects.Add((r, rightPage, BookId, src));
					if (hov)
						Main.LocalPlayer.mouseInterface = true;
				}
			}
		}

		private static void DrawBookFooter(SpriteBatch sb, Player player, int x, int y, int pw, int ph,
			int sideW, float alpha, Color accent)
		{
			int textW = ReadingWidth(pw);
			int pageInset = PageInset(pw);
			int leftX = x + pageInset;
			int rightX = x + sideW + pageInset;

			prevBtn = new Rectangle((twoPage ? leftX : rightX) - 26, y + ph - 50, 26, 22);
			nextBtn = new Rectangle(rightX + textW, y + ph - 50, 26, 22);
			bool nextDisabled = Page + (twoPage ? 1 : 0) >= layout.Count - 1;
			YoyoGuidebookBookSkin.DrawPageArrow(sb, prevBtn, false, false, alpha);
			YoyoGuidebookBookSkin.DrawPageArrow(sb, nextBtn, true, nextDisabled, alpha);

			closeBtn = new Rectangle(x + pw - 42, y + 14, 26, 26);
			Button(sb, closeBtn, "X", false, alpha);

			if (BetterBook && history.Count > 0) {
				backBtn = new Rectangle(x + 12, y + 14, 54, 26);
				bool backHover = backBtn.Contains(UiMouse.ToPoint());
				Button(sb, backBtn, "BACK", false, alpha);
				if (backHover)
					player.mouseInterface = true;
			}

			// Reading scrubber: a brass rail with a grabbable cursor. It replaces
			// the old passive progress line, so jumping through a long volume
			// does not require a stack of page turns.
			if (BetterBook && layout.Count > 0) {
				int barX = leftX;
				int barW = Math.Max(20, rightX + textW - leftX);
				int barY = y + ph - 25;
				scrubberTrack = new Rectangle(barX, barY - 4, barW, 9);
				FillRect(sb, new Rectangle(barX, barY, barW, 1),
					YoyoGuidebookBookSkin.GoldDim * (0.35f * alpha));
				float progress = MathHelper.Clamp((Page + 1f) / layout.Count, 0f, 1f);
				FillRect(sb, new Rectangle(barX, barY, (int)(barW * progress), 1),
					YoyoGuidebookBookSkin.Gold * (0.8f * alpha));
				FillRect(sb, new Rectangle(barX, barY - 1, (int)(barW * progress), 1),
					new Color(255, 238, 196, 45) * alpha);
				int tipX = barX + (int)(barW * progress);
				bool scrubbing = draggingScrubber || scrubberTrack.Contains(UiMouse.ToPoint());
				scrubberThumb = new Rectangle(tipX - 5, barY - 6, 11, 13);
				UiArt.DrawGlow(sb, new Vector2(tipX, barY), scrubbing ? 18f : 11f,
					YoyoGuidebookBookSkin.Gold * ((scrubbing ? 0.36f : 0.16f) * alpha));
				for (int d = -3; d <= 3; d++)
					FillRect(sb, new Rectangle(tipX + d, barY - 3 + Math.Abs(d), 1, 1),
						YoyoGuidebookBookSkin.Gold * ((scrubbing ? 1f : 0.82f) * alpha));
				FillRect(sb, new Rectangle(tipX, barY - 2, 1, 5),
					YoyoGuidebookBookSkin.Ink * (0.55f * alpha));
				string pageLabel = $"PAGE {Page + 1} / {layout.Count}";
				const float plScale = 0.44f;
				float plW = YoyoGuidebookBookSkin.TrackWidth(pageLabel, plScale, 1f);
				YoyoGuidebookBookSkin.DrawTracked(sb, pageLabel,
					new Vector2(barX + 2, barY - 16),
					YoyoGuidebookBookSkin.InkDim * (0.8f * alpha), plScale, 1f);
			}

			// Keyboard-help card lives in the cover chrome, not in the reading
			// footer, so it is available from both the cover and a spread.
			shortcutBtn = new Rectangle(x + pw - 72, y + 14, 26, 26);
			Button(sb, shortcutBtn, "?", false, alpha);

			// Contents leaf footer: volume, length and reading hints.
			if (!twoPage) {
				int footRuleY = y + ph - 98;
				FillRect(sb, new Rectangle(x + 42, footRuleY, sideW - 84, 1),
					YoyoGuidebookBookSkin.GoldDim * (0.7f * alpha));
				string volLine = $"VOL. {YoyoGuidebookBookSkin.Roman(BookIndex)}  -  {layout.Count} PAGES";
				const float volScale = 0.54f;
				const float volTrack = 1.4f;
				float volW = YoyoGuidebookBookSkin.TrackWidth(volLine, volScale, volTrack);
				YoyoGuidebookBookSkin.DrawTracked(sb, volLine,
					new Vector2(x + (sideW - volW) / 2f, footRuleY + 8),
					YoyoGuidebookBookSkin.InkDim * alpha, volScale, volTrack);

				DrawInk(sb, FitText("Arrows / wheel / page click: turn  -  drag rail: jump",
						sideW - 84, 0.54f),
					new Vector2(x + 42, y + ph - 56), YoyoGuidebookBookSkin.InkDim * alpha, 0.54f);
				string spreadHint = CanTwoPage ? "   T spread" : "";
				string hints = BetterBook
					? "TAB / C chapters   F search   M marks   B bookmark"
						+ spreadHint + "   ? keys"
					: "B: bookmark" + (CanTwoPage ? "   T: spread" : "") + "   Esc: close";
				DrawInk(sb, FitText(hints, sideW - 84, 0.52f),
					new Vector2(x + 42, y + ph - 38), YoyoGuidebookBookSkin.InkDim * alpha, 0.52f);
			}

			// Index / spread tab on the fore-edge. Below the spread threshold
			// there is no spread to toggle, so the tab disappears entirely
			// instead of offering a control that cannot work.
			if (CanTwoPage) {
				bool rightTab = !twoPage;
				int tabX = rightTab ? x + pw + 1 : x - 23;
				tabX = Math.Clamp(tabX, 2, Math.Max(2, UiWidth - 24));
				int tabH = Math.Min(108, Math.Max(64, ph - 190));
				int tabY = y + Math.Clamp(ph / 3, 54, 170);
				if (tabY + tabH > y + ph - 52)
					tabY = y + ph - 52 - tabH;
				tabY = Math.Max(y + 48, tabY);
				edgeTab = new Rectangle(tabX, tabY, 22, tabH);
				edgeTabOnRight = rightTab;
				bool tabHover = edgeTab.Contains(UiMouse.ToPoint());
				YoyoGuidebookBookSkin.DrawEdgeTab(sb, edgeTab, rightTab ? "SPREAD" : "INDEX",
					rightTab, alpha, tabHover);
				if (tabHover)
					player.mouseInterface = true;
			}
			else {
				edgeTab = Rectangle.Empty;
			}

			// Tooltips: link first, then item, then stamp.
			Point mouse = UiMouse.ToPoint();
			if (closeBtn.Contains(mouse)) {
				DrawChip(sb, "Close (Esc)", alpha);
				player.mouseInterface = true;
				return;
			}
			if (shortcutBtn.Contains(mouse)) {
				DrawChip(sb, "Keyboard shortcuts (?)", alpha);
				player.mouseInterface = true;
				return;
			}
			if (!twoPage) {
				for (int i = 0; i < leftTabRects.Length; i++) {
					if (leftTabRects[i].Width > 0 && leftTabRects[i].Contains(mouse)) {
						string[] names = { "Contents", "Chapters", "Search", "Marks" };
						DrawChip(sb, names[i] + " (click / Tab)", alpha);
						player.mouseInterface = true;
						return;
					}
				}
			}
			if (searchActive && (searchAllRect.Contains(mouse) || searchThisRect.Contains(mouse))) {
				DrawChip(sb, "Search scope: all owned volumes or this volume", alpha);
				player.mouseInterface = true;
				return;
			}
			if (searchActive && (searchTextRect.Contains(mouse) || searchTitleRect.Contains(mouse))) {
				DrawChip(sb, "Search titles only, or title + page text", alpha);
				player.mouseInterface = true;
				return;
			}
			if (marksOpen && marksClearRect.Contains(mouse)) {
				DrawChip(sb, "Remove every bookmark in this volume", alpha);
				player.mouseInterface = true;
				return;
			}
			if (scrubberTrack.Contains(mouse) || scrubberThumb.Contains(mouse)) {
				DrawChip(sb, "Drag to jump through this volume", alpha);
				player.mouseInterface = true;
				return;
			}
			if (!twoPage && searchField.Contains(mouse)) {
				DrawChip(sb, searchActive ? "Type to search - Esc: back" : "Search (F)", alpha);
				player.mouseInterface = true;
				return;
			}
			if (BetterBook && backBtn.Contains(mouse)) {
				DrawChip(sb, "Back (Backspace)", alpha);
				player.mouseInterface = true;
				return;
			}
			foreach (var link in linkRects) {
				if (!link.rect.Contains(mouse))
					continue;
				DrawChip(sb, "See: " + link.label, alpha);
				player.mouseInterface = true;
				return;
			}
			foreach (var (frame, type) in iconRects) {
				if (!frame.Contains(mouse))
					continue;
				string name;
				try { name = Lang.GetItemNameValue(type); }
				catch { name = "???"; }
				DrawChip(sb, name, alpha);
				player.mouseInterface = true;
				return;
			}
			foreach (var (rect, label, done) in stampRects) {
				if (!rect.Contains(mouse))
					continue;
				DrawChip(sb, label + (done ? " - defeated" : " - not yet defeated"), alpha);
				player.mouseInterface = true;
				return;
			}
			foreach (var ribbon in bookmarkRibbonRects) {
				if (!ribbon.rect.Contains(mouse))
					continue;
				DrawChip(sb, "Remove ribbon", alpha);
				player.mouseInterface = true;
				return;
			}
		}

		// ------------------------------------------------------------------- links

		private static void DrawBodyHighlights(SpriteBatch sb, List<string> lines, float x, float top,
			float bottom, float width, float scale, float lineHeight, float alpha, string query)
		{
			if (string.IsNullOrEmpty(query))
				return;
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float cy = top;
			foreach (string line in lines) {
				if (string.IsNullOrEmpty(line)) {
					cy += lineHeight * 0.7f;
					continue;
				}
				if (cy + lineHeight > bottom + 0.5f)
					break;
				int cursor = 0;
				while (cursor < line.Length) {
					int at = line.IndexOf(query, cursor, StringComparison.OrdinalIgnoreCase);
					if (at < 0)
						break;
					float before = font.MeasureString(line.Substring(0, at)).X * scale;
					float matchW = font.MeasureString(line.Substring(at, query.Length)).X * scale;
					if (before + matchW <= width)
						FillRect(sb, new Rectangle((int)(x + before), (int)(cy + 1),
							(int)matchW, Math.Max(8, (int)(18f * scale))),
							new Color(222, 182, 102, 62) * alpha);
					cursor = at + Math.Max(1, query.Length);
				}
				cy += lineHeight;
			}
		}

		private static void DrawLinkedColumn(SpriteBatch sb, List<string> lines, float x, float top, float bottom,
			float scale, float lineHeight, float alpha, float width, string currentTitle)
		{
			var owned = OwnedVolumes(Main.LocalPlayer);
			var ownedSet = new HashSet<string>(owned);
			Color ink = YoyoGuidebookBookSkin.Ink * alpha;

			float cy = top;
			for (int i = 0; i < lines.Count; i++) {
				string line = lines[i];
				if (string.IsNullOrEmpty(line)) {
					cy += lineHeight * 0.7f;
					continue;
				}
				if (cy + lineHeight > bottom + 0.5f)
					break;

				DrawLinkedLine(sb, line, new Vector2(x, cy), ink, scale, alpha,
					currentTitle, ownedSet);
				cy += lineHeight;
			}
		}

		private static void DrawLinkedLine(SpriteBatch sb, string line, Vector2 pos, Color ink,
			float scale, float alpha, string currentTitle, HashSet<string> owned)
		{
			// Continuation pages carry a "(cont.)" suffix; strip it before the
			// self-link test so a long entry does not link to its own first page.
			string baseTitle = currentTitle.EndsWith(" (cont.)", StringComparison.Ordinal)
				? currentTitle.Substring(0, currentTitle.Length - 8)
				: currentTitle;
			var matches = FindLinkMatches(line, baseTitle, owned);
			if (matches.Count == 0) {
				DrawInk(sb, line, pos, ink, scale);
				return;
			}

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float x = pos.X;
			int cursor = 0;
			int drawn = 0;
			Point mouse = UiMouse.ToPoint();

			foreach (var (index, length, target) in matches) {
				if (drawn >= 3)
					break;
				if (index < cursor)
					continue;

				if (index > cursor) {
					string before = line.Substring(cursor, index - cursor);
					DrawInk(sb, before, new Vector2(x, pos.Y), ink, scale);
					x += font.MeasureString(before).X * scale;
				}

				string phrase = line.Substring(index, length);
				float w = font.MeasureString(phrase).X * scale;
				Rectangle hit = new((int)x, (int)pos.Y, (int)w, (int)(Math.Max(18f, scale * 26f)));
				bool hover = hit.Contains(mouse);

				if (hover)
					FillRect(sb, new Rectangle((int)x, (int)pos.Y + 2, (int)w, (int)(scale * 22f)),
						new Color(222, 182, 102, 38) * alpha);

				Color linkInk = hover
					? new Color(158, 62, 42) * alpha
					: new Color(122, 50, 40) * alpha;
				DrawInk(sb, phrase, new Vector2(x, pos.Y), linkInk, scale);

				float uy = pos.Y + lineHeightFor(scale) * 0.84f;
				for (float u = 0; u < w; u += 3f) {
					FillRect(sb, new Rectangle((int)(x + u), (int)uy, 1, 1),
						(hover ? YoyoGuidebookBookSkin.Gold : YoyoGuidebookBookSkin.GoldDim) * (0.8f * alpha));
				}

				linkRects.Add((hit, target.Book, target.Title, target.Title));
				x += w;
				cursor = index + length;
				drawn++;
			}

			if (cursor < line.Length) {
				string rest = line.Substring(cursor);
				DrawInk(sb, rest, new Vector2(x, pos.Y), ink, scale);
			}
		}

		/// <summary>Plain ink on paper: no UI text border, so prose reads as print.</summary>
		private static void DrawInk(SpriteBatch sb, string text, Vector2 pos, Color color, float scale)
		{
			if (string.IsNullOrEmpty(text))
				return;
			sb.DrawString(FontAssets.MouseText.Value, text, pos, color, 0f, Vector2.Zero,
				scale, SpriteEffects.None, 0f);
		}

		private static void DrawHighlightedInk(SpriteBatch sb, string text, Vector2 pos, float maxWidth,
			Color ink, float scale, float alpha, string query)
		{
			if (string.IsNullOrEmpty(text))
				return;
			string shown = FitText(text, maxWidth, scale);
			query = (query ?? "").Trim();
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			if (query.Length > 0 && shown.Length > 0) {
				int cursor = 0;
				while (cursor < shown.Length) {
					int at = shown.IndexOf(query, cursor, StringComparison.OrdinalIgnoreCase);
					if (at < 0)
						break;
					float before = font.MeasureString(shown.Substring(0, at)).X * scale;
					float match = font.MeasureString(shown.Substring(at, query.Length)).X * scale;
					if (before + match > maxWidth)
						break;
					FillRect(sb, new Rectangle((int)(pos.X + before), (int)(pos.Y + 1),
						(int)match, Math.Max(8, (int)(18f * scale))),
						new Color(222, 182, 102, 68) * alpha);
					cursor = at + Math.Max(1, query.Length);
				}
			}
			DrawInk(sb, shown, pos, ink * alpha, scale);
			if (query.Length == 0 || shown.Length == 0)
				return;
			int underlineCursor = 0;
			while (underlineCursor < shown.Length) {
				int at = shown.IndexOf(query, underlineCursor, StringComparison.OrdinalIgnoreCase);
				if (at < 0)
					break;
				float before = font.MeasureString(shown.Substring(0, at)).X * scale;
				float match = font.MeasureString(shown.Substring(at, query.Length)).X * scale;
				if (before + match > maxWidth)
					break;
				FillRect(sb, new Rectangle((int)(pos.X + before), (int)(pos.Y + 17f * scale),
					(int)match, 1), YoyoGuidebookBookSkin.Gold * (0.8f * alpha));
				underlineCursor = at + Math.Max(1, query.Length);
			}
		}

		private static void DrawInkCentered(SpriteBatch sb, string text, float cx, float y,
			Color color, float scale)
		{
			if (string.IsNullOrEmpty(text))
				return;
			float w = FontAssets.MouseText.Value.MeasureString(text).X * scale;
			DrawInk(sb, text, new Vector2(cx - w / 2f, y), color, scale);
		}

		private static float lineHeightFor(float scale) => 22f * (scale / BookBodyScale);

		private static List<(int index, int length, (string Phrase, string Book, string Title) target)> FindLinkMatches(
			string line, string currentTitle, HashSet<string> owned)
		{
			var found = new List<(int, int, (string, string, string))>();
			int i = 0;
			while (i < line.Length) {
				int bestLen = 0;
				(string Phrase, string Book, string Title) best = default;
				foreach (var link in CrossLinks) {
					int len = link.Phrase.Length;
					if (len <= bestLen || i + len > line.Length)
						continue;
					if (string.Compare(line, i, link.Phrase, 0, len, true) != 0)
						continue;
					bool leftOk = i == 0 || !char.IsLetterOrDigit(line[i - 1]);
					int end = i + len;
					bool rightOk = end >= line.Length || !char.IsLetterOrDigit(line[end]);
					if (!leftOk || !rightOk)
						continue;
					if (!owned.Contains(link.Book))
						continue;
					if (link.Book == BookId && link.Title == currentTitle)
						continue;
					best = link;
					bestLen = len;
				}

				if (bestLen > 0) {
					found.Add((i, bestLen, best));
					i += bestLen;
				}
				else {
					i++;
				}
			}
			return found;
		}

		// ------------------------------------------------------------------ search

		private static void EnsureSearch(int pw, int ph)
		{
			string key = searchText + "|" + BookId + "|" + pw + "|" + ph + "|" + showAllVolumes + "|" + searchTitlesOnly;
			if (key == searchCacheKey)
				return;
			searchCacheKey = key;
			searchHits.Clear();

			string q = searchText.Trim();
			if (q.Length == 0)
				return;

			var owned = OwnedVolumes(Main.LocalPlayer);
			if (!showAllVolumes)
				owned = new List<string> { BookId };
			var seen = new HashSet<string>();
			foreach (string id in owned) {
				var pages = BuildDisplayLayout(id, pw, ph);
				var authored = YoyoGuidebookData.PagesOf(id);
				for (int i = 0; i < pages.Count; i++) {
					GuidebookDisplayPage p = pages[i];
					// One result per authored entry: a long page that wraps onto
					// continuations should not flood the list with near-duplicates.
					string entryKey = id + "|" + p.SourceIndex;
					if (seen.Contains(entryKey))
						continue;

					bool titleHit = p.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
					bool bodyHit = false;
					if (!titleHit && !searchTitlesOnly) {
						foreach (string line in p.Lines) {
							if (line.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) {
								bodyHit = true;
								break;
							}
						}
					}
					if (!titleHit && !bodyHit)
						continue;
					seen.Add(entryKey);

					string authoredTitle = p.Title;
					if (p.SourceIndex >= 0 && p.SourceIndex < authored.Count)
						authoredTitle = authored[p.SourceIndex].Title;

					// One line of context makes the results list scannable without
					// opening each page: the matching line, or the opening line for
					// a title hit.
					string snippet = "";
					if (!searchTitlesOnly) {
						foreach (string line in p.Lines) {
							if (line.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) {
								snippet = line.Trim();
								break;
							}
						}
					}
					if (snippet.Length == 0 && p.Lines.Count > 0)
						snippet = p.Lines[0].Trim();

					searchHits.Add(new SearchHit {
						Book = id,
						SourceIndex = p.SourceIndex,
						DisplayIndex = i,
						Title = authoredTitle,
						Snippet = snippet,
						Rank = titleHit ? 0 : 1,
					});
				}
			}

			searchHits.Sort((a, b) => {
				int r = a.Rank.CompareTo(b.Rank);
				if (r != 0)
					return r;
				int o = VolumeOrder(a.Book).CompareTo(VolumeOrder(b.Book));
				if (o != 0)
					return o;
				return a.DisplayIndex.CompareTo(b.DisplayIndex);
			});


		}

		// ------------------------------------------------------------------ misc UI

		private static string FitText(string text, float width, float scale)
		{
			if (string.IsNullOrEmpty(text))
				return "";
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			if (font.MeasureString(text).X * scale <= width)
				return text;

			string cut = text;
			while (cut.Length > 1 && font.MeasureString(cut + "...").X * scale > width)
				cut = cut.Substring(0, cut.Length - 1);
			return cut + "...";
		}

		private static void DrawChip(SpriteBatch sb, string name, float alpha)
		{
			if (string.IsNullOrEmpty(name))
				return;

			const float scale = 0.75f;
			Vector2 size = FontAssets.MouseText.Value.MeasureString(name) * scale;
			Vector2 pos = UiMouse + new Vector2(16, 10);
			if (pos.X + size.X + 16 > UiWidth)
				pos.X = UiMouse.X - size.X - 22;
			if (pos.Y + size.Y + 16 > UiHeight)
				pos.Y = UiMouse.Y - size.Y - 20;

			Rectangle box = new((int)pos.X - 7, (int)pos.Y - 4, (int)size.X + 14, (int)size.Y + 8);
			Color fill = new Color(238, 230, 208, 245);
			Color edge = YoyoGuidebookBookSkin.GoldDim;
			FillRect(sb, new Rectangle(box.X + 2, box.Y + 2, box.Width, box.Height),
				new Color(0, 0, 0, 100) * alpha);
			FillRect(sb, box, fill * alpha);
			FillRect(sb, new Rectangle(box.X, box.Y, box.Width, 1), edge * alpha);
			FillRect(sb, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), edge * alpha);
			FillRect(sb, new Rectangle(box.X, box.Y, 1, box.Height), edge * alpha);
			FillRect(sb, new Rectangle(box.Right - 1, box.Y, 1, box.Height), edge * alpha);
			sb.DrawString(FontAssets.MouseText.Value, name, pos,
				YoyoGuidebookBookSkin.Ink * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private static void DrawShortcutCard(SpriteBatch sb, Rectangle book, float alpha, Color accent)
		{
			int w = Math.Min(306, Math.Max(220, UiWidth - 16));
			int h = Math.Min(226, Math.Max(170, UiHeight - 16));
			int x = Math.Clamp(book.Right - w - 20, 8, Math.Max(8, UiWidth - w - 8));
			int y = Math.Clamp(book.Y + 64, 8, Math.Max(8, UiHeight - h - 8));
			shortcutCardRect = new Rectangle(x, y, w, h);

			FillRect(sb, new Rectangle(x + 6, y + 7, w, h), new Color(0, 0, 0, 120) * alpha);
			FillRect(sb, shortcutCardRect, new Color(241, 234, 214, 248) * alpha);
			FillRect(sb, new Rectangle(x, y, w, 1), YoyoGuidebookBookSkin.Gold * alpha);
			FillRect(sb, new Rectangle(x, y + h - 1, w, 1), YoyoGuidebookBookSkin.GoldDim * alpha);
			FillRect(sb, new Rectangle(x, y, 1, h), YoyoGuidebookBookSkin.GoldDim * alpha);
			FillRect(sb, new Rectangle(x + w - 1, y, 1, h), YoyoGuidebookBookSkin.GoldDim * alpha);
			FillRect(sb, new Rectangle(x, y + 2, 4, h - 4), accent * (0.7f * alpha));
			FillRect(sb, new Rectangle(x + 16, y + 40, w - 32, 1),
				YoyoGuidebookBookSkin.GoldDim * (0.6f * alpha));

			YoyoGuidebookBookSkin.DrawTracked(sb, "READER'S CARD",
				new Vector2(x + 18, y + 14), YoyoGuidebookBookSkin.InkDim * alpha, 0.55f, 2.4f);
			DrawInk(sb, "Controls", new Vector2(x + w - 72, y + 14),
				YoyoGuidebookBookSkin.InkDim * (0.7f * alpha), 0.52f);

			var rows = new List<(string key, string label)> {
				("ARROWS", "Turn pages"),
				("WHEEL", "Page / scroll chapter list"),
				("TAB", "Cycle index, chapters, search, marks"),
				("F / C / M", "Search / Chapters / Marks"),
				("B", "Bookmark this page"),
			};
			if (CanTwoPage)
				rows.Add(("T", "Two-page spread"));
			rows.Add(("? / ESC", "This card / step back"));
			float rowGap = Math.Min(22f, (h - 72f) / Math.Max(1, rows.Count));
			float rowY = y + 50;
			foreach (var (key, label) in rows) {
				DrawInk(sb, key, new Vector2(x + 18, rowY),
					YoyoGuidebookBookSkin.GoldDim * alpha, 0.54f);
				DrawInk(sb, FitText(label, w - 112, 0.56f),
					new Vector2(x + 96, rowY),
					YoyoGuidebookBookSkin.Ink * alpha, 0.56f);
				rowY += rowGap;
			}

			DrawInk(sb, "Click outside to dismiss", new Vector2(x + 18, y + h - 22),
				YoyoGuidebookBookSkin.InkDim * (0.62f * alpha), 0.5f);
		}

		private static void DrawClosedBookCover(SpriteBatch sb, Rectangle rect, float alpha, Color accent,
			bool prompt)
		{
			Texture2D sigil = null;
			int vtype = VolumeItemType(BookId);
			if (vtype > 0 && vtype < TextureAssets.Item.Length) {
				try { sigil = TextureAssets.Item[vtype].Value; }
				catch { sigil = null; }
			}
			YoyoGuidebookBookSkin.DrawClosedCover(sb, rect, alpha, accent,
				YoyoGuidebookData.VolumeTitle(BookId),
				$"A BiltonB field guide - {layout.Count} pages", sigil, prompt);
		}

		private static void FillRect(SpriteBatch sb, Rectangle rect, Color color)
		{
			sb.Draw(TextureAssets.MagicPixel.Value, rect, new Rectangle(0, 0, 1, 1), color);
		}

		private static void DrawDiamondMark(SpriteBatch sb, int cx, int cy, int radius, Color color)
		{
			for (int d = 0; d <= radius; d++)
				FillRect(sb, new Rectangle(cx - radius + d, cy - d, 1 + (radius - d) * 2, 1), color);
			for (int d = 1; d <= radius; d++)
				FillRect(sb, new Rectangle(cx - radius + d, cy + d, 1 + (radius - d) * 2, 1), color);
		}

		private static void Button(SpriteBatch sb, Rectangle b, string label, bool disabled, float alpha)
		{
			if (b.Width <= 0 || b.Height <= 0)
				return;
			bool hover = b.Contains(UiMouse.ToPoint()) && !disabled;
			YoyoGuidebookBookSkin.DrawButton(sb, b, label, disabled, alpha, hover);
		}
	}
}
