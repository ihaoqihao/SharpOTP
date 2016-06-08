using System.Security.Cryptography;

namespace SharpOTP.Remote
{
    /// <summary>
    /// Fowler-Noll-Vo hash, variant 1, 32-bit version.
    /// http://www.isthe.com/chongo/tech/comp/fnv/
    /// https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
    /// </summary>
    public class FNV1_32 : HashAlgorithm
    {
        private const uint FNV_prime = 16777619;
        private const uint OFFSET_BASIS = 2166136261;

        /// <summary>
        /// hash
        /// </summary>
        protected uint _hash = OFFSET_BASIS;

        /// <summary>
        /// new
        /// </summary>
        public FNV1_32()
        {
            base.HashSizeValue = 32;
        }
        /// <summary>
        /// init
        /// </summary>
        public override void Initialize()
        {
            this._hash = OFFSET_BASIS;
        }
        /// <summary>
        /// hashcore
        /// </summary>
        /// <param name="array"></param>
        /// <param name="ibStart"></param>
        /// <param name="cbSize"></param>
        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            int length = ibStart + cbSize;
            for (int i = ibStart; i < length; i++) this._hash = (this._hash * FNV_prime) ^ array[i];
        }
        /// <summary>
        /// hash final
        /// </summary>
        /// <returns></returns>
        protected override byte[] HashFinal()
        {
            return NetworkBitConverter.GetBytes(this._hash);
        }

        /// <summary>
        /// get hash code
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        static public uint GetHashCode(byte[] array)
        {
            return GetHashCode(array, 0, array.Length);
        }
        /// <summary>
        /// get hash code
        /// </summary>
        /// <param name="array"></param>
        /// <param name="start"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        static public uint GetHashCode(byte[] array, int start, int size)
        {
            var hash = OFFSET_BASIS;
            int length = start + size;
            for (int i = start; i < length; i++) hash = (hash * FNV_prime) ^ array[i];
            return hash;
        }
    }

    /// <summary>
    /// Modified Fowler-Noll-Vo hash, 32-bit version.
    /// http://home.comcast.net/~bretm/hash/6.html
    /// </summary>
    public class ModifiedFNV1_32 : FNV1_32
    {
        /// <summary>
        /// hashFinal.
        /// </summary>
        /// <returns></returns>
        protected override byte[] HashFinal()
        {
            base._hash += base._hash << 13;
            base._hash ^= base._hash >> 7;
            base._hash += base._hash << 3;
            base._hash ^= base._hash >> 17;
            base._hash += base._hash << 5;
            return NetworkBitConverter.GetBytes(base._hash);
        }

        /// <summary>
        /// get hash code
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        static public new uint GetHashCode(byte[] array)
        {
            return GetHashCode(array, 0, array.Length);
        }
        /// <summary>
        /// get hash code
        /// </summary>
        /// <param name="array"></param>
        /// <param name="start"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        static public new uint GetHashCode(byte[] array, int start, int size)
        {
            var hash = FNV1_32.GetHashCode(array, start, size);
            hash += hash << 13;
            hash ^= hash >> 7;
            hash += hash << 3;
            hash ^= hash >> 17;
            hash += hash << 5;
            return hash;
        }
    }
}