using NAudio.Wave;
using SoundTouch;

namespace AudiobookPlayer.Audio;

/// <summary>
/// Changes playback speed without changing pitch, using SoundTouch. Also owns seeking, so that
/// moving the reader and clearing SoundTouch's buffers happen together with the audio thread paused.
/// </summary>
internal sealed class TempoSampleProvider : ISampleProvider
{
    private readonly WaveStream _reader;
    private readonly ISampleProvider _source;
    private readonly SoundTouchProcessor _st = new();
    private readonly object _gate = new();
    private readonly int _channels;
    private readonly float[] _input;
    private double _tempo = 1;
    private bool _sourceEnded;

    public TempoSampleProvider(WaveStream reader)
    {
        _reader = reader;
        _source = reader.ToSampleProvider();
        _channels = _source.WaveFormat.Channels;
        _st.Channels = _channels;
        _st.SampleRate = _source.WaveFormat.SampleRate;
        _input = new float[_source.WaveFormat.SampleRate / 20 * _channels];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public double Tempo
    {
        get => _tempo;
        set
        {
            lock (_gate)
            {
                if (Math.Abs(value - _tempo) < 0.001) return;
                _tempo = value;
                _st.Tempo = value;
                _st.Clear();
                _sourceEnded = false;
            }
        }
    }

    public TimeSpan Position
    {
        get { lock (_gate) return _reader.CurrentTime; }
        set
        {
            lock (_gate)
            {
                var max = _reader.TotalTime;
                _reader.CurrentTime = value < TimeSpan.Zero ? TimeSpan.Zero : value > max ? max : value;
                _st.Clear();
                _sourceEnded = false;
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            if (Math.Abs(_tempo - 1) < 0.001) return _source.Read(buffer, offset, count);

            int wanted = count / _channels, produced = 0;
            while (produced < wanted)
            {
                var got = _st.ReceiveSamples(buffer.AsSpan(offset + produced * _channels, (wanted - produced) * _channels), wanted - produced);
                produced += got;
                if (produced >= wanted) break;
                if (got > 0) continue;
                if (_sourceEnded) break;

                int read = _source.Read(_input, 0, _input.Length);
                if (read == 0)
                {
                    _st.Flush();
                    _sourceEnded = true;
                    continue;
                }
                _st.PutSamples(_input.AsSpan(0, read), read / _channels);
            }
            return produced * _channels;
        }
    }
}
