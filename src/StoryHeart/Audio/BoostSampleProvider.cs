using NAudio.Wave;

namespace StoryHeart.Audio;

/// <summary>
/// Volume from 0 to 300 percent. Above 100 percent a peak limiter keeps loud parts from clipping.
/// </summary>
internal sealed class BoostSampleProvider : ISampleProvider
{
    private const float Ceiling = 0.89f; // about -1 dB
    private readonly ISampleProvider _source;
    private readonly float _release;
    private float _envelope;

    public BoostSampleProvider(ISampleProvider source)
    {
        _source = source;
        // Envelope falls by about 60 dB over a quarter of a second.
        _release = (float)Math.Exp(Math.Log(0.001) / (0.25 * source.WaveFormat.SampleRate * source.WaveFormat.Channels));
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>1.0 is normal volume; up to 3.0.</summary>
    public float Gain { get; set; } = 1;

    public int Read(float[] buffer, int offset, int count)
    {
        int n = _source.Read(buffer, offset, count);
        float gain = Gain;
        if (gain == 1) return n;
        for (int i = offset; i < offset + n; i++)
        {
            float x = buffer[i];
            if (gain <= 1) { buffer[i] = x * gain; continue; }
            float a = Math.Abs(x);
            _envelope = a > _envelope ? a : _envelope * _release;
            float g = _envelope * gain > Ceiling ? Ceiling / _envelope : gain;
            buffer[i] = x * g;
        }
        return n;
    }
}
