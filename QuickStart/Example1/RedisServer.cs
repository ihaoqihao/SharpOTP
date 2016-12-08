using SharpOTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Example1
{
    public sealed class RedisServer
    {
        #region Members
        static private SharpOTP.Remote.Actor _remoteServer = null;

        static private readonly Actor _dispatcherServer =
            GenServer.Start<RedisServer>(null, 32);
        static private readonly Worker[] _arrWorker =
            Enumerable.Range(0, 16).Select(_ => new Worker()).ToArray();
        #endregion

        #region Constructors
        static RedisServer()
        {
        }
        #endregion

        #region Actor Callback
        public Task<Thrift.VoidResp> HandleCall(Thrift.SetReq message)
        {
            return _arrWorker[SharpOTP.Remote.Ketama.GetHashCode(message.Key) % _arrWorker.Length].Set(message);
        }
        public Task<Thrift.StringStruct> HandleCall(Thrift.GetReq message)
        {
            return _arrWorker[SharpOTP.Remote.Ketama.GetHashCode(message.Key) % _arrWorker.Length].Get(message);
        }
        #endregion

        #region API
        static public void RegisterTo(SharpOTP.Remote.Actor remoteServer)
        {
            if (remoteServer == null) throw new ArgumentNullException("remoteServer");
            _remoteServer = remoteServer;

            _remoteServer.Register<Thrift.SetReq, Thrift.VoidResp>("set", _dispatcherServer);
            _remoteServer.Register<Thrift.GetReq, Thrift.StringStruct>("get", _dispatcherServer);
        }

        static public Task Set(string key, string value)
        {
            return _remoteServer.Call<Thrift.SetReq, Thrift.VoidResp>(key, "set",
                new Thrift.SetReq { Key = key, Value = value });
        }
        static public Task<Thrift.StringStruct> Get(string key)
        {
            return _remoteServer.Call<Thrift.GetReq, Thrift.StringStruct>(key, "get",
                new Thrift.GetReq { Key = key, });
        }
        static public Task<Thrift.StringStruct[]> Get(params string[] keys)
        {
            return _remoteServer.Call<Thrift.GetReq, Thrift.StringStruct>("get",
                new Func<Thrift.GetReq, string>(r => r.Key), keys.Select(c => new Thrift.GetReq { Key = c }).ToArray());
        }
        #endregion

        public sealed class Worker
        {
            #region Members
            private readonly Actor _server = null;
            private readonly Dictionary<string, string> _dic =
                new Dictionary<string, string>();
            #endregion

            #region Constructors
            public Worker()
            {
                this._server = GenServer.Start(this);
            }
            #endregion

            #region Actor Callback
            public async Task<Thrift.VoidResp> HandleCall(Thrift.SetReq message)
            {
                //Console.WriteLine("set a key:" + message.Key + " value:" + message.Value);
                this._dic[message.Key] = message.Value;
                return new Thrift.VoidResp();
            }
            public async Task<Thrift.StringStruct> HandleCall(Thrift.GetReq message)
            {
                string value;
                if (this._dic.TryGetValue(message.Key, out value))
                    return new Thrift.StringStruct { Value = value };

                return new Thrift.StringStruct();
            }
            #endregion

            #region API
            public Task<Thrift.VoidResp> Set(Thrift.SetReq request)
            {
                return this._server.Call<Thrift.VoidResp>(request);
            }
            public Task<Thrift.StringStruct> Get(Thrift.GetReq request)
            {
                return this._server.Call<Thrift.StringStruct>(request);
            }
            #endregion
        }
    }
}