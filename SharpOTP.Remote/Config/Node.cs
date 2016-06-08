using System.Configuration;

namespace SharpOTP.Remote.Config
{
    /// <summary>
    /// node
    /// </summary>
    public sealed class Node : ConfigurationElement
    {
        /// <summary>
        /// node name
        /// </summary>
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return (string)this["name"]; }
        }
    }
}