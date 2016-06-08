using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpOTP.Remote
{
    /// <summary>
    /// 一致性哈希消息发分策略, 基于Ketama hash
    /// </summary>
    public sealed class KetamaConsistentHashPolicy : IMessageDispatchPolicy
    {
        #region Private Members
        private readonly Dictionary<uint, string> _dic =
            new Dictionary<uint, string>();
        private uint[] _keys = null;
        private string[] _arrNodes = null;
        #endregion

        #region IMessageDispatchPolicy Members
        /// <summary>
        /// init
        /// </summary>
        /// <param name="arrNodes"></param>
        public void Init(string[] arrNodes)
        {
            if (arrNodes == null || arrNodes.Length == 0)
                throw new ArgumentNullException("arrNodes");

            this._arrNodes = arrNodes.Distinct().ToArray();
            this._dic.Clear();
            this._keys = null;

            foreach (var node in arrNodes)
            {
                for (int i = 0; i < 250; i++)
                {
                    var hashCode = Ketama.GetHashCode(string.Concat(node, "-", i.ToString()));
                    this._dic[hashCode] = node;
                }
            }

            var listKeys = this._dic.Keys.ToList();
            listKeys.Sort();
            this._keys = listKeys.ToArray();
        }
        /// <summary>
        /// calc
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetNode(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            var i = Array.BinarySearch(this._keys, Ketama.GetHashCode(key));
            if (i < 0)
            {
                i = ~i;
                if (i >= this._keys.Length) i = 0;
            }
            return this._dic[this._keys[i]];
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
        #endregion
    }
}