using System.Numerics;

namespace CatSurvivor;

/// <summary>Hàm toán dùng chung (thay cho Mathf của Godot).</summary>
public static class Mx
{
	public const float Pi = MathF.PI;
	public const float Tau = MathF.PI * 2f;

	public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
	public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
	public static float Lerp(float a, float b, float t) => a + (b - a) * t;
	public static float DegToRad(float d) => d * Pi / 180f;
	public static float RadToDeg(float r) => r * 180f / Pi;

	/// <summary>Quấn giá trị vào khoảng [min, max) (vd. Wrap(góc, -Pi, Pi) cho hiệu hai góc).</summary>
	public static float Wrap(float v, float min, float max)
	{
		float range = max - min;
		if (range <= 0f) return min;
		float x = (v - min) % range;
		if (x < 0f) x += range;
		return x + min;
	}
}

/// <summary>Hàm mở rộng và hằng cho vector 2D (System.Numerics.Vector2) theo kiểu quen thuộc của game 2D.</summary>
public static class V
{
	public static readonly Vector2 Right = new(1f, 0f);
	public static readonly Vector2 Left = new(-1f, 0f);

	public static Vector2 FromAngle(float a) => new(MathF.Cos(a), MathF.Sin(a));

	/// <summary>Vector đơn vị; vector 0 thì trả về 0 (không sinh NaN).</summary>
	public static Vector2 Normalized(this Vector2 v)
	{
		float l = v.Length();
		return l > 1e-6f ? v / l : Vector2.Zero;
	}

	public static float Angle(this Vector2 v) => MathF.Atan2(v.Y, v.X);

	public static Vector2 Rotated(this Vector2 v, float a)
	{
		float c = MathF.Cos(a), s = MathF.Sin(a);
		return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
	}

	public static Vector2 LimitLength(this Vector2 v, float max)
	{
		float l = v.Length();
		return l > max && l > 1e-6f ? v / l * max : v;
	}

	public static float Dot(this Vector2 a, Vector2 b) => Vector2.Dot(a, b);
}

/// <summary>Hình chữ nhật (vị trí góc trên trái + kích thước).</summary>
public readonly struct Rect2
{
	public readonly Vector2 Position;
	public readonly Vector2 Size;

	public Rect2(Vector2 position, Vector2 size) { Position = position; Size = size; }
	public Rect2(float x, float y, float w, float h) { Position = new Vector2(x, y); Size = new Vector2(w, h); }

	public Vector2 End => Position + Size;
	public Vector2 Center => Position + Size * 0.5f;
	public Rect2 Grow(float m) => new(Position - new Vector2(m, m), Size + new Vector2(2f * m, 2f * m));
}

/// <summary>Màu RGBA dạng số thực 0..1 (đổi sang System.Drawing.Color khi vẽ).</summary>
public readonly struct Rgba
{
	public readonly float R, G, B, A;

	public Rgba(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
	public Rgba(Rgba c, float a) { R = c.R; G = c.G; B = c.B; A = a; }

	public static readonly Rgba White = new(1f, 1f, 1f);
	public static readonly Rgba Black = new(0f, 0f, 0f);

	public Rgba Lerp(Rgba to, float t) => new(Mx.Lerp(R, to.R, t), Mx.Lerp(G, to.G, t), Mx.Lerp(B, to.B, t), Mx.Lerp(A, to.A, t));
	public Rgba WithAlpha(float a) => new(R, G, B, a);

	public static Rgba From(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

	public Color ToColor()
	{
		static int B8(float v) => (int)MathF.Round(Mx.Clamp(v, 0f, 1f) * 255f);
		return Color.FromArgb(B8(A), B8(R), B8(G), B8(B));
	}
}

/// <summary>Số ngẫu nhiên (thay cho GD.Randf / GD.Randi / GD.RandRange).</summary>
public static class Rng
{
	private static readonly Random R = new();

	public static float Randf() => (float)R.NextDouble();
	public static uint Randi() => (uint)R.Next();
	/// <summary>Số nguyên ngẫu nhiên trong [a, b] (gồm cả b).</summary>
	public static int RandRange(int a, int b) => R.Next(a, b + 1);
}
