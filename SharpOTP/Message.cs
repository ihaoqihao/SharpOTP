using System;
using System.Threading.Tasks;

namespace SharpOTP
{
    /// <summary>
    /// message
    /// </summary>
    internal sealed class Message
    {
        /// <summary>
        /// task source.
        /// </summary>
        private readonly dynamic _taskSource = null;
        /// <summary>
        /// 参数
        /// </summary>
        public readonly dynamic Argument;
        /// <summary>
        /// 过期时间, defautl is DateTime.MaxValue
        /// </summary>
        public readonly DateTime ExpireTime = DateTime.MaxValue;

        /// <summary>
        /// new
        /// </summary>
        /// <param name="argument"></param>
        public Message(dynamic argument)
        {
            this.Argument = argument;
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="millisecondsTimeout"></param>
        public Message(dynamic argument, int millisecondsTimeout)
        {
            this.Argument = argument;
            this.ExpireTime = DateTimeSlim.UtcNow.AddMilliseconds(millisecondsTimeout);
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="taskSource"></param>
        public Message(dynamic argument, dynamic taskSource)
        {
            this.Argument = argument;
            this._taskSource = taskSource;
        }
        /// <summary>
        /// new
        /// </summary>
        /// <param name="argument"></param>
        /// <param name="taskSource"></param>
        /// <param name="millisecondsTimeout"></param>
        public Message(dynamic argument, dynamic taskSource, int millisecondsTimeout)
        {
            this.Argument = argument;
            this._taskSource = taskSource;
            this.ExpireTime = DateTimeSlim.UtcNow.AddMilliseconds(millisecondsTimeout);
        }

        /// <summary>
        /// callback
        /// </summary>
        /// <param name="task"></param>
        /// <exception cref="ArgumentNullException">task is null</exception>
        public void Callback(Task task)
        {
            if (this._taskSource == null) return;
            if (task == null) throw new ArgumentNullException("task");

            task.ContinueWith(c =>
            {
                if (c.IsFaulted) this._taskSource.TrySetException(c.Exception.InnerException);
                else this._taskSource.TrySetResult(((dynamic)task).Result);
            });
        }

        /// <summary>
        /// true is timeout
        /// </summary>
        /// <returns></returns>
        public bool IsTimeout()
        {
            return DateTimeSlim.UtcNow >= this.ExpireTime;
        }
    }
}