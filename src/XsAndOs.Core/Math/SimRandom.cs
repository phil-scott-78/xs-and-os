namespace XsAndOs.Core;

/// <summary>
/// Deterministic PCG32 PRNG. System.Random's sequence is runtime-implementation-defined
/// (Unity Mono vs .NET differ), so the sim owns its RNG to guarantee identical rolls everywhere.
/// One instance per simulation; every stochastic decision goes through it.
/// </summary>
public sealed class SimRandom
{
    private ulong _state;
    private readonly ulong _inc;
    private float _spareGaussian;
    private bool _hasSpareGaussian;

    public SimRandom(int seed)
    {
        _inc = 1442695040888963407UL;
        _state = 0UL;
        NextUInt();
        _state += (ulong)seed;
        NextUInt();
    }

    private uint NextUInt()
    {
        var old = _state;
        _state = old * 6364136223846793005UL + _inc;
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << (-rot & 31));
    }

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

    /// <summary>Uniform float in [min, max).</summary>
    public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

    /// <summary>Uniform int in [min, max).</summary>
    public int NextInt(int min, int max) => min + (int)(NextUInt() % (uint)(max - min));

    /// <summary>True with probability <paramref name="p"/>.</summary>
    public bool Chance(float p) => NextFloat() < p;

    /// <summary>Gaussian sample via Box-Muller (polar form, spare cached).</summary>
    public float NextGaussian(float mean, float stdev)
    {
        if (_hasSpareGaussian)
        {
            _hasSpareGaussian = false;
            return mean + stdev * _spareGaussian;
        }

        float u, v, s;
        do
        {
            u = NextFloat() * 2f - 1f;
            v = NextFloat() * 2f - 1f;
            s = u * u + v * v;
        } while (s >= 1f || s < 1e-12f);

        var mul = (float)global::System.Math.Sqrt(-2f * global::System.Math.Log(s) / s);
        _spareGaussian = v * mul;
        _hasSpareGaussian = true;
        return mean + stdev * u * mul;
    }
}
