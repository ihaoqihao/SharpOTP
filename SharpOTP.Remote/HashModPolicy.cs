using System;
using System.Linq;
using System.Text;

namespace SharpOTP.Remote
{
    /// <summary>
    /// hash取余消息分发策略
    /// </summary>
    public sealed class HashModPolicy : IMessageDispatchPolicy
    {
        private string[] _arrNodes = null;

        /// <summary>
        /// init
        /// </summary>
        /// <param name="arrNodes"></param>
        public void Init(string[] arrNodes)
        {
            if (arrNodes == null || arrNodes.Length == 0)
                throw new ArgumentNullException("arrNodes");

            this._arrNodes = arrNodes.Distinct().OrderBy(c => c).ToArray();
        }
        /// <summary>
        /// calc
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetNode(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            var hash = ModifiedFNV1_32.GetHashCode(Encoding.UTF8.GetBytes(key));
            return this._arrNodes[hash % this._arrNodes.Length];
        }
        /// <summary>
        /// get all nodes
        /// </summary>
        /// <returns></returns>
        public string[] GetAllNodes()
        {
            var arr = new string[this._arrNodes.Length];
            this._arrNodes.CopyTo(arr, 0);
            return arr;
        }
    }
}