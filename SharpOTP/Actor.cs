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
        /// block
        /// </summary>
        private readonly ActionBlock<Message> _block = null;
        /// <summary>
        /// internal server
        /// </summary>
        private readonly dynamic _server;
        /// <summary>
        /// actor name
        /// </summary>
        public readonly string Name;
        #endregion

        #region Event
        /// <summary>
        /// message offered event
        /// </summary>
        public event Action<Actor> Offered;
        /// <summary>
        /// message processed
        /// </summary>
        public event Action<Actor, DateTime> Processed;
        /// <summary>
        /// message process error
        /// </summary>
        public event Action<Actor, Exception> ProcessError;
        #endregion

        #region Constructors
        /// <summary>
        /// new
        /// </summary>
        /// <param name="server"></param>
        /// <param name="name"></param>
        /// <param name="maxDegreeOfParallelism"></param>
        internal Actor(object server, string name, int maxDegreeOfParallelism)
        {
            this._server = server;
            this.Name = name ?? server.GetType().ToString();

            this._block = new ActionBlock<Message>(message =>
            {
                var startTime = DateTimeSlim.UtcNow;

                Task task = null;
                try { task = this._server.HandleCall(message.Argument) as Task; }
                catch (Exception ex) { task = Task.Factory.StartNew(() => { throw ex; }); }
                if (task == null) task = Task.Factory.StartNew(() => { throw new ArgumentNullException("HandleCall.Result"); });

                message.Callback(task);
                return task.ContinueWith(c =>
                {
                    if (this.Processed != null) this.Processed(this, startTime);
                    if (c.IsFaulted)
                    {
                        if (this.ProcessError != null) this.ProcessError(this, c.Exception.InnerException);
                        Trace.TraceError(c.Exception.ToString());
                    }
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
                if (this.Offered != null) this.Offered(this);
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
                if (this.Offered != null) this.Offered(this);
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
    }
}