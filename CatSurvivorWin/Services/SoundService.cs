using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace CatSurvivor;

/// <summary>
/// Âm thanh: hiệu ứng (SFX) + nhạc nền, có công tắc BẬT/TẮT (phím M, menu Cài đặt) và âm lượng riêng cho nhạc / hiệu ứng.
///  - SFX : Assets/audio/sfx/{tên}.wav   Tên dùng trong game: click, hurt, block, kill, levelup, dash, pickup, shot, boss, slam, win, lose
///  - Nhạc nền: Assets/audio/bgm.wav, tự lặp
/// Cách phát: một BỘ TRỘN ÂM nhỏ chạy trên luồng riêng, đẩy âm thanh đã trộn vào một luồng waveOut của Windows (winmm.dll, không cần thư viện ngoài).
/// Âm lượng được nhân thẳng vào từng mẫu âm thanh nên kéo về 0 là IM HẲN (trước đây dùng MCI "setaudio volume" nhưng driver waveaudio của
/// MCI không hỗ trợ lệnh đó nên thanh âm lượng không có tác dụng), nhiều hiệu ứng phát chồng lên nhau thoải mái.
/// Game chỉ xếp yêu cầu vào hàng đợi (rẻ) rồi đi tiếp. Thiếu file / không có thiết bị âm thanh thì bỏ qua và báo một lần (không crash).
/// File WAV: PCM 8 hoặc 16 bit, 1 hoặc 2 kênh, tần số bất kì (tự đổi sang 44100 Hz stereo khi nạp).
/// </summary>
public static class SoundService
{
	// ------------------------------------------------------------------ winmm
	private const uint WaveMapper = 0xFFFFFFFF;
	private const uint WhdrDone = 1;

	[StructLayout(LayoutKind.Sequential)]
	private struct WaveFormatEx
	{
		public ushort wFormatTag, nChannels;
		public uint nSamplesPerSec, nAvgBytesPerSec;
		public ushort nBlockAlign, wBitsPerSample, cbSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct WaveHdr
	{
		public IntPtr lpData;
		public uint dwBufferLength, dwBytesRecorded;
		public IntPtr dwUser;
		public uint dwFlags, dwLoops;
		public IntPtr lpNext, reserved;
	}

	[DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr hwo, uint deviceId, ref WaveFormatEx format, IntPtr callback, IntPtr instance, uint flags);
	[DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr header, int size);
	[DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr header, int size);
	[DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr hwo, IntPtr header, int size);
	[DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr hwo);
	[DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr hwo);

	// ------------------------------------------------------------------ trạng thái công khai
	public static bool Enabled { get; private set; } = true;

	/// <summary>Số hiệu ứng đã nạp được / nhạc nền đã nạp (để kiểm tra).</summary>
	public static int LoadedCount => Clips.Count;
	public static bool BgmLoaded => _bgm != null;

	public static event Action<bool> EnabledChanged;

	private static readonly string[] SfxNames = { "click", "hurt", "block", "kill", "levelup", "dash", "pickup", "shot", "boss", "slam", "win", "lose" };

	/// <summary>Thông báo lỗi gần nhất (để chẩn đoán khi không phát được).</summary>
	public static string LastError { get; private set; }

	// ------------------------------------------------------------------ bộ trộn
	private const int Rate = 44100;               // tần số bộ trộn (stereo, 16 bit)
	private const int BufferFrames = 1024;        // ~23 ms mỗi buffer
	private const int BufferCount = 4;            // trễ tối đa ~ 4 x 23 ms
	private const int MaxVoices = 32;             // số hiệu ứng phát chồng tối đa (quá nhiều thì bỏ bớt hiệu ứng mới)
	private const float CallDbWeight = 0.25f;     // mức dB truyền vào Play() chỉ chỉnh nhẹ (file wav vốn đã được tạo nhỏ sẵn)

	private sealed class Clip { public short[] Samples; }       // stereo xen kẽ L R, 44100 Hz
	private sealed class Voice
	{
		public Clip Clip; public int Pos; public float Gain;
		public float Rate = 1f; public double Fpos; public string Tag; // Rate != 1: phát nhanh/chậm (nội suy), Pos tính bằng Fpos
	}

	private static readonly Dictionary<string, Clip> Clips = new();
	private static Clip _bgm;
	private static int _bgmPos;

	// Âm thanh phụ nạp theo yêu cầu (vd. trận đánh Sans: Assets/sans/audio/*.wav) + kênh nhạc nền riêng thay cho bgm.wav khi đang phát
	private static readonly Dictionary<string, Clip> Extras = new();
	private static volatile Clip _extMusic;
	private static int _extPos;
	private static volatile bool _extPaused;
	private static volatile bool _bgmMuted;

	private static readonly ConcurrentQueue<Voice> Incoming = new();
	private static readonly List<Voice> Active = new();
	private static volatile float _musicGain, _sfxGain = 1f;
	private static float _musicGainPrev;
	private static volatile bool _stop;
	private static Thread _mixer;
	private static IntPtr _hwo;
	private static bool _inited;
	private static readonly Dictionary<string, long> LastPlay = new();   // chỉ luồng game dùng

	/// <summary>Âm lượng 0..100 (thanh trượt) -> hệ số nhân biên độ. Bình phương để thanh trượt nghe đều tai; 0 = im hẳn, 100 = nguyên mức file.</summary>
	private static float Curve(int percent)
	{
		float p = Mx.Clamp(percent, 0, 100) / 100f;
		return p * p;
	}

	public static void Init()
	{
		if (_inited) return;
		_inited = true;

		Settings.Load(out string err);
		if (err != null) AssetStore.Report(Storage.SettingsFile, err);
		Enabled = Settings.Sound;
		_musicGain = _musicGainPrev = Curve(Settings.MusicVolume);
		_sfxGain = Curve(Settings.SfxVolume);

		try
		{
			foreach (var n in SfxNames)
			{
				var clip = LoadClip($"audio/sfx/{n}.wav", "thiếu âm thanh");
				if (clip != null) Clips[n] = clip;
			}
			_bgm = LoadClip("audio/bgm.wav", "thiếu nhạc nền");
		}
		catch (Exception ex) { AssetStore.Report("Assets/audio", ex.Message); }

		if (!OpenDevice()) return;
		_mixer = new Thread(MixLoop) { IsBackground = true, Name = "SoundMixer", Priority = ThreadPriority.AboveNormal };
		_mixer.Start();
	}

	public static void Shutdown()
	{
		if (!_inited) return;
		_stop = true;
		_mixer?.Join(1500);
		_mixer = null;
		_inited = false;
	}

	// ------------------------------------------------------------------ bật/tắt
	public static void Toggle() => SetEnabled(!Enabled);

	public static void SetEnabled(bool on)
	{
		if (Enabled == on) return;
		Enabled = on;
		Settings.Sound = on;
		Settings.Save();
		EnabledChanged?.Invoke(on);
	}

	// ------------------------------------------------------------------ hiệu ứng
	/// <summary>Phát một hiệu ứng. minGap: khoảng cách tối thiểu (giây) giữa hai lần phát cùng một tên, tránh ồn khi xảy ra liên tục. volumeDb: chỉnh nhẹ độ to riêng của lần phát này.</summary>
	public static void Play(string name, float volumeDb = -4f, float minGap = 0.05f)
	{
		if (!Enabled || !_inited || _mixer == null) return;
		if (!Clips.TryGetValue(name, out var clip)) return;
		float gain = _sfxGain * MathF.Pow(10f, MathF.Min(volumeDb, 0f) * CallDbWeight / 20f);
		if (gain <= 0f) return;                                       // âm lượng hiệu ứng = 0: không phát gì
		long now = Environment.TickCount64;
		if (LastPlay.TryGetValue(name, out long last) && now - last < minGap * 1000f) return;
		LastPlay[name] = now;
		if (Incoming.Count > MaxVoices) return;                       // hàng đợi quá dài: bỏ bớt
		Incoming.Enqueue(new Voice { Clip = clip, Gain = gain });
	}

	// ------------------------------------------------------------------ âm thanh phụ (trận Sans)
	private static Clip Extra(string name)
	{
		if (Extras.TryGetValue(name, out var c)) return c;
		c = LoadClip($"sans/audio/{name}.wav", "thiếu âm thanh");
		Extras[name] = c;
		return c;
	}

	/// <summary>Nạp trước các âm thanh phụ (tránh khựng lần phát đầu). Tên = file trong Assets/sans/audio, không có đuôi.</summary>
	public static void PreloadExtras(params string[] names) { foreach (var n in names) Extra(n); }

	/// <summary>Phát một âm thanh phụ. rate: tốc độ phát (1 = gốc, 1.2 = nhanh/cao hơn 20%). tag: để dừng riêng bằng StopExtra(tag).</summary>
	public static void PlayExtra(string name, float rate = 1f, string tag = null, float gainScale = 1f)
	{
		if (!Enabled || !_inited || _mixer == null) return;
		var clip = Extra(name);
		if (clip == null) return;
		float gain = _sfxGain * gainScale;
		if (gain <= 0f || Incoming.Count > MaxVoices) return;
		Incoming.Enqueue(new Voice { Clip = clip, Gain = gain, Rate = rate > 0.05f ? rate : 1f, Tag = tag });
	}

	/// <summary>Dừng các âm thanh phụ đang phát có tag này ("*" = tất cả).</summary>
	public static void StopExtra(string tag) { if (_inited) Incoming.Enqueue(new Voice { Tag = tag }); }

	/// <summary>Bắt đầu một bản nhạc phụ (lặp) thay cho nhạc nền của game cho tới khi gọi StopExtraMusic().</summary>
	public static void PlayExtraMusic(string name)
	{
		var clip = Extra(name);
		if (clip == null || !_inited) return;
		_extPos = 0;
		_extPaused = false;
		_extMusic = clip;
	}

	public static void SetExtraMusicPaused(bool paused) => _extPaused = paused;
	public static void StopExtraMusic() => _extMusic = null;
	public static bool ExtraMusicPlaying => _extMusic != null;

	/// <summary>Tắt riêng nhạc nền của game (bgm.wav) trong lúc chơi trận Sans; tiếng hiệu ứng và nhạc phụ không bị ảnh hưởng.</summary>
	public static bool BgmMuted { get => _bgmMuted; set => _bgmMuted = value; }

	// ------------------------------------------------------------------ âm lượng (menu Cài đặt)
	/// <summary>Đặt âm lượng nhạc nền 0..100 (áp dụng ngay, 0 = im hẳn; lưu bằng Settings.Save()).</summary>
	public static void SetMusicVolume(int percent)
	{
		Settings.MusicVolume = Mx.Clamp(percent, 0, 100);
		_musicGain = Curve(Settings.MusicVolume);
	}

	/// <summary>Đặt âm lượng hiệu ứng 0..100 (có hiệu lực từ lần phát kế tiếp, 0 = im hẳn; lưu bằng Settings.Save()).</summary>
	public static void SetSfxVolume(int percent)
	{
		Settings.SfxVolume = Mx.Clamp(percent, 0, 100);
		_sfxGain = Curve(Settings.SfxVolume);
	}

	// ------------------------------------------------------------------ thiết bị + vòng trộn
	private static bool OpenDevice()
	{
		var fmt = new WaveFormatEx
		{
			wFormatTag = 1, nChannels = 2, nSamplesPerSec = Rate, wBitsPerSample = 16,
			nBlockAlign = 4, nAvgBytesPerSec = Rate * 4, cbSize = 0,
		};
		try
		{
			int r = waveOutOpen(out _hwo, WaveMapper, ref fmt, IntPtr.Zero, IntPtr.Zero, 0);
			if (r != 0)
			{
				LastError = $"không mở được thiết bị âm thanh (mã {r})";
				AssetStore.Report("Âm thanh", LastError);
				_hwo = IntPtr.Zero;
				return false;
			}
			return true;
		}
		catch (Exception ex) // winmm không dùng được: coi như không có âm thanh
		{
			LastError = ex.Message;
			AssetStore.Report("Âm thanh", ex.Message);
			return false;
		}
	}

	private static void MixLoop()
	{
		int hdrSize = Marshal.SizeOf<WaveHdr>();
		int bytes = BufferFrames * 4;
		var headers = new IntPtr[BufferCount];
		var data = new IntPtr[BufferCount];
		var started = new bool[BufferCount];
		int flagsOffset = (int)Marshal.OffsetOf<WaveHdr>(nameof(WaveHdr.dwFlags));
		var mix = new float[BufferFrames * 2];
		var outBuf = new short[BufferFrames * 2];

		try
		{
			for (int i = 0; i < BufferCount; i++)
			{
				data[i] = Marshal.AllocHGlobal(bytes);
				headers[i] = Marshal.AllocHGlobal(hdrSize);
				var h = new WaveHdr { lpData = data[i], dwBufferLength = (uint)bytes };
				Marshal.StructureToPtr(h, headers[i], false);
				if (waveOutPrepareHeader(_hwo, headers[i], hdrSize) != 0) throw new InvalidOperationException("waveOutPrepareHeader");
			}

			while (!_stop)
			{
				bool wrote = false;
				for (int i = 0; i < BufferCount; i++)
				{
					if (started[i] && (Marshal.ReadInt32(headers[i], flagsOffset) & WhdrDone) == 0) continue; // buffer này còn đang phát
					Mix(mix, outBuf);
					Marshal.Copy(outBuf, 0, data[i], outBuf.Length);
					waveOutWrite(_hwo, headers[i], hdrSize);
					started[i] = true;
					wrote = true;
				}
				if (!wrote) Thread.Sleep(3);
			}
		}
		catch (Exception ex) { LastError = ex.Message; AssetStore.Report("Âm thanh", ex.Message); }
		finally
		{
			try
			{
				waveOutReset(_hwo);
				for (int i = 0; i < BufferCount; i++)
				{
					if (headers[i] == IntPtr.Zero) continue;
					waveOutUnprepareHeader(_hwo, headers[i], hdrSize);
					Marshal.FreeHGlobal(headers[i]);
					if (data[i] != IntPtr.Zero) Marshal.FreeHGlobal(data[i]);
				}
				waveOutClose(_hwo);
			}
			catch (Exception) { }
		}
	}

	/// <summary>Trộn nhạc nền + các hiệu ứng đang phát vào một buffer BufferFrames khung (stereo 16 bit).</summary>
	private static void Mix(float[] mix, short[] outBuf)
	{
		Array.Clear(mix, 0, mix.Length);
		bool on = Enabled;

		// Nhận hiệu ứng mới (theo đúng thứ tự xếp hàng; voice không có Clip = lệnh dừng theo tag); đang tắt âm thì bỏ hết
		while (Incoming.TryDequeue(out var v))
		{
			if (v.Clip == null) { string tag = v.Tag; Active.RemoveAll(a => a.Tag != null && (tag == "*" || a.Tag == tag)); }
			else if (on && Active.Count < MaxVoices) Active.Add(v);
		}
		if (!on) Active.Clear();

		// Nhạc nền: lặp vòng; hệ số âm lượng đổi dần trong buffer để không "lách tách" khi kéo thanh trượt. Tắt âm: giữ nguyên vị trí (bật lại thì nghe tiếp)
		var ext = _extMusic;
		if (ext != null)
		{
			// Nhạc phụ (trận Sans) thay cho nhạc nền; đang tạm dừng thì im lặng nhưng giữ nguyên vị trí
			float g0 = _musicGainPrev, g1 = _musicGain;
			if (on && !_extPaused)
			{
				var s = ext.Samples;
				int pos = _extPos;
				for (int i = 0; i < BufferFrames; i++)
				{
					float g = g0 + (g1 - g0) * (i / (float)BufferFrames);
					mix[2 * i] += s[pos] * g;
					mix[2 * i + 1] += s[pos + 1] * g;
					pos += 2;
					if (pos >= s.Length) pos = 0;
				}
				_extPos = pos;
			}
			_musicGainPrev = g1;
		}
		else if (_bgm != null && on && !_bgmMuted)
		{
			float g0 = _musicGainPrev, g1 = _musicGain;
			var s = _bgm.Samples;
			int pos = _bgmPos;
			for (int i = 0; i < BufferFrames; i++)
			{
				float g = g0 + (g1 - g0) * (i / (float)BufferFrames);
				mix[2 * i] += s[pos] * g;
				mix[2 * i + 1] += s[pos + 1] * g;
				pos += 2;
				if (pos >= s.Length) pos = 0;
			}
			_bgmPos = pos;
			_musicGainPrev = g1;
		}
		else _musicGainPrev = _musicGain;

		// Hiệu ứng
		for (int k = Active.Count - 1; k >= 0; k--)
		{
			var v = Active[k];
			var s = v.Clip.Samples;
			if (v.Rate != 1f)
			{
				// Phát đổi tốc độ: đọc theo bước Rate khung/khung và nội suy tuyến tính giữa hai khung kề nhau
				int total = s.Length / 2;
				int i = 0;
				for (; i < BufferFrames; i++)
				{
					int f0 = (int)v.Fpos;
					if (f0 >= total - 1) break;
					float t = (float)(v.Fpos - f0);
					mix[2 * i] += (s[2 * f0] * (1f - t) + s[2 * f0 + 2] * t) * v.Gain;
					mix[2 * i + 1] += (s[2 * f0 + 1] * (1f - t) + s[2 * f0 + 3] * t) * v.Gain;
					v.Fpos += v.Rate;
				}
				if (i < BufferFrames) Active.RemoveAt(k);
				continue;
			}
			int frames = Math.Min(BufferFrames, (s.Length - v.Pos) / 2);
			for (int i = 0; i < frames; i++)
			{
				mix[2 * i] += s[v.Pos] * v.Gain;
				mix[2 * i + 1] += s[v.Pos + 1] * v.Gain;
				v.Pos += 2;
			}
			if (v.Pos >= s.Length) Active.RemoveAt(k);
		}

		float peak = 0f;
		for (int i = 0; i < mix.Length; i++)
		{
			float x = mix[i];
			outBuf[i] = (short)(x > 32767f ? 32767 : x < -32768f ? -32768 : x);
			float a = x < 0f ? -x : x;
			if (a > peak) peak = a;
		}
		_lastPeak = peak / 32768f;
	}

	private static volatile float _lastPeak;

	/// <summary>Mức đỉnh (0..1) của buffer vừa gửi ra loa: dùng để kiểm tra tự động (âm lượng 0 phải cho 0).</summary>
	public static float LastPeak => _lastPeak;

	// ------------------------------------------------------------------ nạp WAV
	/// <summary>Nạp một file WAV PCM (8/16 bit, 1/2 kênh) thành mẫu stereo 16 bit 44100 Hz; thiếu/hỏng thì báo một lần và trả null.</summary>
	private static Clip LoadClip(string relative, string missingText)
	{
		string path = AssetStore.PathOf(relative);
		if (!File.Exists(path)) { AssetStore.Report("Assets/" + relative, missingText); return null; }
		try
		{
			byte[] b = File.ReadAllBytes(path);
			if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') throw new InvalidDataException("không phải file WAV");

			int channels = 0, bits = 0, srcRate = 0, dataOff = -1, dataLen = 0;
			int pos = 12;
			while (pos + 8 <= b.Length)
			{
				string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
				int len = BitConverter.ToInt32(b, pos + 4);
				if (len < 0) break;
				if (id == "fmt ")
				{
					int tag = BitConverter.ToUInt16(b, pos + 8);
					channels = BitConverter.ToUInt16(b, pos + 10);
					srcRate = BitConverter.ToInt32(b, pos + 12);
					bits = BitConverter.ToUInt16(b, pos + 22);
					if (tag != 1 && tag != 0xFFFE) throw new InvalidDataException("chỉ hỗ trợ WAV PCM (không nén)");
				}
				else if (id == "data")
				{
					dataOff = pos + 8;
					dataLen = Math.Min(len, b.Length - dataOff);
					break;
				}
				pos += 8 + len + (len & 1);
			}
			if (dataOff < 0 || channels < 1 || channels > 2 || srcRate <= 0) throw new InvalidDataException("thiếu thông tin định dạng/dữ liệu");
			if (bits != 8 && bits != 16) throw new InvalidDataException($"chỉ hỗ trợ 8 hoặc 16 bit (file này {bits} bit)");

			int bytesPerSample = bits / 8;
			int srcFrames = dataLen / (bytesPerSample * channels);
			if (srcFrames == 0) throw new InvalidDataException("không có dữ liệu âm thanh");

			float Sample(int frame, int ch)
			{
				frame = Math.Min(frame, srcFrames - 1);
				int o = dataOff + (frame * channels + Math.Min(ch, channels - 1)) * bytesPerSample;
				return bits == 16 ? BitConverter.ToInt16(b, o) : (b[o] - 128) * 256f;
			}

			int dstFrames = (int)((long)srcFrames * Rate / srcRate);
			var samples = new short[dstFrames * 2];
			for (int i = 0; i < dstFrames; i++)
			{
				double sp = (double)i * srcRate / Rate;      // vị trí trong file gốc (nội suy tuyến tính)
				int f0 = (int)sp;
				float t = (float)(sp - f0);
				for (int ch = 0; ch < 2; ch++)
					samples[2 * i + ch] = (short)(Sample(f0, ch) * (1f - t) + Sample(f0 + 1, ch) * t);
			}
			return new Clip { Samples = samples };
		}
		catch (Exception ex)
		{
			AssetStore.Report("Assets/" + relative, "không nạp được âm thanh: " + ex.Message);
			return null;
		}
	}
}
