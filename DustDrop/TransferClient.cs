using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DustDrop
{
    public delegate void TransferEventHandler(object sender, TransferQueue queue);
    public delegate void ConnectCallBack(object sender, string error);
    public class TransferClient
    {
        private Socket _BaseSocket;
        private byte[] _Buffer = new byte[8192];
        private ConnectCallBack _ConnectCallback;
        private Dictionary<int, TransferQueue> _Transfers = new Dictionary<int, TransferQueue>();
        public Dictionary<int, TransferQueue> Transfers
        {
            get{return _Transfers;}
        }

        public bool Closed
        {
            get;
            private set;
        }

        public string OutputFolder
        {
            get;
            set;
        }

        public IPEndPoint EndPoint
        {
            get;
            private set;
        }

        public event TransferEventHandler Queued;
        public event TransferEventHandler ProgressChanged;
        public event TransferEventHandler Stopped;
        public event TransferEventHandler Complate;
        public event EventHandler Disconnected;

        public TransferClient()
        {
            _BaseSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        public TransferClient(Socket socket)
        {
            _BaseSocket = socket;
            EndPoint = (IPEndPoint)_BaseSocket.RemoteEndPoint;
        }

        public void Connect(string hostname, int port, ConnectCallBack callBack)
        {
            _ConnectCallback = callBack;

            _BaseSocket.BeginConnect(hostname, port, connectCallback, null);
        }

        private void connectCallback(IAsyncResult ar)
        {
            string error = null;
            try
            {
                _BaseSocket.EndConnect(ar);
                EndPoint = (IPEndPoint)_BaseSocket.RemoteEndPoint;
            } catch (Exception ex)
            {
                error = ex.Message;
            }
            _ConnectCallback(this, error);
        }

        public void Run()
        {
            try
            {
                _BaseSocket.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.Peek, null, null);
            } catch
            {

            }
        }

        public void Close()
        {
            Closed = true;
            _BaseSocket.Close();
            _Transfers.Clear();
            _Transfers = null;
            _Buffer = null;
            OutputFolder = null;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        public void Send(byte[] data)
        {
            if (Closed)
            {
                return;
            }
            lock (this)
            {
                try
                {
                    _BaseSocket.Send(BitConverter.GetBytes(data.Length), 0, 4, SocketFlags.None);
                    _BaseSocket.Send(data, 0, data.Length, SocketFlags.None);
                } catch
                {
                    Close();
                }
            }
        }
        private void receiveCallback(IAsyncResult ar)
        {
            try
            {
                int found = _BaseSocket.EndReceive(ar);
                if(found >= 4)
                {
                    _BaseSocket.Receive(_Buffer, 0, 4, SocketFlags.None);
                    int size = BitConverter.ToInt32(_Buffer, 0);
                    int read = _BaseSocket.Receive(_Buffer, 0, size, SocketFlags.None);
                    while(read < size)
                    {
                        read += _BaseSocket.Receive(_Buffer, read, size - read, SocketFlags.None);
                    }
                    process();
                }
            }
            catch
            {

            }
        }
        internal void callProgressChanged(TransferQueue queue)
        {
            if (ProgressChanged != null)
            {
                ProgressChanged(this, queue);
            }
        }
    }
}
