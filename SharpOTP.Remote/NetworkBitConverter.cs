
namespace SharpOTP.Remote
{
    /// <summary>
    /// network bit converter.
    /// </summary>
    static public class NetworkBitConverter
    {
        /// <summary>
        /// 以网络字节数组的形式返回指定的 16 位无符号整数值。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public byte[] GetBytes(ushort value)
        {
            return GetBytes((short)value);
        }
        /// <summary>
        /// 以网络字节数组的形式返回指定的 16 位有符号整数值。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public byte[] GetBytes(short value)
        {
            var bytes = new byte[2];
            bytes[0] = (byte)(0xff & (value >> 8));
            bytes[1] = (byte)(0xff & value);
            return bytes;
        }

        /// <summary>
        /// 以网络字节数组的形式返回指定的 32 位无符号整数值。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public byte[] GetBytes(uint value)
        {
            return GetBytes((int)value);
        }
        /// <summary>
        /// 以网络字节数组的形式返回指定的 32 位有符号整数值。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public byte[] GetBytes(int value)
        {
            var bytes = new byte[4];
            bytes[0] = (byte)(0xff & (value >> 24));
            bytes[1] = (byte)(0xff & (value >> 16));
            bytes[2] = (byte)(0xff & (value >> 8));
            bytes[3] = (byte)(0xff & value);
            return bytes;
        }

        /// <summary>
        /// 以网络字节数组的形式返回指定的 64 位无符号整数值。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public byte[] GetBytes(ulong value)
        {
            return GetBytes((long)value);
        }
        /// <summary>
        /// 以网络字节数组的形式返回指定的 64 位有符号整数值。
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        static public byte[] GetBytes(long value)
        {
            var bytes = new byte[8];
            bytes[0] = (byte)(0xff & (value >> 56));
            bytes[1] = (byte)(0xff & (value >> 48));
            bytes[2] = (byte)(0xff & (value >> 40));
            bytes[3] = (byte)(0xff & (value >> 32));
            bytes[4] = (byte)(0xff & (value >> 24));
            bytes[5] = (byte)(0xff & (value >> 16));
            bytes[6] = (byte)(0xff & (value >> 8));
            bytes[7] = (byte)(0xff & value);
            return bytes;
        }

        /// <summary>
        /// 返回由网络字节数组中指定位置的两个字节转换来的 16 位无符号整数。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        static public ushort ToUInt16(byte[] value, int startIndex)
        {
            return (ushort)ToInt16(value, startIndex);
        }
        /// <summary>
        /// 返回由网络字节数组中指定位置的两个字节转换来的 16 位有符号整数。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        static public unsafe short ToInt16(byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return (short)((*pbyte << 8) | (*(pbyte + 1)));
            }
        }

        /// <summary>
        /// 返回由网络字节数组中指定位置的四个字节转换来的 32 位无符号整数。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        static public uint ToUInt32(byte[] value, int startIndex)
        {
            return (uint)ToInt32(value, startIndex);
        }
        /// <summary>
        /// 返回由网络字节数组中指定位置的四个字节转换来的 32 有无符号整数。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        static public unsafe int ToInt32(byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return (*pbyte << 24) | (*(pbyte + 1) << 16) | (*(pbyte + 2) << 8) | (*(pbyte + 3));
            }
        }

        /// <summary>
        /// 返回由网络字节数组中指定位置的八个字节转换来的 64 位无符号整数。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        static public ulong ToUInt64(byte[] value, int startIndex)
        {
            return (ulong)ToInt64(value, startIndex);
        }
        /// <summary>
        /// 返回由网络字节数组中指定位置的八个字节转换来的 64 位有符号整数。
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        static public unsafe long ToInt64(byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                int i1 = (*pbyte << 24) | (*(pbyte + 1) << 16) | (*(pbyte + 2) << 8) | (*(pbyte + 3));
                int i2 = (*(pbyte + 4) << 24) | (*(pbyte + 5) << 16) | (*(pbyte + 6) << 8) | (*(pbyte + 7));
                return (uint)i2 | ((long)i1 << 32);
            }
        }

        ///// <summary>
        ///// 返回由网络字节数组中指定位置的两个字节转换来的 16 位有符号整数。
        ///// </summary>
        ///// <param name="value"></param>
        ///// <param name="startIndex"></param>
        ///// <returns></returns>
        //static public short ToInt16(byte[] value, int startIndex)
        //{
        //    return (short)(((value[startIndex] & 0xff) << 8) |
        //                   ((value[startIndex + 1] & 0xff)));
        //}
        ///// <summary>
        ///// 返回由网络字节数组中指定位置的四个字节转换来的 32 位有符号整数。
        ///// </summary>
        ///// <param name="value"></param>
        ///// <param name="startIndex"></param>
        ///// <returns></returns>
        //static public int ToInt32(byte[] value, int startIndex)
        //{
        //    return (int)(((value[startIndex] & 0xff) << 24) |
        //                 ((value[startIndex + 1] & 0xff) << 16) |
        //                 ((value[startIndex + 2] & 0xff) << 8) |
        //                 ((value[startIndex + 3] & 0xff)));
        //}
        ///// <summary>
        ///// 返回由网络字节数组中指定位置的八个字节转换来的 64 位有符号整数。
        ///// </summary>
        ///// <param name="value"></param>
        ///// <param name="startIndex"></param>
        ///// <returns></returns>
        //static public long ToInt64(byte[] value, int startIndex)
        //{
        //    return (long)(((long)(value[startIndex] & 0xff) << 56) |
        //                  ((long)(value[startIndex + 1] & 0xff) << 48) |
        //                  ((long)(value[startIndex + 2] & 0xff) << 40) |
        //                  ((long)(value[startIndex + 3] & 0xff) << 32) |
        //                  ((long)(value[startIndex + 4] & 0xff) << 24) |
        //                  ((long)(value[startIndex + 5] & 0xff) << 16) |
        //                  ((long)(value[startIndex + 6] & 0xff) << 8) |
        //                  ((long)(value[startIndex + 7] & 0xff)));
        //}
    }
}