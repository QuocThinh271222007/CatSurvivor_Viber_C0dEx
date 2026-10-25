namespace CatSurvivor;

// Các điểm truy cập dành cho tự kiểm tra (SelfTest --sans); không dùng khi chơi.
internal sealed partial class SansFight
{
	internal readonly HashSet<string> TestUnknown = new();
	internal bool TestPanicked => _texts.Any(t => t.Name == "Error");
	internal void TestSkipBegin() => _begun = true;
	internal int TestMenuState => _menuState;
	internal bool TestTextWaiting => _texts.Any(t => t.Interactive);
	internal bool TestScriptRunning => _tl.Running;
	/// <summary>Đang trong lượt Sans tấn công (kể cả lúc kịch bản tạm dừng chờ khung co lại).</summary>
	internal bool TestAttacking => _hEnabled;
	internal bool TestTargetActive => _target != null && _target.State == 0;
	internal bool TestDodging => _dodgeState != 0;
	internal bool TestBlack => _black;
	internal string TestInfoText => _infoText;
	internal int TestBoneCount => _bones.Count + _stabs.Count;
	internal int TestBlasterCount => _blasters.Count;
	internal int TestBeamCount => _blasters.Count(b => b.BeamVisible);
	internal int TestWarnCount => _warns.Count;
	internal int TestStabCount => _stabs.Count;
	internal bool TestStrike => _strike != null;
	internal int TestPlatformCount => _platforms.Count;
	internal int TestMenuBoneCount => _menuBones.Count;
	internal (float x, float y, int mode, bool visible) TestHeart => (_hx, _hy, _hMode, _hVisible);
	internal (float l, float t, float r, float b) TestZone => (ZL, ZT, ZR, ZB);
	internal int TestSelected => Top.Sel;
	internal int TestMenuDepth => _stack.Count;
	internal int TestItemCount => _items.Count;

	/// <summary>Dừng kịch bản và xoá mọi vật tấn công (để thử riêng từng vật).</summary>
	internal void TestFreeze() { _tl.Stop(); ClearAttackObjects(); }

	internal void TestCall(string name, params string[] args) => Call(name, args);
	internal void TestAddBone(bool vertical, int x, int y, int len, int dir, int speed, int color) => AddBone(vertical, x, y, len, dir, speed, color);
	internal (float x, float y, float dx, float dy) TestPlatform0 => _platforms.Count == 0 ? (0, 0, 0, 0) : (_platforms[0].X, _platforms[0].Y, _platforms[0].Dx, _platforms[0].Dy);
	internal float TestShake => ShakeOffset.Length();
	internal float TestKr => Kr;

	/// <summary>Chạy thẳng một kịch bản đòn đánh (như khi tới lượt của nó).</summary>
	internal void TestRunAttack(string name)
	{
		_hEnabled = true;
		_hdx = _hdy = 0f;
		ResetVars();
		MenuBonesOff();
		_black = false;
		RunAttack(name);
	}

	/// <summary>Đã kết thúc lượt tấn công và quay về menu chưa.</summary>
	internal bool TestBackInMenu => _menuState >= 1;
}
