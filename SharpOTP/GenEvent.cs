using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SharpOTP
{
    /// <summary>
    /// gen event
    /// </summary>
    static public class GenEvent
    {
        #region Private Members
        /// <summary>
        /// key:event name.
        /// </summary>
        static private readonly ConcurrentDictionary<string, Lazy<Actor>> _dicActor =
            new ConcurrentDictionary<string, Lazy<Actor>>();
        #endregion

        #region Constructors
        /// <summary>
        /// static new
        /// </summary>
        static GenEvent()
        {
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// start
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">name is null or empty.</exception>
        static public Actor Start(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            return _dicActor.GetOrAdd(name, _ => new Lazy<Actor>(() => GenServer.Start<EventServer>(), true)).Value;
        }
        /// <summary>
        /// stop
        /// </summary>
        /// <param name="name"></param>
        /// <exception cref="ArgumentNullException">name is null or empty.</exception>
        static public Actor Stop(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

            Lazy<Actor> removed = null;
            if (!_dicActor.TryRemove(name, out removed)) return null;

            GenServer.Stop(removed.Value);
            return removed.Value;
        }

        /// <summary>
        /// add handler
        /// </summary>
        /// <param name="name"></param>
        /// <param name="eventHandler"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">name is null or empty.</exception>
        /// <exception cref="ArgumentNullException">eventHandler is null.</exception>
        static public bool AddHandler(string name, dynamic eventHandler)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (eventHandler == null) throw new ArgumentNullException("eventHandler");

            Lazy<Actor> exists = null;
            if (!_dicActor.TryGetValue(name, out exists)) return false;
            exists.Value.Call(new AddHandlerMessage { Handler = eventHandler });
            return true;
        }
        /// <summary>
        /// remove handler
        /// </summary>
        /// <param name="name"></param>
        /// <param name="eventHandler"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">name is null or empty.</exception>
        /// <exception cref="ArgumentNullException">eventHandler is null.</exception>
        static public bool DeleteHandler(string name, dynamic eventHandler)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (eventHandler == null) throw new ArgumentNullException("eventHandler");

            Lazy<Actor> exists = null;
            if (!_dicActor.TryGetValue(name, out exists)) return false;
            exists.Value.Call(new DeleteHandlerMessage { Handler = eventHandler });
            return true;
        }
        #endregion

        #region Message
        /// <summary>
        /// add event handler message
        /// </summary>
        internal sealed class AddHandlerMessage
        {
            /// <summary>
            /// handler
            /// </summary>
            public dynamic Handler { get; set; }
        }
        /// <summary>
        /// delete event handler message
        /// </summary>
        internal sealed class DeleteHandlerMessage
        {
            /// <summary>
            /// handler
            /// </summary>
            public dynamic Handler { get; set; }
        }
        #endregion

        /// <summary>
        /// event server
        /// </summary>
        internal sealed class EventServer
        {
            #region Private Members
            private readonly List<dynamic> _list = new List<dynamic>();
            #endregion

            #region Public Methods
            /// <summary>
            /// handle call
            /// </summary>
            /// <param name="eventArgs"></param>
            public void HandleCall(dynamic eventArgs)
            {
                for (int i = 0, l = this._list.Count; i < l; i++)
                    this._list[i].HandleEvent(eventArgs);
            }
            /// <summary>
            /// handle call
            /// </summary>
            /// <param name="message"></param>
            public void HandleCall(AddHandlerMessage message)
            {
                this._list.Add(message.Handler);
            }
            /// <summary>
            /// handle call
            /// </summary>
            /// <param name="message"></param>
            public void HandleCall(DeleteHandlerMessage message)
            {
                this._list.Remove(message.Handler);
            }
            #endregion
        }
    }
}