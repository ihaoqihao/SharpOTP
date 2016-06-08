using System.Configuration;

namespace SharpOTP.Remote.Config
{
    /// <summary>
    /// node集合
    /// </summary>
    [ConfigurationCollection(typeof(Node), AddItemName = "node")]
    public class NodeCollection : ConfigurationElementCollection
    {
        /// <summary>
        /// 创建新元素
        /// </summary>
        /// <returns></returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new Node();
        }
        /// <summary>
        /// 获取指定元素的Key。
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            var node = element as Node;
            return node.Name;
        }
        /// <summary>
        /// 获取指定位置的对象。
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public Node this[int i]
        {
            get { return BaseGet(i) as Node; }
        }
        /// <summary>
        /// to array
        /// </summary>
        /// <returns></returns>
        public Node[] ToArray()
        {
            var arr = new Node[this.Count];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = this[i];

            return arr;
        }
    }
}