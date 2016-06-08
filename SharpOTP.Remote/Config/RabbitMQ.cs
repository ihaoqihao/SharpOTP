using System.Configuration;

namespace SharpOTP.Remote.Config
{
    /// <summary>
    /// rabbitMQ setting
    /// </summary>
    public sealed class RabbitMQ : ConfigurationElement
    {
        /// <summary>
        /// host
        /// </summary>
        [ConfigurationProperty("host", IsRequired = true)]
        public string Host
        {
            get { return (string)this["host"]; }
        }
        /// <summary>
        /// port
        /// </summary>
        [ConfigurationProperty("port", IsRequired = true)]
        public int Port
        {
            get { return (int)this["port"]; }
        }
        /// <summary>
        /// userName
        /// </summary>
        [ConfigurationProperty("userName", IsRequired = true)]
        public string UserName
        {
            get { return (string)this["userName"]; }
        }
        /// <summary>
        /// password
        /// </summary>
        [ConfigurationProperty("password", IsRequired = true)]
        public string Password
        {
            get { return (string)this["password"]; }
        }
        /// <summary>
        /// vhost
        /// </summary>
        [ConfigurationProperty("vhost", IsRequired = true)]
        public string VHost
        {
            get { return (string)this["vhost"]; }
        }
        /// <summary>
        /// exchange
        /// </summary>
        [ConfigurationProperty("exchange", IsRequired = true)]
        public string Exchange
        {
            get { return (string)this["exchange"]; }
        }
    }
}