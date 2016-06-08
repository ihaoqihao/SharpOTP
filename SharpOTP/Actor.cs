using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpOTP
{
    /// <summary>
    /// actor
    /// </summary>
    public sealed class Actor
    {
        #region Members
        /// <summary>
        /// actor name
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// block
        /// </summary>
        private readonly ActionBlock<Message> _block = null;
        /// <summary>
        /// internal server
        /// </summary>
        private readonly dynamic _server;
        /// <summary>
        /// performance counter
        /// </summary>
        private readonly Counter _counter = null;
        #endregion

        #region Constructors
        /// <summary>
        /// new
        /// </summary>
        /// <param name="name"></param>
        /// <param name="server"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <param name="enableMonitoring"></param>
        internal Actor(string name, object server, int maxDegreeOfParallelism, bool enableMonitoring)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (server == null) throw new ArgumentNullException("server");

            this.Name = name;
            this._server = server;
            if (enableMonitoring) this._counter = new Counter(name);

            this._block = new ActionBlock<Message>(message =>
            {
                Stopwatch sw = null;
                if (this._counter != null) sw = Stopwatch.StartNew();

                Task t = null;
                try { t = this._server.HandleCall(message.Argument) as Task; }
                catch (Exception ex) { t = FromException(ex); }
                if (t == null) t = FromException(new InvalidOperationException("actor.HandleCall"));

                message.Callback(t);
                return t.ContinueWith(c =>
                {
                    if (c.IsFaulted)
                    {
                        if (this._counter != null) this._counter.Fail();
                        Trace.TraceError(c.Exception.ToString());
                        return;
                    }

                    if (this._counter != null) this._counter.Complete((int)sw.ElapsedMilliseconds);
                });
            }, new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism });
        }
        #endregion

        #region Public Members
        /// <summary>
        /// Gets the number of input items waiting to be processed by this actor.
        /// </summary>
        public int InputCount
        {
            get { return this._block.InputCount; }
        }
        /// <summary>
        /// call
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        public bool Call(dynamic argument)
        {
            if (this._block.Post(new Message(argument)))
            {
                if (this._counter != null) this._counter.Post();
                return true;
            }
            return false;
        }
        /// <summary>
        /// call
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="argument"></param>
        /// <returns></returns>
        public Task<TResult> Call<TResult>(dynamic argument)
        {
            var source = new TaskCompletionSource<TResult>();
            if (this._block.Post(new Message(argument, source)))
            {
                if (this._counter != null) this._counter.Post();
                return source.Task;
            }
            return null;
        }
        #endregion

        #region Internal Members
        /// <summary>
        /// Gets a System.Threading.Tasks.Task object that represents the asynchronous 
        /// operation and completion of the dataflow block.
        /// </summary>
        internal Task Completion
        {
            get { return this._block.Completion; }
        }
        /// <summary>
        /// Signals to the dataflow block that it shouldn't accept or produce any more 
        /// messages and shouldn't consume any more postponed messages.
        /// </summary>
        internal void Complete()
        {
            this._block.Complete();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// from exception
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        static private Task FromException(Exception ex)
        {
            return Task.Run(() => { throw ex; });
        }
        #endregion
    }
}