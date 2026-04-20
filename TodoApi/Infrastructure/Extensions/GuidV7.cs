using System.Security.Cryptography;

namespace TodoApi.Infrastructure.Extensions;

public static class GuidV7
{
    public static Guid NewGuid()
    {
        Span<byte> bytes = stackalloc byte[16];

        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(unixMs >> 40);
        bytes[1] = (byte)(unixMs >> 32);
        bytes[2] = (byte)(unixMs >> 24);
        bytes[3] = (byte)(unixMs >> 16);
        bytes[4] = (byte)(unixMs >> 8);
        bytes[5] = (byte)unixMs;

        RandomNumberGenerator.Fill(bytes[6..]);

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70); // version 7
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant

        // Big-endian so the textual form starts with the timestamp (RFC 9562).
        return new Guid(bytes, bigEndian: true);
    }
}
