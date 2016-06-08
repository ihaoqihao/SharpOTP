using System.Configuration;

namespace SharpOTP.Remote.Config
{
    /// <summary>
    /// 集群配置
    /// </summary>
    public sealed class Cluster : ConfigurationElement
    {
        /// <summary>
        /// cookie
        /// </summary>
        [ConfigurationProperty("cookie", IsRequired = true)]
        public string Cookie
        {
            get { return (string)this["cookie"]; }
        }
        /// <summary>
        /// dispatchPolicy, polling|hashMod|consistentHash, 默认为hashMod
        /// </summary>
        [ConfigurationProperty("dispatchPolicy", DefaultValue = "hashMod")]
        public string DispatchPolicy
        {
            get { return (string)this["dispatchPolicy"]; }
        }
        /// <summary>
        /// timeout, ms
        /// </summary>
        [ConfigurationProperty("remoteTimeout", DefaultValue = 10000)]
        public int RemoteTimeout
        {
            get { return (int)this["remoteTimeout"]; }
        }
        /// <summary>
        /// node集合
        /// </summary>
        [ConfigurationProperty("nodes", IsRequired = true)]
        public NodeCollection Nodes
        {
            get { return this["nodes"] as NodeCollection; }
        }
    }
}