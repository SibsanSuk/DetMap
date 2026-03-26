using System.Security.Cryptography;
using DetMap.Core;

namespace DetMap.Serialization;

/// <summary>
/// Computes a deterministic snapshot content hash that ignores the current tick.
/// If two snapshots produce the same hash, their serialized world/state content matches.
/// </summary>
public static class DetStateHash
{
    private const ushort MinSupportedVersion = 2;
    private const ushort MaxSupportedVersion = 5;

    public static byte[] Compute(DetSpatialDatabase database)
    {
        return Compute(database.ToBytes());
    }

    public static string ComputeHex(DetSpatialDatabase database)
        => ToLowerHex(Compute(database));

    public static string ComputeHex(byte[] snapshotBytes)
        => ToLowerHex(Compute(snapshotBytes));

    public static byte[] ComputeFrame(DetSpatialDatabase database)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(database.ToBytes());
    }

    public static string ComputeFrameHex(DetSpatialDatabase database)
        => ToLowerHex(ComputeFrame(database));

    private static byte[] Compute(byte[] snapshotBytes)
    {
        (int tickOffset, int stateEndOffset) = FindStateOffsets(snapshotBytes);
        byte[] bytes = new byte[stateEndOffset];
        Array.Copy(snapshotBytes, bytes, stateEndOffset);
        Array.Clear(bytes, tickOffset, 8);

        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(bytes);
    }

    private static (int TickOffset, int StateEndOffset) FindStateOffsets(byte[] snapshotBytes)
    {
        using var ms = new MemoryStream(snapshotBytes, writable: false);
        using var br = new BinaryReader(ms);

        byte[] magic = br.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != 'D' || magic[1] != 'M' || magic[2] != 'A' || magic[3] != 'P')
            throw new InvalidDataException("Not a DetMap save file.");

        ushort version = br.ReadUInt16();
        if (version < MinSupportedVersion || version > MaxSupportedVersion)
            throw new InvalidDataException($"Unsupported snapshot version: {version}.");

        int width = br.ReadInt32();
        int height = br.ReadInt32();
        int cellCount = checked(width * height);

        int layerCount = br.ReadInt32();
        var layerKinds = new byte[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            layerKinds[i] = br.ReadByte();
            br.ReadString();
        }

        int tableCount = br.ReadInt32();
        var tableColumns = new byte[tableCount][];
        for (int i = 0; i < tableCount; i++)
        {
            br.ReadString();
            int columnCount = br.ReadInt32();
            tableColumns[i] = new byte[columnCount];
            for (int j = 0; j < columnCount; j++)
            {
                tableColumns[i][j] = br.ReadByte();
                br.ReadString();
                if (version >= 3)
                {
                    br.ReadBoolean();
                    br.ReadBoolean();
                    br.ReadString();
                }
            }

            if (version >= 5)
            {
                int indexCount = br.ReadInt32();
                for (int j = 0; j < indexCount; j++)
                {
                    br.ReadByte();
                    br.ReadString();
                    br.ReadString();
                }
            }
        }

        int globalCount = br.ReadInt32();
        for (int i = 0; i < globalCount; i++)
            br.ReadString();

        int storeCount = br.ReadInt32();
        for (int i = 0; i < storeCount; i++)
            br.ReadString();

        int tickOffset = checked((int)ms.Position);
        br.ReadUInt64();

        foreach (byte kind in layerKinds)
            SkipLayerData(br, kind, cellCount);

        for (int i = 0; i < globalCount; i++)
            br.ReadInt64();

        foreach (byte[] columns in tableColumns)
            SkipTableData(br, columns);

        for (int i = 0; i < storeCount; i++)
            SkipPathStore(br);

        return (tickOffset, checked((int)ms.Position));
    }

    private static void SkipLayerData(BinaryReader br, byte kind, int cellCount)
    {
        switch (kind)
        {
            case 0:
            case 1:
            case 2:
                br.ReadBytes(br.ReadInt32());
                break;
            case 3:
                br.ReadBytes(checked(br.ReadInt32() * sizeof(ulong)));
                break;
            case 4:
                {
                    int headCount = br.ReadInt32();
                    br.ReadBytes(checked(headCount * sizeof(int) * 2));
                    int len = br.ReadInt32();
                    br.ReadBytes(checked(len * sizeof(int) * 2));
                    break;
                }
            case 5:
                {
                    int entryCount = br.ReadInt32();
                    for (int i = 0; i < entryCount; i++)
                    {
                        br.ReadInt32();
                        int tagCount = br.ReadInt32();
                        for (int j = 0; j < tagCount; j++)
                            br.ReadString();
                    }
                    break;
                }
            case 6:
                {
                    int len = br.ReadInt32();
                    br.ReadBytes(len);
                    br.ReadBytes(checked(len * sizeof(long)));
                    break;
                }
            default:
                throw new InvalidDataException($"Unsupported layer kind: {kind}.");
        }
    }

    private static void SkipTableData(BinaryReader br, byte[] columnKinds)
    {
        br.ReadInt32(); // highWater
        int freeCount = br.ReadInt32();
        br.ReadBytes(checked(freeCount * sizeof(int)));

        SkipColumnData(br, 0); // alive column
        foreach (byte kind in columnKinds)
            SkipColumnData(br, kind);
    }

    private static void SkipColumnData(BinaryReader br, byte kind)
    {
        int len = br.ReadInt32();
        switch (kind)
        {
            case 0:
                br.ReadBytes(len);
                break;
            case 1:
                br.ReadBytes(checked(len * sizeof(int)));
                break;
            case 2:
                br.ReadBytes(checked(len * sizeof(long)));
                break;
            case 3:
                for (int i = 0; i < len; i++)
                {
                    if (br.ReadBoolean())
                        br.ReadString();
                }
                break;
            default:
                throw new InvalidDataException($"Unsupported column kind: {kind}.");
        }
    }

    private static void SkipPathStore(BinaryReader br)
    {
        int slotCount = br.ReadInt32();
        for (int i = 0; i < slotCount; i++)
        {
            int pathLength = br.ReadInt32();
            if (pathLength > 0)
            {
                br.ReadInt32();
                br.ReadBytes(checked(pathLength * sizeof(int)));
            }
        }
    }

    private static string ToLowerHex(byte[] bytes)
    {
        char[] chars = new char[bytes.Length * 2];
        int index = 0;
        foreach (byte value in bytes)
        {
            chars[index++] = GetHexChar(value >> 4);
            chars[index++] = GetHexChar(value & 0xF);
        }

        return new string(chars);
    }

    private static char GetHexChar(int value)
        => (char)(value < 10 ? '0' + value : 'a' + (value - 10));
}
