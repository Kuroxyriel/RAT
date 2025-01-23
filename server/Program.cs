using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace Virus {
    class Program {
    	private static IPEndPoint IPEP = IPEndPoint.Parse("127.0.0.1:42069");
		private static bool running = true;
		private static List<Handler> conns = new List<Handler>();
		
        private static void Main(string[] args) {
        	new Thread(StartUDPListener).Start();
			Socket sock = new Socket(SocketType.Stream, ProtocolType.Tcp);
			sock.Bind(IPEP);
			sock.Listen();
        	Console.WriteLine("Socket bound");
			
			while (running) {
				Socket client = sock.Accept();
				Handler hnd = new Handler(client, conns.Count);
				conns.Add(hnd);
			}
			sock.Dispose();
        }

        private static void StartUDPListener() {
        	UdpClient listener = new UdpClient(IPEP.Port-1);
        	while (running) {
        		byte[] data = listener.Receive(ref IPEP);
        		conns[data[0]].Write((Message.Type)data[1], data.Skip(2));
        	}
        }
		public static void Remove(int id) {
			conns.RemoveAt(id);
			Console.WriteLine("#{0} disconnected, next ID is #{1}", id, conns.Count);
		}
	}
}