using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using SendReceiveClasses;

namespace BridgeOpsClient
{
    /* This class handles all interaction from the client TO the server over the inbound port. No NetworkStream
     * objects should be created anywhere in the program to use that port outside of this class object, initialised
     * in App.
     * 
     * Interactions should ALWAYS begin with NewNetworkStream, and ALWAYS end with CloseNetworkStream, without fail.
     * The reason for this is that other threads are completely blocked from accessing the methods in this function
     * between these two functions.
     */

    class NetworkStreamController
    {
        SendReceive sr;
        SessionDetails sd;

        public NetworkStreamController(SendReceive sr, SessionDetails sd)
        {
            this.sr = sr;
            this.sd = sd;
        }

        private object networkStreamLock = new();
        private NetworkStream? stream;
        public NetworkStream? Stream { get { return stream; } }

        private object streamLock = new();
        private bool streamInUse = false;

        public bool NewNetworkStream()
        {
            lock (streamLock)
            {
                while (streamInUse)
                    Monitor.Wait(streamLock);
                streamInUse = true;
            }

            try
            {
                stream = App.sr.NewClientNetworkStream(sd.ServerEP);
            }
            catch { }

            // Switch off streamInUse if we failed to initialise stream.
            streamInUse = stream != null;

            return streamInUse;
        }

        public int ReadByte()
        {
            if (!streamInUse || stream == null)
                return int.MaxValue;
            return sr.ReadByte(stream);
        }

        public string ReadString()
        {
            if (!streamInUse || stream == null)
                return "";
            return sr.ReadString(stream);
        }

        public void WriteByte(byte b)
        {
            if (streamInUse && stream != null)
                stream.WriteByte(b);
        }

        public void WriteString(string s)
        {
            if (streamInUse && stream != null)
                sr.WriteAndFlush(stream, s);
        }

        public void CloseNetworkStream()
        {

            lock (streamLock)
            {
                stream = null;
                streamInUse = false;
                Monitor.Pulse(streamLock);
            }
        }
    }
}
