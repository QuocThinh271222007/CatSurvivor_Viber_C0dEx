using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CatSurvivor;

/// <summary>
/// Khung hình ở độ phân giải gốc của game (640x360): một DIB section của Windows được bọc thành Bitmap GDI+ để vẽ vào,
/// rồi phóng to lên cửa sổ bằng StretchBlt (nearest-neighbor, rất nhanh). Vẽ ở độ phân giải gốc rồi phóng một lần nhanh hơn nhiều
/// so với vẽ từng sprite đã phóng to bằng GDI+ (đo thực tế: 140 sprite từ ~20 ms xuống vài ms) và cho ra pixel-art "đúng chuẩn".
/// </summary>
internal sealed class FrameBuffer : IDisposable
{
	[StructLayout(LayoutKind.Sequential)]
	private struct BitmapInfoHeader
	{
		public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, SizeImage;
		public int XPelsPerMeter, YPelsPerMeter; public uint ClrUsed, ClrImportant;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BitmapInfo { public BitmapInfoHeader Header; public uint Colors; }

	[DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);
	[DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
	[DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
	[DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
	[DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
	[DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
	[DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, uint rop);

	[DllImport("gdi32.dll")] private static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr old);

	private const int ColorOnColor = 3;     // nearest-neighbor
	private const int Halftone = 4;         // lọc trung bình khi thu nhỏ
	private const uint SrcCopy = 0x00CC0020;

	private readonly int _w, _h;
	private IntPtr _dc, _hbm, _old;

	public Bitmap Bitmap { get; private set; }
	public Graphics G { get; private set; }
	public bool Ok { get; private set; }

	public FrameBuffer(int width, int height)
	{
		_w = width; _h = height;
		try
		{
			var bi = new BitmapInfo
			{
				Header = new BitmapInfoHeader { Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = width, Height = -height, Planes = 1, BitCount = 32 }, // Height âm = từ trên xuống
			};
			_hbm = CreateDIBSection(IntPtr.Zero, ref bi, 0, out IntPtr bits, IntPtr.Zero, 0);
			if (_hbm == IntPtr.Zero || bits == IntPtr.Zero) return;
			_dc = CreateCompatibleDC(IntPtr.Zero);
			_old = SelectObject(_dc, _hbm);
			Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, bits);
			G = Graphics.FromImage(Bitmap);
			Ok = true;
		}
		catch (Exception)
		{
			Ok = false; // không tạo được DIB: GameCanvas rơi về cách vẽ trực tiếp
		}
	}

	/// <summary>Phóng to khung hình vào hình chữ nhật dest của target (nearest-neighbor).</summary>
	public void BlitTo(Graphics target, Rectangle dest)
	{
		IntPtr hdc = target.GetHdc();
		try
		{
			SetStretchBltMode(hdc, ColorOnColor);
			StretchBlt(hdc, dest.X, dest.Y, dest.Width, dest.Height, _dc, 0, 0, _w, _h, SrcCopy);
		}
		finally { target.ReleaseHdc(hdc); }
	}

	/// <summary>
	/// Phóng to vừa khít hình chữ nhật dest có TỈ LỆ LẺ mà vẫn nét ("sharp"): trước hết phóng NGUYÊN số lần (làm tròn lên) bằng nearest vào bộ đệm trung gian
	/// (mỗi điểm ảnh gốc thành khối đều nhau), rồi thu nhỏ nhẹ về đúng cỡ bằng bilinear (~10 ms ở 1080p, đắt hơn nearest ~3 ms nên chỉ dùng khi tỉ lệ lệch xa bội nguyên). Điểm ảnh đều và sắc hơn hẳn nearest lẻ (chỗ to chỗ nhỏ).
	/// scratch được tạo/đổi cỡ khi cần (do GameCanvas giữ).
	/// </summary>
	public void BlitSharp(Graphics target, Rectangle dest, ref FrameBuffer scratch)
	{
		int k = Math.Max(1, (int)Math.Ceiling(Math.Max(dest.Width / (double)_w, dest.Height / (double)_h)));
		if (scratch == null || scratch._w != _w * k || scratch._h != _h * k)
		{
			scratch?.Dispose();
			scratch = new FrameBuffer(_w * k, _h * k);
		}
		if (!scratch.Ok) { BlitTo(target, dest); return; }

		SetStretchBltMode(scratch._dc, ColorOnColor);
		StretchBlt(scratch._dc, 0, 0, _w * k, _h * k, _dc, 0, 0, _w, _h, SrcCopy);

		// Thu nhỏ nhẹ từ bản phóng nguyên số lần về đúng cỡ bằng GDI+ bilinear (nhanh hơn nhiều so với HALFTONE của GDI)
		var gs = target.Save();
		try
		{
			target.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
			target.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
			target.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
			target.DrawImage(scratch.Bitmap, dest, 0, 0, _w * k, _h * k, GraphicsUnit.Pixel);
		}
		finally { target.Restore(gs); }
	}
	public void Dispose()
	{
		G?.Dispose();
		Bitmap?.Dispose();
		if (_dc != IntPtr.Zero) { SelectObject(_dc, _old); DeleteDC(_dc); _dc = IntPtr.Zero; }
		if (_hbm != IntPtr.Zero) { DeleteObject(_hbm); _hbm = IntPtr.Zero; }
		Ok = false;
	}
}
