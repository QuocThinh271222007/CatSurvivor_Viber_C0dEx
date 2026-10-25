using System.Numerics;

namespace CatSurvivor;

// Vẽ cả cảnh ở độ phân giải logic 640x480 (nền đen), theo thứ tự các lớp: Background -> Enemies -> Buttons -> CombatZone -> CombatZoneClipped -> Overlay.
internal sealed partial class SansFight
{
	private static readonly Rgba White = Rgba.White;
	private static readonly Rgba BlueBone = new(0.0784f, 0.6627f, 1f, 1f);
	private static readonly Rgba OrangeBone = new(1f, 0.6274f, 0.2509f, 1f);

	private static Rgba BoneTint(int color) => color == 1 ? BlueBone : color == 2 ? OrangeBone : White;

	public void Draw(Canvas c)
	{
		c.ScreenMode();
		c.FillRect(-64f, -64f, W + 128f, H + 128f, new Rgba(0f, 0f, 0f, 1f));
		var so = ShakeOffset;
		c.WorldMode(new Vector2(-so.X, -so.Y));       // rung cả màn hình

		DrawHud(c);
		DrawTexts(c, LayerBackground);
		DrawSans(c);
		DrawSpeech(c);
		DrawTexts(c, LayerEnemies);
		DrawStrike(c);
		DrawButtons(c);
		DrawCombatLayer(c);
		DrawClippedLayer(c);
		DrawOverlay(c);

		c.ScreenMode();
	}

	// ---------------------------------------------------------------- vẽ tiện ích
	private static void Sprite(Canvas c, Bitmap tex, float x, float y, float w, float h, Rgba? tint = null) =>
		c.DrawTexture(tex, new Rect2(MathF.Round(x), MathF.Round(y), MathF.Round(w), MathF.Round(h)), tint);

	private static void Sprite(Canvas c, Bitmap tex, float x, float y, Rgba? tint = null) => Sprite(c, tex, x, y, tex.Width, tex.Height, tint);

	/// <summary>Lát (tile) một vùng ảnh nguồn lặp lại kín vùng đích.</summary>
	private static void Tile(Canvas c, Bitmap tex, float sx, float sy, float sw, float sh, float dx, float dy, float dw, float dh, Rgba? tint)
	{
		if (sw <= 0f || sh <= 0f || dw <= 0f || dh <= 0f) return;
		for (float ty = 0f; ty < dh; ty += sh)
		{
			float ph = MathF.Min(sh, dh - ty);
			for (float tx = 0f; tx < dw; tx += sw)
			{
				float pw = MathF.Min(sw, dw - tx);
				c.DrawTexture(tex, new Rect2(dx + tx, dy + ty, pw, ph), new Rect2(sx, sy, pw, ph), tint);
			}
		}
	}

	/// <summary>"9-patch": bốn góc giữ nguyên, bốn cạnh và phần giữa được LÁT lặp (tile) hoặc KÉO GIÃN (stretch) theo kiểu của plugin gốc.</summary>
	private static void NinePatch(Canvas c, Bitmap tex, float x, float y, float w, float h, float ml, float mt, float mr, float mb, bool tile, bool center, Rgba? tint = null)
	{
		x = MathF.Round(x); y = MathF.Round(y); w = MathF.Round(w); h = MathF.Round(h);
		float tw = tex.Width, th = tex.Height;
		float cw = tw - ml - mr, ch = th - mt - mb;
		float dw = w - ml - mr, dh = h - mt - mb;
		void Part(float sx, float sy, float sw, float sh, float dx, float dy, float pw, float ph)
		{
			if (sw <= 0f || sh <= 0f || pw <= 0f || ph <= 0f) return;
			if (tile) Tile(c, tex, sx, sy, sw, sh, dx, dy, pw, ph, tint);
			else c.DrawTexture(tex, new Rect2(dx, dy, pw, ph), new Rect2(sx, sy, sw, sh), tint);
		}
		Part(0, 0, ml, mt, x, y, ml, mt);
		Part(tw - mr, 0, mr, mt, x + w - mr, y, mr, mt);
		Part(0, th - mb, ml, mb, x, y + h - mb, ml, mb);
		Part(tw - mr, th - mb, mr, mb, x + w - mr, y + h - mb, mr, mb);
		Part(ml, 0, cw, mt, x + ml, y, dw, mt);
		Part(ml, th - mb, cw, mb, x + ml, y + h - mb, dw, mb);
		Part(0, mt, ml, ch, x, y + mt, ml, dh);
		Part(tw - mr, mt, mr, ch, x + w - mr, y + mt, mr, dh);
		if (center) Part(ml, mt, cw, ch, x + ml, y + mt, dw, dh);
	}

	private void DrawTexts(Canvas c, int layer)
	{
		foreach (var t in _texts)
			if (t.Layer == layer && t.Shown.Length > 0)
				t.Font.Draw(c, t.Shown, new Vector2(MathF.Round(t.Pos.X), MathF.Round(t.Pos.Y)), t.Scale, t.Tint, t.Wrap);
	}

	// ---------------------------------------------------------------- lớp Background: tên, HP, KR
	private void DrawHud(Canvas c)
	{
		float bgW = MathF.Floor(MaxHp * 1.2f), barH = 21f, x0 = 256f, y0 = 400f;
		c.FillRect(x0, y0, bgW, barH, new Rgba(191f / 255f, 0f, 0f, 1f));
		float hpW = bgW * Hp / MaxHp;
		if (hpW > 0f) c.FillRect(x0, y0, hpW, barH, new Rgba(1f, 1f, 0f, 1f));
		float krW = MathF.Ceiling(bgW * Kr / MaxHp);
		if (krW > 0f) c.FillRect(x0 + bgW * (Hp - Kr) / MaxHp, y0, krW, barH, new Rgba(1f, 0f, 1f, 1f));

		var hp = SansAssets.Frame("HP", "Default", 0);
		var kr = SansAssets.Frame("KR", "Default", 0);
		Sprite(c, hp, 224f, 416f - hp.Height);
		Sprite(c, kr, 400f - kr.Width, 416f - kr.Height);

		var f = SansFont.Battle;
		f.Draw(c, "CHARA   LV 19", new Vector2(32f, 402f), 3f, White);
		string hpText = $"{Math.Max(0, (int)MathF.Ceiling(Hp)):00} / {(int)MaxHp}";
		f.Draw(c, hpText, new Vector2(416f, 400f), 3f, Kr > 0f ? new Rgba(1f, 0f, 1f, 1f) : White);
	}

	// ---------------------------------------------------------------- lớp Enemies: Sans
	private void DrawSans(Canvas c)
	{
		if (_legsVisible) { var p = LegsPart(); Sprite(c, p.Tex, p.Dest.X, p.Dest.Y, p.Dest.Width, p.Dest.Height); }
		if (_torsoVisible) { var p = TorsoPart(); Sprite(c, p.Tex, p.Dest.X, p.Dest.Y, p.Dest.Width, p.Dest.Height); }
		if (_bodyVisible) { var p = BodyPart(); Sprite(c, p.Tex, p.Dest.X, p.Dest.Y, p.Dest.Width, p.Dest.Height); }
		{ var p = HeadPart(); Sprite(c, p.Tex, p.Dest.X, p.Dest.Y, p.Dest.Width, p.Dest.Height); }
		if (_sweat > 0) { var p = SweatPart(); Sprite(c, p.Tex, p.Dest.X, p.Dest.Y, p.Dest.Width, p.Dest.Height); }
	}

	private void DrawSpeech(Canvas c)
	{
		var tex = SansAssets.Frame("SpeechBubble", "Default", 0);
		foreach (var b in _bubbles) Sprite(c, tex, b.X, b.Y);
	}

	private static readonly Vector2[] StrikeHot =
	{
		new(-1f, 5.66667f), new(-0.5f, 1.36364f), new(0f, 0.428571f), new(0f, 0.15625f), new(0f, -0.75f), new(-0.142857f, -4f),
	};

	private void DrawStrike(Canvas c)
	{
		if (_strike == null) return;
		int i = Math.Min(5, (int)(_strike.T * 10f));
		var tex = SansAssets.Frame("Strike", "Default", i);
		const float s = 1.5f;
		Sprite(c, tex, _strike.Pos.X - StrikeHot[i].X * tex.Width * s, _strike.Pos.Y - StrikeHot[i].Y * tex.Height * s, tex.Width * s, tex.Height * s);
	}

	// ---------------------------------------------------------------- lớp Buttons
	private void DrawButtons(Canvas c)
	{
		for (int i = 0; i < 4; i++)
			Sprite(c, SansAssets.Frame(ButtonName[i], _btnHighlight[i] ? "Highlight" : "Default", 0), ButtonX(i), ButtonY);
	}

	// ---------------------------------------------------------------- lớp CombatZone (không cắt)
	private void DrawCombatLayer(Canvas c)
	{
		if (_black) return;     // màn đen phủ lên: các lớp dưới không thấy

		// bục (nằm dưới trái tim)
		var p1 = SansAssets.Tex("Platform1");
		var p2 = SansAssets.Tex("Platform2");
		foreach (var p in _platforms)
		{
			NinePatch(c, p1, p.X, p.Y, p.W, p.H, 4f, 2f, 4f, 2f, false, true);
			NinePatch(c, p2, p.X, p.Y - 4f, p.W, p.H, 4f, 2f, 4f, 2f, false, true);   // viền xanh lục phía trên bục
		}

		if (_hVisible && _hMode >= 0) DrawHeart(c);

		var boneH = SansAssets.Tex("BoneH");
		foreach (var b in _bones)
			if (!b.Vertical) NinePatch(c, boneH, b.X, b.Y, b.W, b.H, 6f, 0f, 6f, 0f, true, true, BoneTint(b.Color));

		var warn = SansAssets.Tex("BoneStabWarn");
		foreach (var w in _warns)
			NinePatch(c, warn, w.Rect.X, w.Rect.Y, w.Rect.Width, w.Rect.Height, 4f, 4f, 4f, 4f, false, true);

		foreach (var b in _blasters) DrawBlaster(c, b);

		foreach (var b in _menuBones)
			Sprite(c, SansAssets.Frame(b.Bottom ? "MenuBoneBottom" : "MenuBoneLeft", "Default", 0), b.X, b.Y);

		DrawFightTarget(c);
		DrawTexts(c, LayerCombat);
	}

	private void DrawHeart(Canvas c)
	{
		var tex = SansAssets.Frame("PlayerHeart", _heartSplit ? "Split" : "Default", 0);
		c.SetTransform(new Vector2(MathF.Round(_hx), MathF.Round(_hy)), Rad(_hAngle), Vector2.One);
		c.DrawTexture(tex, new Rect2(-tex.Width / 2f, -tex.Height / 2f, tex.Width, tex.Height), _hTint);
		c.ResetTransform();
	}

	private void DrawBlaster(Canvas c, Blaster b)
	{
		string anim = b.Firing ? "Fire" : "Default";
		var frames = SansAssets.Anim("GasterBlaster", anim);
		int fi = 0;
		if (b.Firing)
		{
			int f = (int)(b.AnimT * 30f);
			fi = f < frames.Length ? f : (frames.Length > 3 ? 3 + (f - 3) % (frames.Length - 3) : 0);
		}
		float hotX = b.Firing ? 0.508772f : 0.491228f;
		c.SetTransform(new Vector2(MathF.Round(b.X), MathF.Round(b.Y)), Rad(b.Ang), Vector2.One);
		c.DrawTexture(frames[Math.Min(fi, frames.Length - 1)], new Rect2(-hotX * b.W, -0.5f * b.H, b.W, b.H));
		c.ResetTransform();

		if (!b.BeamVisible) return;
		float sc = b.Scale;
		float alpha = Math.Clamp(b.Opacity / 100f, 0f, 1f);
		var col = new Rgba(1f, 1f, 1f, alpha);
		var dir = new Vector2(CosDeg(b.Ang), SinDeg(b.Ang));
		void Bar(float offset, float length, float height)
		{
			if (height <= 0f) return;
			c.SetTransform(new Vector2(b.X, b.Y) + dir * offset * sc, Rad(b.Ang), Vector2.One);
			c.FillRect(0f, -height / 2f, length, height, col);
			c.ResetTransform();
		}
		Bar(70f, 1000f, b.BaseSize + b.SineSize);                 // chùm chính
		Bar(60f, 10f * sc, b.BaseSize / 1.25f + b.SineSize);      // hai bậc thu nhỏ gần miệng
		Bar(50f, 20f * sc, b.BaseSize / 2f + b.SineSize);
	}

	private void DrawFightTarget(Canvas c)
	{
		if (_target != null)
		{
			var tex = SansAssets.Frame("Target", "Default", 0);
			float w = MathF.Max(0f, _target.Width);
			Sprite(c, tex, 320f - w / 2f, 320f - 0.504274f * tex.Height, w, tex.Height, new Rgba(1f, 1f, 1f, Math.Clamp(_target.Opacity / 100f, 0f, 1f)));
		}
		if (_choice != null)
		{
			int fi = _choice.Animating && (_choice.AnimT * 30f) % 5f >= 3f ? 1 : 0;     // nhấp nháy sau khi bấm
			var tex = SansAssets.Frame("TargetChoice", "Default", fi);
			Sprite(c, tex, _choice.X - tex.Width / 2f, 320f - tex.Height / 2f);
		}
	}

	// ---------------------------------------------------------------- lớp CombatZoneClipped (cắt theo vùng chiến đấu)
	private void DrawClippedLayer(Canvas c)
	{
		if (_black || (_bones.Count == 0 && _stabs.Count == 0)) return;
		c.ResetTransform();
		c.G.SetClip(new RectangleF(ZL, ZT, ZR - ZL, ZB - ZT));
		var boneV = SansAssets.Tex("BoneV");
		foreach (var b in _bones)
			if (b.Vertical) NinePatch(c, boneV, b.X, b.Y, b.W, b.H, 0f, 6f, 0f, 6f, true, true, BoneTint(b.Color));
		var stabH = SansAssets.Tex("BoneStabH");
		var stabV = SansAssets.Tex("BoneStabV");
		foreach (var s in _stabs)
		{
			if (s.Vertical) NinePatch(c, stabV, s.X, s.Y, s.W, s.H, 0f, 6f, 0f, 6f, true, true);
			else NinePatch(c, stabH, s.X, s.Y, s.W, s.H, 6f, 0f, 6f, 0f, true, true);
		}
		c.G.ResetClip();
	}

	// ---------------------------------------------------------------- lớp Overlay
	private void DrawOverlay(Canvas c)
	{
		if (_black) c.FillRect(-64f, -64f, W + 128f, H + 128f, new Rgba(0f, 0f, 0f, 1f));
		else if (_zoneVisible)
			NinePatch(c, SansAssets.Tex("CombatZone"), ZL, ZT, ZR - ZL, ZB - ZT, 5f, 5f, 5f, 5f, false, false);

		if (_hMode < 0)
		{
			if (_hVisible) DrawHeart(c);       // trái tim chết nằm trên màn đen
			var shard = SansAssets.Anim("HeartShard", "Default");
			foreach (var s in _shards)
			{
				var tex = shard[(int)(_time * 15f) % shard.Length];
				Sprite(c, tex, s.Pos.X - tex.Width / 2f, s.Pos.Y - tex.Height / 2f, tex.Width, tex.Height, new Rgba(1f, 0f, 0f, 1f));
			}
		}
		DrawTexts(c, LayerOverlay);
	}
}
