using System;
using System.Threading.Tasks;

namespace SharpOTP
{
    /// <summary>
    /// gen server
    /// </summary>
    static public class GenServer
    {
        /// <summary>
        /// start new actor
        /// </summary>
        /// <typeparam name="TServer"></typeparam>
        /// <param name="name"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="enableMonitoring"></param>
        /// <returns></returns>
        static public Actor Start<TServer>(string name = null,
            int maxDegreeOfParallelism = 1,
            bool enableMonitoring = false)
            where TServer : new()
        {
            return new Actor(name ?? typeof(TServer).ToString(),
                new TServer(),
                maxDegreeOfParallelism,
                enableMonitoring);
        }
        /// <summary>
        /// start new actor
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="enableMonitoring"></param>
        /// <returns></returns>
        static public Actor Start(object server,
            string name = null,
            int maxDegreeOfParallelism = 1,
            bool enableMonitoring = false)
        {
            if (server == null) throw new ArgumentNullException("server");
            return new Actor(name ?? server.GetType().ToString(),
                server,
                maxDegreeOfParallelism,
                enableMonitoring);
        }
        /// <summary>
        /// stop actor.
        /// </summary>
        /// <param name="actor"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">actor is null.</exception>
        static public Task Stop(Actor actor)
        {
            if (actor == null) throw new ArgumentNullException("actor");
            actor.Complete();
            return actor.Completion;
        }
    }
}