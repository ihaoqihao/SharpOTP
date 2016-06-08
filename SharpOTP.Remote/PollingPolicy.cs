using System;
using System.Linq;
using System.Threading;

namespace SharpOTP.Remote
{
    /// <summary>
    /// 轮询消息分发策略
    /// </summary>
    public sealed class PollingPolicy : IMessageDispatchPolicy
    {
        private string[] _arrNodes = null;
        private int _seqId = -1;

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
            var i = (Interlocked.Increment(ref this._seqId) & int.MaxValue) % this._arrNodes.Length;
            return this._arrNodes[i];
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