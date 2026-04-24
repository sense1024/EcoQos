using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace EfficiencyModeScanner;

/// <summary>
/// Uses GetLogicalProcessorInformationEx(RelationProcessorCore) EfficiencyClass to infer E-cores
/// (lowest efficiency class) and build an affinity mask.
/// </summary>
internal static class HybridCpuTopology
{
    private const int RelationProcessorCore = 0;

    /// <summary>Aggregates only processor group 0 mask for SetProcessAffinityMask.</summary>
    internal static bool TryGetEcoreAffinityMaskGroup0(out ulong mask, out string? errorMessage)
    {
        mask = 0;
        errorMessage = null;

        if (!TryGetLogicalProcessorCoreBuffer(out byte[] buffer))
        {
            errorMessage = $"Failed to read CPU topology (Win32 error: {Marshal.GetLastWin32Error()}).";
            return false;
        }

        int index = 0;
        byte minEfficiency = byte.MaxValue;
        var cores = new List<(byte Efficiency, ushort Group, ulong Affinity)>();

        while (index + 8 <= buffer.Length)
        {
            int relationship = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(index, 4));
            uint entrySize = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(index + 4, 4));
            if (entrySize < 8 || index + entrySize > buffer.Length)
                break;

            if (relationship == RelationProcessorCore)
            {
                int procOff = index + 8;
                if (procOff + 24 > buffer.Length)
                    break;

                byte efficiencyClass = buffer[procOff + 1];
                ushort groupCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(procOff + 22, 2));
                int groupAffinityOffset = procOff + 24;

                if (groupCount == 0 || groupAffinityOffset + 16 * groupCount > buffer.Length)
                {
                    index += (int)entrySize;
                    continue;
                }

                for (int g = 0; g < groupCount; g++)
                {
                    int ga = groupAffinityOffset + g * 16;
                    ulong affinity = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(ga, 8));
                    ushort group = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(ga + 8, 2));
                    if (affinity == 0)
                        continue;

                    cores.Add((efficiencyClass, group, affinity));
                    if (efficiencyClass < minEfficiency)
                        minEfficiency = efficiencyClass;
                }
            }

            index += (int)entrySize;
        }

        if (cores.Count == 0)
        {
            errorMessage = "No processor-core topology entries were returned.";
            return false;
        }

        if (minEfficiency == 0 && cores.TrueForAll(c => c.Efficiency == 0))
        {
            errorMessage = "This CPU reports no heterogeneous EfficiencyClass values (all zero); E-core affinity cannot be inferred automatically.";
            return false;
        }

        foreach (var core in cores)
        {
            if (core.Efficiency != minEfficiency)
                continue;

            if (core.Group != 0)
                continue;

            mask |= core.Affinity;
        }

        if (mask == 0)
        {
            errorMessage = "E-cores are not in processor group 0; SetProcessAffinityMask is only applied for group 0 in this tool.";
            return false;
        }

        return true;
    }

    private static bool TryGetLogicalProcessorCoreBuffer(out byte[] buffer)
    {
        buffer = Array.Empty<byte>();
        uint length = 0;
        _ = NativeMethods.GetLogicalProcessorInformationEx(RelationProcessorCore, nint.Zero, ref length);
        if (Marshal.GetLastWin32Error() != NativeMethods.ErrorInsufficientBuffer)
            return false;

        nint ptr = Marshal.AllocHGlobal((nint)length);
        try
        {
            if (!NativeMethods.GetLogicalProcessorInformationEx(RelationProcessorCore, ptr, ref length))
                return false;

            buffer = new byte[length];
            Marshal.Copy(ptr, buffer, 0, (int)length);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}
