using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using GaussianSplatting.Runtime;

/// <summary>
/// 런타임에서 PLY 파일을 로드하여 GaussianSplatAsset으로 변환하는 유틸리티
/// Aras-p의 UnityGaussianSplatting Editor 코드를 런타임용으로 수정
/// </summary>
public static class RuntimePLYLoader
{
    public enum ElementType
    {
        None,
        Float,
        Double,
        UChar
    }

    public struct InputSplatData
    {
        public Vector3 pos;
        public Vector3 nor;
        public Vector3 dc0;
        public Vector3 sh1, sh2, sh3, sh4, sh5, sh6, sh7, sh8, sh9, shA, shB, shC, shD, shE, shF;
        public float opacity;
        public Vector3 scale;
        public Quaternion rot;
    }

    public static int TypeToSize(ElementType t)
    {
        return t switch
        {
            ElementType.None => 0,
            ElementType.Float => 4,
            ElementType.Double => 8,
            ElementType.UChar => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
        };
    }

    private static string ReadLine(FileStream fs)
    {
        var byteBuffer = new List<byte>();
        while (true)
        {
            int b = fs.ReadByte();
            if (b == -1 || b == '\n')
                break;
            byteBuffer.Add((byte)b);
        }
        // if line had CRLF line endings, remove the CR part
        if (byteBuffer.Count > 0 && byteBuffer[byteBuffer.Count - 1] == '\r')
            byteBuffer.RemoveAt(byteBuffer.Count - 1);
        return Encoding.UTF8.GetString(byteBuffer.ToArray());
    }

    private static void ReadHeaderImpl(string filePath, out int vertexCount, out int vertexStride, out List<(string, ElementType)> attrs, FileStream fs)
    {
        // C# arrays and NativeArrays make it hard to have a "byte" array larger than 2GB :/
        if (fs.Length >= 2 * 1024 * 1024 * 1024L)
            throw new IOException($"PLY {filePath} read error: currently files larger than 2GB are not supported");

        // read header
        vertexCount = 0;
        vertexStride = 0;
        attrs = new List<(string, ElementType)>();
        const int kMaxHeaderLines = 9000;
        bool got_binary_le = false;
        for (int lineIdx = 0; lineIdx < kMaxHeaderLines; ++lineIdx)
        {
            var line = ReadLine(fs);
            if (line == "end_header" || line.Length == 0)
                break;
            var tokens = line.Split(' ');
            if (tokens.Length == 3 && tokens[0] == "format" && tokens[1] == "binary_little_endian" && tokens[2] == "1.0")
                got_binary_le = true;
            if (tokens.Length == 3 && tokens[0] == "element" && tokens[1] == "vertex")
                vertexCount = int.Parse(tokens[2]);
            if (tokens.Length == 3 && tokens[0] == "property")
            {
                ElementType type = tokens[1] switch
                {
                    "float" => ElementType.Float,
                    "double" => ElementType.Double,
                    "uchar" => ElementType.UChar,
                    _ => ElementType.None
                };
                vertexStride += TypeToSize(type);
                attrs.Add((tokens[2], type));
            }
        }

        if (!got_binary_le)
        {
            throw new IOException($"PLY {filePath} not supported: needs to be binary, little endian PLY format");
        }
    }

    public static unsafe void LoadPLY(string filePath, out int splatCount, out NativeArray<InputSplatData> splats)
    {
        Debug.Log($"[RuntimePLYLoader] Starting to load PLY file: {filePath}");

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"PLY file not found: {filePath}");
        }

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        ReadHeaderImpl(filePath, out splatCount, out var vertexStride, out var attributes, fs);

        Debug.Log($"[RuntimePLYLoader] PLY Header: {splatCount} splats, stride: {vertexStride} bytes");

        // Validate attributes
        string[] required = { "x", "y", "z", "f_dc_0", "f_dc_1", "f_dc_2", "opacity", "scale_0", "scale_1", "scale_2", "rot_0", "rot_1", "rot_2", "rot_3" };
        foreach (var req in required)
        {
            if (!attributes.Contains((req, ElementType.Float)))
            {
                throw new IOException($"PLY file missing required attribute: {req}");
            }
        }

        // Read raw vertex data
        NativeArray<byte> vertices = new NativeArray<byte>(splatCount * vertexStride, Allocator.TempJob);
        var readBytes = fs.Read(vertices);
        if (readBytes != vertices.Length)
        {
            vertices.Dispose();
            throw new IOException($"PLY {filePath} read error, expected {vertices.Length} data bytes got {readBytes}");
        }

        Debug.Log($"[RuntimePLYLoader] Read {readBytes} bytes of vertex data");

        // Convert to InputSplatData format
        splats = PLYDataToSplats(vertices, splatCount, vertexStride, attributes);

        vertices.Dispose();

        // Process the data
        ReorderSHs(splatCount, (float*)splats.GetUnsafePtr());
        LinearizeData(splats);

        Debug.Log($"[RuntimePLYLoader] PLY loading completed successfully");
    }

    private static unsafe NativeArray<InputSplatData> PLYDataToSplats(
        NativeArray<byte> input,
        int count,
        int stride,
        List<(string, ElementType)> attributes)
    {
        // Build attribute offset map
        NativeArray<int> fileAttrOffsets = new NativeArray<int>(attributes.Count, Allocator.Temp);
        int offset = 0;
        for (var ai = 0; ai < attributes.Count; ai++)
        {
            var attr = attributes[ai];
            fileAttrOffsets[ai] = offset;
            offset += TypeToSize(attr.Item2);
        }

        string[] splatAttributes =
        {
            "x", "y", "z",
            "nx", "ny", "nz",
            "f_dc_0", "f_dc_1", "f_dc_2",
            "f_rest_0", "f_rest_1", "f_rest_2", "f_rest_3", "f_rest_4", "f_rest_5", "f_rest_6", "f_rest_7", "f_rest_8",
            "f_rest_9", "f_rest_10", "f_rest_11", "f_rest_12", "f_rest_13", "f_rest_14",
            "f_rest_15", "f_rest_16", "f_rest_17", "f_rest_18", "f_rest_19", "f_rest_20",
            "f_rest_21", "f_rest_22", "f_rest_23", "f_rest_24", "f_rest_25", "f_rest_26",
            "f_rest_27", "f_rest_28", "f_rest_29", "f_rest_30", "f_rest_31", "f_rest_32",
            "f_rest_33", "f_rest_34", "f_rest_35", "f_rest_36", "f_rest_37", "f_rest_38",
            "f_rest_39", "f_rest_40", "f_rest_41", "f_rest_42", "f_rest_43", "f_rest_44",
            "opacity",
            "scale_0", "scale_1", "scale_2",
            "rot_0", "rot_1", "rot_2", "rot_3",
        };

        NativeArray<int> srcOffsets = new NativeArray<int>(splatAttributes.Length, Allocator.Temp);
        for (int ai = 0; ai < splatAttributes.Length; ai++)
        {
            int attrIndex = attributes.FindIndex(a => a.Item1 == splatAttributes[ai] && a.Item2 == ElementType.Float);
            int attrOffset = attrIndex >= 0 ? fileAttrOffsets[attrIndex] : -1;
            srcOffsets[ai] = attrOffset;
        }

        NativeArray<InputSplatData> dst = new NativeArray<InputSplatData>(count, Allocator.Persistent);
        ReorderPLYData(count, (byte*)input.GetUnsafeReadOnlyPtr(), stride, (byte*)dst.GetUnsafePtr(), UnsafeUtility.SizeOf<InputSplatData>(), (int*)srcOffsets.GetUnsafeReadOnlyPtr());

        fileAttrOffsets.Dispose();
        srcOffsets.Dispose();

        return dst;
    }

    private static unsafe void ReorderPLYData(int splatCount, byte* src, int srcStride, byte* dst, int dstStride, int* srcOffsets)
    {
        for (int i = 0; i < splatCount; i++)
        {
            for (int attr = 0; attr < dstStride / 4; attr++)
            {
                if (srcOffsets[attr] >= 0)
                    *(int*)(dst + attr * 4) = *(int*)(src + srcOffsets[attr]);
            }
            src += srcStride;
            dst += dstStride;
        }
    }

    private static unsafe void ReorderSHs(int splatCount, float* data)
    {
        int splatStride = UnsafeUtility.SizeOf<InputSplatData>() / 4;
        int shStartOffset = 9, shCount = 15;
        float* tmp = stackalloc float[shCount * 3];
        int idx = shStartOffset;
        for (int i = 0; i < splatCount; ++i)
        {
            for (int j = 0; j < shCount; ++j)
            {
                tmp[j * 3 + 0] = data[idx + j];
                tmp[j * 3 + 1] = data[idx + j + shCount];
                tmp[j * 3 + 2] = data[idx + j + shCount * 2];
            }

            for (int j = 0; j < shCount * 3; ++j)
            {
                data[idx + j] = tmp[j];
            }

            idx += splatStride;
        }
    }

    private static void LinearizeData(NativeArray<InputSplatData> splatData)
    {
        for (int index = 0; index < splatData.Length; index++)
        {
            var splat = splatData[index];

            // Normalize and pack rotation
            var q = splat.rot;
            var qq = GaussianUtils.NormalizeSwizzleRotation(new float4(q.x, q.y, q.z, q.w));
            qq = GaussianUtils.PackSmallest3Rotation(qq);
            splat.rot = new Quaternion(qq.x, qq.y, qq.z, qq.w);

            // Convert scale to linear
            splat.scale = GaussianUtils.LinearScale(splat.scale);

            // Convert SH0 to color
            splat.dc0 = GaussianUtils.SH0ToColor(splat.dc0);
            splat.opacity = GaussianUtils.Sigmoid(splat.opacity);

            splatData[index] = splat;
        }
    }
}
