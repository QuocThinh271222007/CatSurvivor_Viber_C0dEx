using System.Numerics;

namespace CatSurvivor;

/// <summary>
/// Một đội hình quái. Đội hình có "tâm" di chuyển theo luật riêng; mỗi thành viên bám vào ô (Slot) của mình
/// quanh tâm đó. Khi đội hình giải thể (tới gần người chơi / đi qua / hết giờ) mỗi con quay về AI riêng.
///
///  March  : đi thẳng theo hướng cố định (tường quét, gọng kìm) -> người chơi né được vì hướng không đổi
///  Weave  : bay về phía người chơi nhưng tâm lượn qua lại (đàn dơi chữ V, đội bomber)
///  Escort : hộ tống một "thủ lĩnh" (Brute): hộ vệ đi sau lưng thủ lĩnh, quay mặt về phía người chơi
/// </summary>
public class Formation
{
	public enum Mode { March, Weave, Escort }

	public Mode Kind;
	public Vector2 Center;          // March/Weave: tâm đội hình (Escort: dùng vị trí thủ lĩnh)
	public Vector2 Dir = V.Right;
	public float Speed;
	public float Life = 15f;        // quá thời gian này thì giải thể
	public float BreakDist;         // tới gần người chơi hơn mức này thì giải thể (0 = không dùng)
	public float WeaveAmp, WeaveFreq = 1.6f, WeavePhase;
	public float TurnRate;          // March: tốc độ xoay tối đa (rad/s) để hướng đội hình bám theo người chơi (0 = đi thẳng cố định)
	public Enemy Leader;            // chỉ dùng cho Escort
	public int Variant;             // bậc biến thể màu của cả đội hình (mọi thành viên cùng màu cho dễ đọc)
	public bool Active = true;
	public float Age;
	public readonly List<Enemy> Members = new();

	public Vector2 Perp => new(-Dir.Y, Dir.X);

	/// <summary>Vị trí thế giới của ô đội hình mà quái `e` phải bám theo.</summary>
	public Vector2 SlotPos(Enemy e)
	{
		Vector2 origin = Kind == Mode.Escort && Leader != null ? Leader.Pos : Center;
		return origin + Dir * e.Slot.X + Perp * e.Slot.Y;
	}

	public void Update(float dt, Player player)
	{
		if (!Active) return;
		Age += dt;
		Vector2 pp = player.Pos;

		switch (Kind)
		{
			case Mode.March:
			{
				// Xoay dần hướng đi về phía người chơi (giới hạn tốc độ xoay nên vẫn đọc được đường đi và né được)
				if (TurnRate > 0f)
				{
					Vector2 toPlayer = pp - Center;
					if (toPlayer.LengthSquared() > 1f)
					{
						float cur = Dir.Angle();
						float diff = Mx.Wrap(toPlayer.Angle() - cur, -Mx.Pi, Mx.Pi);
						Dir = V.FromAngle(cur + Mx.Clamp(diff, -TurnRate * dt, TurnRate * dt));
					}
				}
				Center += Dir * Speed * dt;
				Vector2 rel = pp - Center;
				bool passed = rel.Dot(Dir) < -70f;                       // tường đã quét qua người chơi
				bool close = BreakDist > 0f && rel.Length() < BreakDist; // gọng kìm đã tới nơi
				if (passed || close) Active = false;
				break;
			}
			case Mode.Weave:
			{
				Vector2 to = pp - Center;
				float dist = to.Length();
				if (dist > 0.01f) Dir = Vector2.Lerp(Dir, to / dist, 1f - MathF.Exp(-3f * dt)).Normalized();
				// Tâm lượn ngang qua lại: cả đội hình uốn éo như một con rắn thay vì đi đường thẳng
				Vector2 heading = (Dir + Perp * MathF.Sin(Age * WeaveFreq + WeavePhase) * WeaveAmp).Normalized();
				Center += heading * Speed * dt;
				if (BreakDist > 0f && dist < BreakDist) Active = false;
				break;
			}
			case Mode.Escort:
			{
				// Thủ lĩnh chết / đang lao / đã tới gần -> hộ vệ tách ra tự chiến đấu
				if (Leader == null || Leader.Dead || Leader.ChargeState == 2 || Leader.Airborne)
				{
					Active = false;
					break;
				}
				Vector2 to = pp - Leader.Pos;
				float dist = to.Length();
				if (dist > 0.01f) Dir = Vector2.Lerp(Dir, to / dist, 1f - MathF.Exp(-4f * dt)).Normalized();
				if (BreakDist > 0f && dist < BreakDist) Active = false;
				break;
			}
		}

		if (Age > Life) Active = false;
	}
}
