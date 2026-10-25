using System.Numerics;

namespace CatSurvivor;

// Menu của trận đánh (FIGHT / ACT / ITEM / MERCY), thanh Target của FIGHT, và chữ gõ từng ký tự (RPGText).
// Menu là một "ngăn xếp" các cột (mỗi cột nhớ mục đang chọn + hành động Quay lại khi bấm X); điều hướng bằng khoảng cách/hướng giữa các mục, y như bản gốc.
internal sealed partial class SansFight
{
	// Các lớp vẽ theo thứ tự (như các layer của Construct 2)
	private const int LayerBackground = 0, LayerEnemies = 1, LayerButtons = 2, LayerCombat = 3, LayerClipped = 4, LayerOverlay = 5;

	// ================================================================== chữ gõ từng ký tự
	private sealed class RpgText
	{
		public SansFont Font;
		public Vector2 Pos;
		public float Scale = 1f, Wrap;
		public string Full = "", Shown = "";
		public int Cur;
		public float T, Timeout;
		public string Voice = "", EndFunc = "", Name = "";
		public bool Interactive;
		public Rgba Tint = Rgba.White;
		public int Layer;
	}

	private readonly List<RpgText> _texts = new();

	private void UpdateTexts(float dt)
	{
		foreach (var t in _texts.ToArray())
		{
			if (!_texts.Contains(t)) continue;
			t.T += dt;
			if (t.Cur < t.Full.Length && t.T >= 1f / 30f)
			{
				// 30 ký tự/giây, mỗi ký tự kèm tiếng "bíp"
				t.T -= 1f / 30f;
				t.Cur++;
				t.Shown = t.Full.Substring(0, t.Cur);
				if (t.Voice.Length > 0) SoundService.PlayExtra(t.Voice, 1f, null, 0.8f);
			}
			if (t.Interactive)
			{
				if (Pad.Pressed(SansBtn.Confirm) && t.Cur == t.Full.Length) { DestroyText(t); continue; }   // Z: sang đoạn kế (khi đã hiện hết)
				if (Pad.Pressed(SansBtn.Cancel)) { t.Cur = t.Full.Length; t.Shown = t.Full; t.T = 0f; }      // X: hiện hết ngay
			}
			else if (t.Cur == t.Full.Length && t.Timeout > 0f)
			{
				t.Timeout -= MathF.Min(dt, t.Timeout);
				if (t.Timeout == 0f) DestroyText(t);
			}
		}
	}

	/// <summary>Xoá một đoạn chữ; nếu nó có EndFunc thì gọi hàm đó (như trigger "On destroyed" của bản gốc).</summary>
	private void DestroyText(RpgText t)
	{
		if (!_texts.Remove(t)) return;
		if (t.EndFunc.Length > 0) Call(t.EndFunc);
	}

	private void DestroyTexts(Func<RpgText, bool> match)
	{
		foreach (var t in _texts.Where(match).ToArray()) DestroyText(t);
	}

	private RpgText AddInfoText(string full, string endFunc = "", bool interactive = false)
	{
		var t = new RpgText
		{
			Font = SansFont.Default, Pos = new Vector2(48f, 272f), Scale = 2f, Full = full, Name = "InfoText", Layer = LayerCombat,
			Voice = "BattleText", Wrap = 544f, EndFunc = endFunc, Interactive = interactive,
		};
		_texts.Add(t);
		return t;
	}

	// ================================================================== ngăn xếp menu
	private sealed class MenuCol { public int Sel; public string Back = ""; }
	private sealed class MenuEntry { public int Id; public float X, Y; public string Text = "", Action = ""; }

	private readonly List<MenuCol> _stack = new() { new MenuCol() };
	private readonly List<MenuEntry> _menuItems = new();
	private int _menuState;     // 0 tắt, 1 đang co khung về menu, 2 menu chính, 3 menu con
	private readonly bool[] _btnHighlight = new bool[4];

	private static readonly string[] ButtonName = { "UIFight", "UIAct", "UIItem", "UIMercy" };
	private static readonly string[] ButtonAction = { "MenuFight", "MenuAct", "MenuItem", "MenuMercy" };
	private static readonly float[] ButtonXs = { 32f, 184f, 344f, 496f };
	private static float ButtonX(int i) => ButtonXs[Math.Clamp(i, 0, 3)];
	private const float ButtonY = 432f, ButtonW = 110f, ButtonH = 42f;

	private MenuCol Top => _stack[^1];

	private void BattleMenuEnable(bool on)
	{
		if (on)
		{
			_stack.Clear();
			_stack.Add(new MenuCol());
			_menuState = 1;
			ZoneResize(33f, 251f, 608f, 391f, "MenuBattle");
		}
		else _menuState = 0;
	}

	private void ClearMenuUi()
	{
		_menuItems.Clear();
		DestroyTexts(t => t.Font == SansFont.Default);
	}

	private void MenuBackAction(string f) => Top.Back = f;

	private void CreateMenuItem(float x, float y, int id, string text, string action)
	{
		_menuItems.Add(new MenuEntry { Id = id, X = x, Y = y, Text = text, Action = action });
		_texts.Add(new RpgText
		{
			Font = SansFont.Default, Pos = new Vector2(x + 32f, y), Scale = 2f, Shown = text, Name = "Menu" + id, Layer = LayerBackground,
			Tint = action.Length == 0 ? new Rgba(0.5f, 0.5f, 0.5f, 1f) : Rgba.White,
		});
	}

	// ---- các hành động (hàm) của menu ----
	private void MenuBattle()
	{
		ClearMenuUi();
		MenuBackAction("");
		_menuState = 2;
		AddInfoText(_infoText);
		for (int i = 0; i < 4; i++)
			_menuItems.Add(new MenuEntry { Id = i, X = ButtonX(i), Y = ButtonY, Action = ButtonAction[i] });
	}

	private void MenuEnemyList()
	{
		// Chỉ có một đối thủ: Sans
		string action = "";
		if (_stack[0].Sel == 0) action = "MenuFightEnemy";
		else if (_stack[0].Sel == 1) action = "MenuActEnemy";
		CreateMenuItem(64f, 272f, 0, "* Sans", action);
	}

	private void MenuFight()
	{
		ClearMenuUi();
		MenuBackAction("MenuBattle");
		_menuState = 3;
		MenuEnemyList();
	}

	private void MenuAct()
	{
		ClearMenuUi();
		MenuBackAction("MenuBattle");
		_menuState = 3;
		MenuEnemyList();
	}

	private void MenuActEnemy()
	{
		ClearMenuUi();
		MenuBackAction("MenuAct");
		_menuState = 3;
		CreateMenuItem(64f, 272f, 0, "* Check", "MenuCheckSans");
	}

	private void MenuCheckSans()
	{
		ClearMenuUi();
		_menuState = 0;
		AddInfoText("* SANS 1 ATK 1 DEF\n* The easiest enemy.\n* Can only deal 1 damage.", HitAttempts > 0 ? "MenuCheckSans2" : "StartAttack", true);
		_hVisible = false;
	}

	private void MenuCheckSans2() => AddInfoText("* Can't keep dodging forever.\n* Keep attacking.", "StartAttack", true);

	private void MenuMercy()
	{
		ClearMenuUi();
		MenuBackAction("MenuBattle");
		_menuState = 3;
		CreateMenuItem(64f, 272f, 0, "* Spare", "MenuSpare");
	}

	private void MenuSpare()
	{
		ClearMenuUi();
		_menuState = 0;
		_hVisible = false;
		StartAttack();
	}

	// ---- ITEM ----
	private readonly List<int> _items = new();
	private static readonly (int heal, string name, string shortName)[] ItemDb =
	{
		(99, "Butterscotch Pie", "Pie"),
		(90, "Instant Noodles", "I.Noodles"),
		(60, "Face Steak", "Steak"),
		(40, "Legendary Hero", "L. Hero"),
	};

	private void MenuItemList()
	{
		if (_items.Count == 0) { _stack.RemoveAt(_stack.Count - 1); return; }   // hết đồ: huỷ cú bấm
		ClearMenuUi();
		MenuBackAction("MenuBattle");
		_menuState = 3;
		_texts.Add(new RpgText { Font = SansFont.Default, Pos = new Vector2(384f, 336f), Scale = 2f, Shown = "PAGE 1", Name = "Page", Layer = LayerBackground });
		for (int i = 0; i < _items.Count && i < 8; i++)
		{
			// 4 món một trang (2 cột x 2 hàng); trang 2 nằm ngoài màn hình bên phải, trượt vào khi chọn tới
			float x = 64f + (i % 4 % 2) * 256f + (i >= 4 ? 640f : 0f);
			float y = 272f + (i % 4 / 2) * 32f;
			CreateMenuItem(x, y, i, "* " + ItemDb[_items[i]].shortName, "MenuUseItem");
		}
	}

	private void MenuUseItem()
	{
		ClearMenuUi();
		_hVisible = false;
		_menuState = 0;
		int slot = _stack[^2].Sel;
		if (slot < 0 || slot >= _items.Count) { StartAttack(); return; }
		var item = ItemDb[_items[slot]];
		Hp += item.heal;
		SoundService.PlayExtra("PlayerHeal");
		AddInfoText($"* You eat the {item.name}.\n* You recovered {item.heal} HP!", "StartAttack", true);
		_items.RemoveAt(slot);
	}

	// ================================================================== điều khiển menu
	private void UpdateMenu(float dt)
	{
		UpdateTarget(dt);
		if (_menuState >= 2) RunMenu();

		if (_menuState == 3 && _stack[0].Sel == 2)
		{
			// Danh sách đồ: chọn tới món nằm ở trang kia thì trượt cả hai trang cho món đó vào màn hình
			var cur = _menuItems.FirstOrDefault(m => m.Id == Top.Sel);
			if (cur != null)
			{
				if (cur.X > 640f) foreach (var m in _menuItems) m.X -= 640f;
				cur = _menuItems.FirstOrDefault(m => m.Id == Top.Sel);
				if (cur != null && cur.X < 0f) foreach (var m in _menuItems) m.X += 640f;
			}
			var page = _texts.FirstOrDefault(t => t.Name == "Page");
			if (page != null) page.Shown = "PAGE " + (Top.Sel / 4 + 1);
		}

		if (_menuState == 0 || _menuState == 2) Array.Clear(_btnHighlight);
		if (_menuState == 1 || _menuState == 2)
		{
			int s = Top.Sel;
			if (s >= 0 && s < 4)
			{
				_hx = ButtonX(s) + 0.145455f * ButtonW;     // điểm "Heart" của nút
				_hy = ButtonY + 0.5f * ButtonH;
				if (_menuState == 2) _btnHighlight[s] = true;
			}
		}
		if (_menuState == 3)
		{
			var cur = _menuItems.FirstOrDefault(m => m.Id == Top.Sel);
			if (cur != null) { _hx = cur.X + 8f; _hy = cur.Y + 12f; }
		}
	}

	private void RunMenu()
	{
		var cur = _menuItems.FirstOrDefault(m => m.Id == Top.Sel);
		if (Pad.Pressed(SansBtn.Confirm) && cur != null && cur.Action.Length > 0)
		{
			_stack.Add(new MenuCol());
			Call(cur.Action);
			SoundService.PlayExtra("MenuSelect");
		}
		else if (Pad.Pressed(SansBtn.Cancel) && Top.Back.Length > 0)
		{
			string f = Top.Back;
			_stack.RemoveAt(_stack.Count - 1);
			Call(f);
			SoundService.PlayExtra("MenuSelect");
		}
		else if (Pad.Pressed(SansBtn.Right)) MenuSelect(0);
		else if (Pad.Pressed(SansBtn.Down)) MenuSelect(1);
		else if (Pad.Pressed(SansBtn.Left)) MenuSelect(2);
		else if (Pad.Pressed(SansBtn.Up)) MenuSelect(3);

		foreach (var m in _menuItems)
		{
			var label = _texts.FirstOrDefault(t => t.Name == "Menu" + m.Id);
			if (label != null) label.Pos = new Vector2(m.X + 32f, m.Y);   // chữ đi theo mục
		}
	}

	private static float AngleDeg(float x1, float y1, float x2, float y2)
	{
		float a = MathF.Atan2(y2 - y1, x2 - x1) * 180f / MathF.PI;
		return a < 0f ? a + 360f : a;
	}

	private static bool WithinAngle(float a, float b, float within)
	{
		float d = ((a - b) % 360f + 540f) % 360f - 180f;
		return MathF.Abs(d) <= within;
	}

	/// <summary>Di chuyển lựa chọn theo hướng (0 phải, 1 xuống, 2 trái, 3 lên): mục gần nhất nằm đúng hướng đó; không có thì vòng sang mục xa nhất phía đối diện.</summary>
	private void MenuSelect(int dir)
	{
		var cur = _menuItems.FirstOrDefault(m => m.Id == Top.Sel);
		if (cur == null) return;
		int target = -1;
		float best = float.PositiveInfinity;
		foreach (var m in _menuItems)
		{
			if (m.Id == cur.Id) continue;
			if (!WithinAngle(AngleDeg(cur.X, cur.Y, m.X, m.Y), dir * 90f, 0.5f)) continue;
			float d = Vector2.Distance(new Vector2(cur.X, cur.Y), new Vector2(m.X, m.Y));
			if (d < best) { best = d; target = m.Id; }
		}
		if (target == -1)
		{
			best = float.NegativeInfinity;
			foreach (var m in _menuItems)
			{
				if (m.Id == cur.Id) continue;
				if (!WithinAngle(AngleDeg(cur.X, cur.Y, m.X, m.Y), dir * 90f - 180f, 0.5f)) continue;
				float d = Vector2.Distance(new Vector2(cur.X, cur.Y), new Vector2(m.X, m.Y));
				if (d > best) { best = d; target = m.Id; }
			}
		}
		if (target != -1)
		{
			Top.Sel = target;
			SoundService.PlayExtra("MenuCursor");
		}
	}

	// ================================================================== FIGHT: thanh Target
	private sealed class FightTarget { public int State; public float Width = 548f, Opacity = 100f; }
	private sealed class TargetChoice { public float X; public int Dir; public bool Animating; public float AnimT; }
	private sealed class Strike { public float T; public Vector2 Pos; }

	private FightTarget _target;
	private TargetChoice _choice;
	private Strike _strike;
	private readonly List<(float at, Action act)> _delayed = new();

	private void MenuFightEnemy()
	{
		ClearMenuUi();
		_menuState = 0;
		_hVisible = false;
		_target = new FightTarget();
		bool left = _rng.Next(2) == 0;      // thanh trượt chạy từ trái sang hoặc phải sang, ngẫu nhiên
		_choice = new TargetChoice { X = left ? ZL : ZR, Dir = left ? 0 : 2 };
	}

	private void AddMissText(float x, float y, float timeout) =>
		_texts.Add(new RpgText { Font = SansFont.Damage, Pos = new Vector2(x, y), Shown = "MISS", Timeout = timeout, Tint = new Rgba(0.75f, 0.75f, 0.75f, 1f), Layer = LayerEnemies, Name = "Damage" });

	private void UpdateTarget(float dt)
	{
		for (int i = _delayed.Count - 1; i >= 0; i--)
			if (_time >= _delayed[i].at) { var act = _delayed[i].act; _delayed.RemoveAt(i); act(); }

		if (_choice != null && _choice.Animating) _choice.AnimT += dt;

		if (_target != null && _target.State == 0 && _choice != null)
		{
			_choice.X += DirVec(_choice.Dir).X * dt * 360f;
			if (_choice.Dir == 0 && _choice.X > ZR || _choice.Dir == 2 && _choice.X < ZL)
			{
				// Để thanh trượt chạy qua mà không bấm: trượt đòn
				_choice = null;
				_target.State = 2;
				AddMissText(272f, 76f, 1f);
				StartAttack();
			}
			else if (Pad.Pressed(SansBtn.Confirm))
			{
				_target.State = 1;
				_choice.Animating = true;
				_choice.AnimT = 0f;
				_strike = new Strike { T = 0f, Pos = new Vector2(_sansX, _sansY - 96f) };
				SoundService.PlayExtra("PlayerFight");
				_dodgeState = 1;       // Sans né
			}
		}
		if (_target != null && _target.State == 2)
		{
			_target.Width -= dt * 960f;
			_target.Opacity -= dt * 240f;
			if (_target.Opacity <= 0f) _target = null;
		}
		if (_strike != null)
		{
			_strike.T += dt;
			if (_strike.T >= 0.6f) _strike = null;   // 6 khung x 0.1s
		}
	}
}
