using System.Configuration;

namespace SharpOTP.Remote.Config
{
    /// <summary>
    /// otp section
    /// </summary>
    public sealed class OTPSection : ConfigurationSection
    {
        /// <summary>
        /// node name
        /// </summary>
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return (string)this["name"]; }
        }
        /// <summary>
        /// current node name
        /// </summary>
        [ConfigurationProperty("currNode")]
        public string CurrNode
        {
            get { return (string)this["currNode"]; }
        }
        /// <summary>
        /// true表示开启监控
        /// </summary>
        [ConfigurationProperty("enableMonitoring", DefaultValue = false)]
        public bool EnableMonitoring
        {
            get { return (bool)this["enableMonitoring"]; }
        }
        /// <summary>
        /// cluster config
        /// </summary>
        [ConfigurationProperty("cluster", IsRequired = true)]
        public Cluster Cluster
        {
            get { return this["cluster"] as Cluster; }
        }
        /// <summary>
        /// rabbitMQ config
        /// </summary>
        [ConfigurationProperty("rabbitMQ", IsRequired = true)]
        public RabbitMQ RabbitMQ
        {
            get { return this["rabbitMQ"] as RabbitMQ; }
        }
    }
}