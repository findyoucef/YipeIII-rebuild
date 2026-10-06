namespace Yipe.Core;

/// <summary>The original Visual C++ generator; the complete state also serves as a resume seed.</summary>
public sealed class MsvcRandom(uint seed = 1)
{
    public uint State { get; private set; } = seed;

    public int Next()
    {
        State = unchecked(State * 0x343fdu + 0x269ec3u);
        return (int)((State >> 16) & 0x7fffu);
    }
}
