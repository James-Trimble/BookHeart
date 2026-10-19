using System.IO;
using NAudio.Vorbis;
using NAudio.Wave;

namespace BookHeart.Audio;

/// <summary>Plays one audio file at a time, with speed, seeking and boosted volume.</summary>
internal sealed class AudioEngine : IDisposable
{
    private WaveStream? _reader;
    private TempoSampleProvider? _tempo;
    private BoostSampleProvider? _boost;
    private WaveOutEvent? _output;
    private bool _closing;
    private double _speed = 1;
    private float _gain = 1;

    /// <summary>Raised on the UI thread when the file plays to its end.</summary>
    public event EventHandler? Ended;

    /// <summary>Raised on the UI thread when the sound device fails during playback.</summary>
    public event EventHandler<Exception>? Failed;

    public bool IsOpen => _reader is not null;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;
    public long FileSize { get; private set; }

    public TimeSpan Position
    {
        get => _tempo?.Position ?? TimeSpan.Zero;
        set { if (_tempo is not null) _tempo.Position = value; }
    }

    public double Speed
    {
        get => _speed;
        set { _speed = value; if (_tempo is not null) _tempo.Tempo = value; }
    }

    public int VolumePercent
    {
        get => (int)Math.Round(_gain * 100);
        set { _gain = Math.Clamp(value, 0, 300) / 100f; if (_boost is not null) _boost.Gain = _gain; }
    }

    /// <summary>Opens a file ready to play. Throws if the file can't be read or decoded.</summary>
    public void Open(string path)
    {
        Close();
        var reader = OpenReader(path);
        try
        {
            var tempo = new TempoSampleProvider(reader) { Tempo = _speed };
            var boost = new BoostSampleProvider(tempo) { Gain = _gain };
            // Created on the UI thread, so PlaybackStopped is raised on the UI thread.
            var output = new WaveOutEvent { DesiredLatency = 250, NumberOfBuffers = 3 };
            output.Init(boost);
            output.PlaybackStopped += OnPlaybackStopped;
            (_reader, _tempo, _boost, _output) = (reader, tempo, boost, output);
            FileSize = new FileInfo(path).Length;
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    public static TimeSpan ReadDuration(string path)
    {
        using var reader = OpenReader(path);
        return reader.TotalTime;
    }

    public void Play()
    {
        if (_output is null || _reader is null) return;
        if (_reader.Position >= _reader.Length) _tempo!.Position = TimeSpan.Zero;
        _output.Play();
    }

    public void Pause() => _output?.Pause();

    public void Close()
    {
        _closing = true;
        try
        {
            _output?.Stop();
            _output?.Dispose();
            _reader?.Dispose();
        }
        finally
        {
            (_reader, _tempo, _boost, _output) = (null, null, null, null);
            FileSize = 0;
            _closing = false;
        }
    }

    public void Dispose() => Close();

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_closing || sender != _output) return;
        if (e.Exception is not null) Failed?.Invoke(this, e.Exception);
        else Ended?.Invoke(this, EventArgs.Empty);
    }

    private static WaveStream OpenReader(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".ogg" or ".oga")
        {
            try { return new VorbisWaveReader(path); }
            catch (Exception) { /* Might be Opus in an Ogg file; let Media Foundation try. */ }
        }
        try
        {
            return new MediaFoundationReader(path, new MediaFoundationReader.MediaFoundationReaderSettings { RequestFloatOutput = true });
        }
        catch (Exception) when (ext is ".m4b" or ".m4a" or ".mp4" or ".aac")
        {
            // Some Windows setups don't link .m4b to the MP4 decoder; open it as a stream so the content decides.
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try { return new OwnedStreamReader(stream); }
            catch { stream.Dispose(); throw; }
        }
    }

    /// <summary>A stream reader that closes its file when it is disposed.</summary>
    private sealed class OwnedStreamReader : StreamMediaFoundationReader
    {
        private readonly Stream _stream;

        public OwnedStreamReader(Stream stream) : base(stream) => _stream = stream;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _stream.Dispose();
        }
    }
}
