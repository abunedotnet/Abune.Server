//-----------------------------------------------------------------------
// <copyright file="CliClient.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Cli
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using Abune.Shared.Command;
    using Abune.Shared.Message;
    using Abune.Shared.Util;
    using Abune.Shared.Protocol;

    /// <summary>
    /// Abune CLI client.
    /// </summary>
    public class CliClient : IDisposable
    {
        private Thread thread;
        private UdpClient client;
        private IPEndPoint localEndPoint;
        private IPEndPoint serverEndPoint;
        private float locationX, locationY, locationZ;
        private string signingKeyBase64;

        /// <summary>Gets or sets the client identifier.</summary>
        /// <value>The client identifier.</value>
        public uint ClientId { get; private set; }

        /// <summary>Gets the time stamp of the last message received.</summary>
        /// <value>The last message received.</value>
        public DateTime LastMessageReceived { get; private set; }

        /// <summary>Gets the reliable messaging.</summary>
        /// <value>The reliable messaging.</value>
        public ReliableUdpMessaging ReliableMessaging { get; private set; }

        /// <summary>Gets or sets the command message handler.</summary>
        /// <value>The command action.</value>
        public Action<ObjectCommandEnvelope> OnCommand { get; set; }

        /// <summary>Gets or sets the frame message handler.</summary>
        /// <value>The command action.</value>
        public Action<UdpTransferFrame> OnFrame { get; set; }

        /// <summary>Gets or sets the connection handler.</summary>
        /// <value>The command action.</value>
        public Action OnConnected { get; set; }

        /// <summary>Initializes a new instance of the <a onclick="return false;" href="CliClient" originaltag="see">CliClient</a> class.</summary>
        public CliClient()
        {
            LastMessageReceived = DateTime.MinValue;
            ReliableMessaging = new ReliableUdpMessaging();
            ReliableMessaging.OnProcessCommandMessage = ProcessCommandMessage;
            ReliableMessaging.OnSendFrame = (f) => SendData(f.Serialize());
        }

        /// <summary>Connects the specified server endpoint.</summary>
        /// <param name="serverEndpoint">The server endpoint.</param>
        /// <param name="serverPort">The server port.</param>
        /// <param name="clientPort">The client port.</param>
        /// <param name="signingKeyBase64">Signing key for token generation.</param>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="locationX">The location x.</param>
        /// <param name="locationY">The location y.</param>
        /// <param name="locationZ">The location z.</param>
        public void Connect(string serverEndpoint, int serverPort, int clientPort, string signingKeyBase64, uint clientId, float locationX, float locationY, float locationZ)
        {
            this.locationX = locationX;
            this.locationY = locationY;
            this.locationZ = locationZ;
            this.ClientId = clientId;
            this.signingKeyBase64 = signingKeyBase64;
            if (this.ClientId == 0)
            {
                var random = new System.Random();
                this.ClientId = (uint)random.Next();
            }
            //send

            IPAddress ipAddr;
            if (IPAddress.TryParse(serverEndpoint, out ipAddr))
            {
                serverEndPoint = new IPEndPoint(IPAddress.Parse(serverEndpoint), serverPort);
            }
            else
            {
                var hostEntry = Dns.GetHostEntry(serverEndpoint).AddressList.First(a => a.AddressFamily == AddressFamily.InterNetwork);
                serverEndPoint = new IPEndPoint(hostEntry, serverPort);
            }

            this.client = new UdpClient(clientPort);

            //receive       
            this.localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)client.Client.LocalEndPoint).Port);

            InitializeCommunication((uint)localEndPoint.Port);

            this.thread = new Thread(Run);
            this.thread.Start();
        }

        /// <summary>Processes the command message.</summary>
        /// <param name="cmdMsg">The command MSG.</param>
        public void ProcessCommandMessage(ObjectCommandEnvelope cmdMsg)
        {
            if (OnCommand != null)
            {
                OnCommand.Invoke(cmdMsg);
            }
        }

        /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
        /// <param name="disposing">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }            
            if (client != null)
            {
                client.Dispose();
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);            
        }
        private static string GetAbuneSharedAssemblyVersion()
        {
            return typeof(ClientHelloMessage).Assembly.GetName().Version.ToString();
        }

        /// <summary>Initializes the communication.</summary>
        /// <param name="clientPort">The client port.</param>
        private void InitializeCommunication(uint clientPort)
        {
            var msgClientHello = new ClientHelloMessage() { ClientId = ClientId, ClientPort = clientPort, Message = $"Hello from {ClientId}", Version = GetAbuneSharedAssemblyVersion() };
            UdpTransferFrame frame = new UdpTransferFrame(FrameType.ClientHello, msgClientHello.Serialize());
            ReliableMessaging.OnSendFrame(frame);
        }
        
        /// <summary>Runs this instance.</summary>
        private void Run()
        {
            byte[] receiveBuffer;
            do
            {
                IPEndPoint bindToServerEndpoint = new IPEndPoint(serverEndPoint.Address, serverEndPoint.Port);
                receiveBuffer = client.Receive(ref bindToServerEndpoint);
                var udpTransferFrame = new UdpTransferFrame(receiveBuffer);
                ProcessUdpTransferFrame(udpTransferFrame);
            } while (receiveBuffer != null && receiveBuffer.Length > 0);
        }

        /// <summary>Sends the data.</summary>
        /// <param name="sendBuffer">The send buffer.</param>
        private void SendData(byte[] sendBuffer)
        {
            client.Send(sendBuffer, sendBuffer.Length, serverEndPoint);
        }

        /// <summary>Subscribes to default area.</summary>
        private void SubscribeToDefaultArea()
        {
            ulong areaId = Locator.GetAreaIdFromWorldPosition(locationX, locationY, locationZ);
            ReliableMessaging.SendCommand(0, new SubscribeAreaCommand(ClientId, areaId, 0), 0);
        }

        private void ProcessAuthenticationRequest(ServerAuthenticationRequest request, string signingKey)
        {
            var offlineTokenProvider = new OfflineTokenProvider("", signingKey);
            string token = offlineTokenProvider.CreateJWTToken(request.AuthenticationChallenge, DateTime.UtcNow.AddMinutes(15));
            var cmdClientAuthenticationResponse = new ClientAuthenticationResponse() { AuthenticationToken = token };
            ReliableMessaging.OnSendFrame(new UdpTransferFrame(FrameType.ClientAuthenticationResponse, cmdClientAuthenticationResponse.Serialize()));
        }

        /// <summary>Processes the UDP transfer frame.</summary>
        /// <param name="frame">The frame.</param>
        private void ProcessUdpTransferFrame(UdpTransferFrame frame)
        {
            switch (frame.Type)
            {
                case FrameType.ServerAuthenticationRequest:
                    var msgAuthenticationRequest = new ServerAuthenticationRequest(frame.MessageBuffer);
                    ProcessAuthenticationRequest(msgAuthenticationRequest, this.signingKeyBase64);
                    break;

                case FrameType.ServerHello:
                    SubscribeToDefaultArea();
                    if (OnConnected != null)
                    {
                        Thread connected = new Thread(() => OnConnected());
                        connected.Start();
                    }
                    break;

                case FrameType.ServerPing:
                    var msgServerPing = new ServerPingMessage(frame.MessageBuffer);
                    var now = TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
                    var cmdClientPong = new ClientPongMessage() { ServerRequestTimestamp = msgServerPing.ServerTimestamp, ClientRequestTimestamp = now, ClientResponseTimestamp = now };
                    ReliableMessaging.OnSendFrame(new UdpTransferFrame(FrameType.ClientPong, cmdClientPong.Serialize()));
                    break;
                case FrameType.Message:
                    LastMessageReceived = DateTime.Now;
                    ReliableMessaging.ProcessMessageFrame(frame);
                    break;
                default:
                    throw new NotImplementedException(frame.Type.ToString());
            }
        }        
    }
}