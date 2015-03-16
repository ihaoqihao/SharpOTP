using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpOTP
{
    /// <summary>
    /// monitor
    /// </summary>
    public class Monitor
    {
        #region Private Members
        /// <summary>
        /// gen server
        /// </summary>
        static private readonly Actor _server = null;
        /// <summary>
        /// actor list
        /// </summary>
        static private readonly List<Actor> _listTargets =
            new List<Actor>();
        /// <summary>
        /// count tuple
        /// </summary>
        static private readonly Dictionary<string, CountTuple> _dicCountTuple =
            new Dictionary<string, CountTuple>();
        /// <summary>
        /// timer
        /// </summary>
        static private readonly Timer _timer = null;
        #endregion

        #region Constructors
        /// <summary>
        /// new
        /// </summary>
        static Monitor()
        {
            _server = new Actor(new Monitor(), "otp.monintor", 1);
            _timer = new Timer(_ => _server.Call(new TimeMessage()), null, 1000 * 5, 1000 * 5);
        }
        #endregion

        #region Actor Callback
        /// <summary>
        /// attach
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task HandleCall(AttachMessage message)
        {
            _listTargets.Add(message.Actor);
        }
        /// <summary>
        /// detach
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task HandleCall(DetachMessage message)
        {
            _listTargets.Remove(message.Actor);
        }
        /// <summary>
        /// message offered
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task HandleCall(OfferedMessage message)
        {
            GetOrAdd(message.Actor.Name).Offered(message.CurrTime);
        }
        /// <summary>
        /// message processed
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task HandleCall(ProcessedMessage message)
        {
            GetOrAdd(message.Actor.Name).Processed(message.StartTime, message.EndTime);
        }
        /// <summary>
        /// message process error
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task HandleCall(ProcessErrorMessage message)
        {
            GetOrAdd(message.Actor.Name).Error(message.CurrTime);
        }
        /// <summary>
        /// time
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task HandleCall(TimeMessage message)
        {
            var minutes = (int)TimeSpan.FromTicks(DateTimeSlim.UtcNow.Ticks).TotalMinutes;
            foreach (var actor in _listTargets)
            {
                var input = actor.InputCount;
                if (input == 0) continue;
                GetOrAdd(actor.Name).Wait(minutes, actor.InputCount);
            }

            if (_dicCountTuple.Count == 0) return;

            foreach (var t in _dicCountTuple.Values)
                Console.WriteLine(t.ToString());

            Console.WriteLine(Environment.NewLine);
            _dicCountTuple.Clear();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// get or add
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        static private CountTuple GetOrAdd(string name)
        {
            CountTuple tuple;
            if (!_dicCountTuple.TryGetValue(name, out tuple))
                _dicCountTuple[name] = tuple = new CountTuple(name);

            return tuple;
        }
        /// <summary>
        /// actor message offered
        /// </summary>
        /// <param name="actor"></param>
        static private void OnMessageOffered(Actor actor)
        {
            _server.Call(new OfferedMessage(actor));
        }
        /// <summary>
        /// actor message processed
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="startTime"></param>
        static private void OnMessageProcessed(Actor actor, DateTime startTime)
        {
            _server.Call(new ProcessedMessage(actor, startTime, DateTimeSlim.UtcNow));
        }
        /// <summary>
        /// actor message process error
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="ex"></param>
        static private void OnMessageProcessError(Actor actor, Exception ex)
        {
            _server.Call(new ProcessErrorMessage(actor, ex));
        }
        #endregion

        #region API
        /// <summary>
        /// attach
        /// </summary>
        /// <param name="actor"></param>
        static public void Attach(Actor actor)
        {
            actor.Offered += OnMessageOffered;
            actor.Processed += OnMessageProcessed;
            actor.ProcessError += OnMessageProcessError;
            actor.Completion.ContinueWith(_ =>
            {
                actor.Offered -= OnMessageOffered;
                actor.Processed -= OnMessageProcessed;
                actor.ProcessError -= OnMessageProcessError;
                _server.Call(new DetachMessage(actor));
            });

            _server.Call(new AttachMessage(actor));
        }
        #endregion

        #region Actor Message
        /// <summary>
        /// time message
        /// </summary>
        public sealed class TimeMessage
        {
        }
        /// <summary>
        /// attach message
        /// </summary>
        public sealed class AttachMessage
        {
            /// <summary>
            /// actor
            /// </summary>
            public readonly Actor Actor;

            /// <summary>
            /// new
            /// </summary>
            /// <param name="actor"></param>
            /// <exception cref="ArgumentNullException">actor is null</exception>
            public AttachMessage(Actor actor)
            {
                if (actor == null) throw new ArgumentNullException("actor");
                this.Actor = actor;
            }
        }
        /// <summary>
        /// detach message
        /// </summary>
        public sealed class DetachMessage
        {
            /// <summary>
            /// actor
            /// </summary>
            public readonly Actor Actor;

            /// <summary>
            /// new
            /// </summary>
            /// <param name="actor"></param>
            /// <exception cref="ArgumentNullException">actor is null</exception>
            public DetachMessage(Actor actor)
            {
                if (actor == null) throw new ArgumentNullException("actor");
                this.Actor = actor;
            }
        }
        /// <summary>
        /// offered message
        /// </summary>
        public sealed class OfferedMessage
        {
            /// <summary>
            /// actor
            /// </summary>
            public readonly Actor Actor;
            /// <summary>
            /// current time
            /// </summary>
            public readonly DateTime CurrTime = DateTimeSlim.UtcNow;

            /// <summary>
            /// new
            /// </summary>
            /// <param name="actor"></param>
            /// <exception cref="ArgumentNullException">actor is null</exception>
            public OfferedMessage(Actor actor)
            {
                if (actor == null) throw new ArgumentNullException("actor");
                this.Actor = actor;
            }
        }
        /// <summary>
        /// processed message
        /// </summary>
        public sealed class ProcessedMessage
        {
            /// <summary>
            /// actor
            /// </summary>
            public readonly Actor Actor;
            /// <summary>
            /// start time
            /// </summary>
            public readonly DateTime StartTime;
            /// <summary>
            /// end time
            /// </summary>
            public readonly DateTime EndTime;

            /// <summary>
            /// new
            /// </summary>
            /// <param name="actor"></param>
            /// <param name="startTime"></param>
            /// <param name="endTime"></param>
            /// <exception cref="ArgumentNullException">actor is null</exception>
            public ProcessedMessage(Actor actor, DateTime startTime, DateTime endTime)
            {
                if (actor == null) throw new ArgumentNullException("actor");
                this.Actor = actor;
                this.StartTime = startTime;
                this.EndTime = endTime;
            }
        }
        /// <summary>
        /// actor process error message
        /// </summary>
        public sealed class ProcessErrorMessage
        {
            /// <summary>
            /// actor
            /// </summary>
            public readonly Actor Actor;
            /// <summary>
            /// ex
            /// </summary>
            public readonly Exception Ex;
            /// <summary>
            /// current time
            /// </summary>
            public readonly DateTime CurrTime = DateTimeSlim.UtcNow;

            /// <summary>
            /// new
            /// </summary>
            /// <param name="actor"></param>
            /// <param name="ex"></param>
            public ProcessErrorMessage(Actor actor, Exception ex)
            {
                if (actor == null) throw new ArgumentNullException("actor");
                this.Actor = actor;
                this.Ex = ex;
            }
        }
        #endregion

        /// <summary>
        /// count tuple
        /// </summary>
        public sealed class CountTuple
        {
            #region Members
            /// <summary>
            /// actor name
            /// </summary>
            public readonly string ActorName;

            /// <summary>
            /// offered map
            /// </summary>
            private readonly Dictionary<string, int> _dicOffered =
                new Dictionary<string, int>();
            /// <summary>
            /// processed map
            /// </summary>
            private readonly Dictionary<string, CallTime> _dicProcessed =
                new Dictionary<string, CallTime>();
            /// <summary>
            /// process error map
            /// </summary>
            private readonly Dictionary<string, int> _dicProcessError =
                new Dictionary<string, int>();
            /// <summary>
            /// process wait
            /// </summary>
            private readonly Dictionary<string, int> _dicProcesswait =
                new Dictionary<string, int>();
            #endregion

            #region Constructors
            /// <summary>
            /// new
            /// </summary>
            /// <param name="actorName"></param>
            public CountTuple(string actorName)
            {
                this.ActorName = actorName;
            }
            #endregion

            #region Public Methods
            /// <summary>
            /// offered a message
            /// </summary>
            /// <param name="currTime"></param>
            public void Offered(DateTime currTime)
            {
                var minutes = (int)TimeSpan.FromTicks(currTime.Ticks).TotalMinutes;
                var arrKeys = new string[3];
                arrKeys[0] = string.Concat("1m/", minutes.ToString());
                arrKeys[1] = string.Concat("5m/", (minutes / 5).ToString());
                arrKeys[2] = string.Concat("30m/", (minutes / 30).ToString());

                foreach (var key in arrKeys)
                {
                    int value;
                    this._dicOffered.TryGetValue(key, out value);
                    this._dicOffered[key] = value + 1;
                }
            }
            /// <summary>
            /// processed a message
            /// </summary>
            /// <param name="startTime"></param>
            /// <param name="endTime"></param>
            public void Processed(DateTime startTime, DateTime endTime)
            {
                var ms = (int)endTime.Subtract(startTime).TotalMilliseconds;
                var minutes = (int)TimeSpan.FromTicks(startTime.Ticks).TotalMinutes;
                var arrKeys = new string[3];
                arrKeys[0] = string.Concat("1m/", minutes.ToString());
                arrKeys[1] = string.Concat("5m/", (minutes / 5).ToString());
                arrKeys[2] = string.Concat("30m/", (minutes / 30).ToString());

                foreach (var key in arrKeys)
                {
                    CallTime callTime;
                    if (this._dicProcessed.TryGetValue(key, out callTime))
                    {
                        this._dicProcessed[key] = new CallTime(Math.Max(callTime.Max, ms),
                            Math.Min(callTime.Min, ms),
                            callTime.Count + 1,
                            callTime.Total + ms); continue;
                    }

                    this._dicProcessed[key] = new CallTime(ms, ms, 1, ms);
                }
            }
            /// <summary>
            /// error
            /// </summary>
            public void Error(DateTime currTime)
            {
                var minutes = (int)TimeSpan.FromTicks(currTime.Ticks).TotalMinutes;
                var arrKeys = new string[3];
                arrKeys[0] = string.Concat("1m/", minutes.ToString());
                arrKeys[1] = string.Concat("5m/", (minutes / 5).ToString());
                arrKeys[2] = string.Concat("30m/", (minutes / 30).ToString());

                foreach (var key in arrKeys)
                {
                    int value;
                    this._dicProcessError.TryGetValue(key, out value);
                    this._dicProcessError[key] = value + 1;
                }
            }
            /// <summary>
            /// wait
            /// </summary>
            /// <param name="totalMinutes"></param>
            /// <param name="value"></param>
            public void Wait(int totalMinutes, int value)
            {
                var arrKeys = new string[3];
                arrKeys[0] = string.Concat("1m/", totalMinutes.ToString());
                arrKeys[1] = string.Concat("5m/", (totalMinutes / 5).ToString());
                arrKeys[2] = string.Concat("30m/", (totalMinutes / 30).ToString());

                foreach (var key in arrKeys)
                {
                    int existsValue;
                    this._dicProcesswait.TryGetValue(key, out existsValue);
                    if (value > existsValue) this._dicProcesswait[key] = value;
                }
            }
            #endregion

            #region Override Methods
            /// <summary>
            /// to string
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                var sb = new System.Text.StringBuilder();

                sb.Append(string.Join(Environment.NewLine, this._dicOffered.Select(c => string.Concat(c.Key, "/offered:", c.Value.ToString())).ToArray()));
                sb.Append(Environment.NewLine);
                sb.Append(string.Join(Environment.NewLine, this._dicProcesswait.Select(c => string.Concat(c.Key, "/wait:", c.Value.ToString())).ToArray()));
                sb.Append(Environment.NewLine);
                sb.Append(string.Join(Environment.NewLine, this._dicProcessed.Select(c => string.Concat(c.Key, "/processed:", c.Value.ToString())).ToArray()));
                sb.Append(Environment.NewLine);
                sb.Append(string.Join(Environment.NewLine, this._dicProcessError.Select(c => string.Concat(c.Key, "/process_error:", c.Value.ToString())).ToArray()));
                sb.Append(Environment.NewLine);

                return sb.ToString();
            }
            #endregion

            #region CallTime
            /// <summary>
            /// call time
            /// </summary>
            public struct CallTime
            {
                /// <summary>
                /// max
                /// </summary>
                public readonly int Max;
                /// <summary>
                /// min
                /// </summary>
                public readonly int Min;
                /// <summary>
                /// count
                /// </summary>
                public readonly int Count;
                /// <summary>
                /// total
                /// </summary>
                public readonly int Total;

                /// <summary>
                /// new
                /// </summary>
                /// <param name="max"></param>
                /// <param name="min"></param>
                /// <param name="count"></param>
                /// <param name="total"></param>
                public CallTime(int max, int min, int count, int total)
                {
                    this.Max = max;
                    this.Min = min;
                    this.Count = count;
                    this.Total = total;
                }

                /// <summary>
                /// tostring
                /// </summary>
                /// <returns></returns>
                public override string ToString()
                {
                    return string.Concat("max:", this.Max.ToString(),
                        " min:", this.Min.ToString(),
                        " count:", this.Count.ToString(),
                        " total:", this.Total.ToString());
                }
            }
            #endregion
        }
    }
}