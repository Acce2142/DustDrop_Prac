using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DustDrop
{
    public enum QueueType : byte
    {
        Download,
        Upload

    }
    public class TransferQueue
    {
        private const int FILE_BUFFER_SIZE = 8175;
        private static byte[] file_buffer = new byte[FILE_BUFFER_SIZE];

        private ManualResetEvent pauseEvent;

        public int ID;
        public int Progress, LastProgress;

        public long Transferred;
        public long Index;
        public long Length;
        public bool Running;
        public bool Paused;

        public string Filename;

        public QueueType Type;
        public TransferClient Client;
        public Thread Thread;
        public FileStream FS;

        private TransferQueue()
        {
            pauseEvent = new ManualResetEvent(true);
            
            Running = true;
        }

        public void Start()
        {
            Running = true;
            Thread.Start(this);
        }

        public void Stop()
        {
            Running = false;
        }

        public void Pause()
        {
            if (!Paused)
            {
                pauseEvent.Reset();
            } else
            {
                pauseEvent.Set();
            }
            Paused = !Paused;
        }

        public void Close()
        {
            try
            {
                Client.Transfers.Remove(ID);
            }
            catch
            {

            }
            Running = false;
            FS.Close();
            pauseEvent.Dispose();
            Client = null;
        }

        public void Write(byte[] bytes, long index)
        {
            lock (this)
            {
                FS.Position = index;
                FS.Write(bytes, 0, bytes.Length);
                Transferred += bytes.Length;
            }
        }

        private static void transferProc(object o)
        {
            TransferQueue queue = (TransferQueue)o;
            while(queue.Running && queue.Index < queue.Length)
            {
                queue.pauseEvent.WaitOne();
                if (!queue.Running)
                {
                    break;
                }
                lock (file_buffer)
                {
                    queue.FS.Position = queue.Index;
                    int read = queue.FS.Read(file_buffer, 0, file_buffer.Length);
                    PacketWriter pw = new PacketWriter();
                    pw.Write(queue.ID);
                    pw.Write(queue.Index);
                    pw.Write(read);
                    pw.Write(file_buffer, 0, read);
                    queue.Transferred += read;
                    queue.Index += read;

                    queue.Client.Send(pw.GetBytes());
                    queue.Progress = (int)((queue.Transferred * 100) / queue.Length);
                    if(queue.LastProgress < queue.Progress)
                    {
                        queue.LastProgress = queue.Progress;
                        queue.Client.callProgressChanged(queue);
                    }
                }
            }
        }
    }
}
