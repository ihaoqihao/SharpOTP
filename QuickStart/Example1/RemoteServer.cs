using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example1
{
    public sealed class RemoteServer
    {
        static private readonly List<SharpOTP.Remote.Actor> listActor = new List<SharpOTP.Remote.Actor>();
        static private SharpOTP.Remote.Actor _remoteServer = null;

        static public void StartNew(string currNode)
        {
            _remoteServer = new SharpOTP.Remote.Actor(currNode, "otp.config", "otp");
            listActor.Add(_remoteServer);
            RedisServer.RegisterTo(_remoteServer);
        }

        static public void StopAll()
        {
            listActor.ForEach(c => c.Dispose());
        }
    }
}