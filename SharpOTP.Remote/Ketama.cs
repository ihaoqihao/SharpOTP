using System;
using System.Security.Cryptography;
using System.Text;

namespace SharpOTP.Remote
{
    /// <summary>
    /// Ketama hash
    /// </summary>
    static public class Ketama
    {
        /// <summary>
        /// get hash code
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        static public uint GetHashCode(string key)
        {
            return GetHashCode(key, 0);
        }
        /// <summary>
        /// get hash code
        /// </summary>
        /// <param name="key"></param>
        /// <param name="nTime"></param>
        /// <returns></returns>
        static public uint GetHashCode(string key, int nTime)
        {
            if (key == null) throw new ArgumentNullException("key");
            if (nTime < 0 || nTime > 3) throw new ArgumentOutOfRangeException("nTime");

            byte[] hashBytes = null;
            using (var md5 = MD5CryptoServiceProvider.Create()) hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
            return (UInt32)ToInt32(hashBytes, nTime * 4);
        }
        /// <summary>
        /// to int32
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        static private unsafe int ToInt32(byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return (*pbyte) | (*(pbyte + 1) << 8) | (*(pbyte + 2) << 16) | (*(pbyte + 3) << 24);
            }
        }
    }
}