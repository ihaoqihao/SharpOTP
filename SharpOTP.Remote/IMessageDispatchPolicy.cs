
namespace SharpOTP.Remote
{
    /// <summary>
    /// message dispatch policy
    /// </summary>
    public interface IMessageDispatchPolicy
    {
        /// <summary>
        /// init
        /// </summary>
        /// <param name="arrNodes"></param>
        void Init(string[] arrNodes);
        /// <summary>
        /// calc key dispatch to node
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string GetNode(string key);
        /// <summary>
        /// get all nodes
        /// </summary>
        /// <returns></returns>
        string[] GetAllNodes();
    }
}