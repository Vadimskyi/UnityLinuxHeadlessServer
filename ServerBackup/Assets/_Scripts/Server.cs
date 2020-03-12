using System;
using System.Collections;
using System.IO;
using ENet;
using UnityEngine;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace App
{
    public class Server : MonoBehaviour
    {
        [SerializeField] private ushort _port;

        private bool _listen;
        private Host _server;
        private int _packetsReceived;
        private Protocol _protocol;
        private StreamWriter sw;
        private FileStream fs;

        private void Start()
        {
            _listen = true;
            _protocol = new Protocol();
            Debug.unityLogger.logEnabled = true;
            Application.runInBackground = true;
            Application.targetFrameRate = 30;       //important for linux
            QualitySettings.vSyncCount = 0;


            OpenLogFile();
            StartCoroutine(ServerListen());
        }

        private IEnumerator ServerListen()
        {
            ENet.Library.Initialize();
            using (_server = new Host())
            {
                Address address = new Address();

                address.Port = _port;
                _server.Create(address, 4000);

                Event netEvent;

                while (_listen)
                {
                    bool polled = false;
                    while (!polled)
                    {
                        if (_server.CheckEvents(out netEvent) <= 0)
                        {
                            if (_server.Service(15, out netEvent) <= 0)
                                break;

                            polled = true;
                        }

                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                break;

                            case EventType.Connect:
                                DebugLog("Client connected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                                break;

                            case EventType.Disconnect:
                                DebugLog("Client disconnected - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                                break;

                            case EventType.Timeout:
                                DebugLog("Client timeout - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP);
                                break;

                            case EventType.Receive:
                                DebugLog("Packet received from - ID: " + netEvent.Peer.ID + ", IP: " + netEvent.Peer.IP + ", Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);
                                HandlePacket(ref netEvent);
                                netEvent.Packet.Dispose();
                                break;
                        }
                    }

                    yield return null;
                }

                _server.Flush();
            }
            ENet.Library.Deinitialize();
        }

        private void HandlePacket(ref Event netEvent)
        {
            _packetsReceived++;
            var readBuffer = new byte[64];
            var readStream = new MemoryStream(readBuffer);
            var reader = new BinaryReader(readStream);

            readStream.Position = 0;
            netEvent.Packet.CopyTo(readBuffer);
            var packetId = (NetworkEvents)reader.ReadByte();

            DebugLog($"HandlePacket received: {_packetsReceived}");

            if (packetId == NetworkEvents.UpdatePositionRequest)
            {
                var playerId = reader.ReadUInt32();
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var z = reader.ReadSingle();
                var data = new TransformData
                {
                    EventId = (byte)packetId,
                    PlayerId = playerId,
                    xPos = x,
                    yPos = y,
                    zRotation = z
                };
                BroadcastPositionUpdateEvent(data);
            }
        }

        private void BroadcastPositionUpdateEvent(TransformData data)
        {
            var buffer = _protocol.Serialize(data);
            var packet = default(Packet);
            packet.Create(buffer);
            _server.Broadcast(0, ref packet);
        }

        private void DebugLog(string msg)
        {
            Debug.Log(msg);
            sw?.WriteLine(msg);
        }

        private void OpenLogFile()
        {
            try
            {
                fs = File.Open(Application.streamingAssetsPath + "/Logs/debug.log", FileMode.OpenOrCreate, FileAccess.Write);
                sw = new StreamWriter(fs);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnDestroy()
        {
            sw.Close();
            sw.Dispose();
            fs.Close();
            fs.Dispose();

        }
    }

    public enum NetworkEvents : byte
    {
        Login = 1,
        Logout = 2,
        UpdatePositionRequest = 3,
        UpdatePositionEvent
    }

    public struct TransformData
    {
        public byte EventId;
        public uint PlayerId;
        public float xPos;
        public float yPos;
        public float zRotation;
    }
}