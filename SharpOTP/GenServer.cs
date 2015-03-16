using System;

namespace SharpOTP
{
    /// <summary>
    /// gen server
    /// </summary>
    static public class GenServer
    {
        /// <summary>
        /// start new
        /// </summary>
        /// <typeparam name="TServer"></typeparam>
        /// <param name="name"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="enableMonitoring"></param>
        /// <returns></returns>
        static public Actor Start<TServer>(string name = null,
            int maxDegreeOfParallelism = 1,
            bool enableMonitoring = false) where TServer : new()
        {
            var actor = new Actor(new TServer(), name, maxDegreeOfParallelism);
            if (enableMonitoring) Monitor.Attach(actor);
            return actor;
        }
        /// <summary>
        /// start new
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="enableMonitoring"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">server is null</exception>
        static public Actor Start(object server,
            string name = null,
            int maxDegreeOfParallelism = 1,
            bool enableMonitoring = false)
        {
            if (server == null) throw new ArgumentNullException("server");

            var actor = new Actor(server, name, maxDegreeOfParallelism);
            if (enableMonitoring) Monitor.Attach(actor);
            return actor;
        }
        /// <summary>
        /// stop actor.
        /// </summary>
        /// <param name="actor"></param>
        /// <exception cref="ArgumentNullException">actor is null.</exception>
        static public void Stop(Actor actor)
        {
            if (actor == null) throw new ArgumentNullException("actor");
            actor.Complete();
        }
    }
}