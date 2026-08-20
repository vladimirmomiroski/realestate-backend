using System.Buffers;
using System.Text;

namespace RealEstate.Infrastructure.Geocoding.Geoapify;

internal static class GeoapifyText
{
    public static bool IsWellFormed(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        ReadOnlySpan<char> remaining = value.AsSpan();

        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(
                remaining,
                out _,
                out int charsConsumed);

            if (status != OperationStatus.Done)
            {
                return false;
            }

            remaining = remaining[charsConsumed..];
        }

        return true;
    }

    public static bool IsWithinScalarLimit(
        string value,
        int maximumScalars)
    {
        ArgumentNullException.ThrowIfNull(value);

        ReadOnlySpan<char> remaining = value.AsSpan();
        int scalarCount = 0;

        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(
                remaining,
                out _,
                out int charsConsumed);

            if (status != OperationStatus.Done || ++scalarCount > maximumScalars)
            {
                return false;
            }

            remaining = remaining[charsConsumed..];
        }

        return true;
    }
}
