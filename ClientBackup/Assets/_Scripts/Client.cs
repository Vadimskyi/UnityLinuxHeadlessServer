using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using ENet;
using UnityEngine;
using UnityEngine.UI;
using Event = ENet.Event;
using EventType = ENet.EventType;

namespace App
{
    public class Client : MonoBehaviour
    {
        [SerializeField]
        private Text _packetsLabel;
        [SerializeField]
        private Text _errorLabel;
        [SerializeField]
        private bool _listen;
        [SerializeField]
        private string _ip;
        [SerializeField]
        private ushort _port;
        [SerializeField]
        private int _packetsSent;
        [SerializeField]
        private int _packetsReceived;
        [SerializeField]
        private int _packetsSize;

        private Peer _peer;
        private Protocol _protocol;

        private void Awake()
        {
            _protocol = new Protocol();
            Application.runInBackground = true;
            Application.logMessageReceivedThreaded += Application_logMessageReceivedThreaded;

            StartCoroutine(ClientListen());
        }

        private void Start()
        {
            StartCoroutine(NetworkUpdater());
        }

        private IEnumerator ClientListen()
        {
            ENet.Library.Initialize();
            using (Host client = new Host())
            {
                Address address = new Address();
                address.SetHost(_ip);
                address.Port = _port;
                client.Create();

                var peer = client.Connect(address);
                _peer = peer;

                Event netEvent;

                while (_listen)
                {
                    bool polled = false;

                    while (!polled)
                    {
                        if (client.CheckEvents(out netEvent) <= 0)
                        {
                            if (client.Service(15, out netEvent) <= 0)
                                break;

                            polled = true;
                        }

                        switch (netEvent.Type)
                        {
                            case EventType.None:
                                break;

                            case EventType.Connect:
                                Debug.Log("Client connected to server - " + peer.ID);
                                break;

                            case EventType.Disconnect:
                                Debug.Log("Client disconnected from server");
                                break;

                            case EventType.Timeout:
                                Debug.Log("Client connection timeout");
                                break;

                            case EventType.Receive:
                                ParsePacket(ref netEvent);
                                netEvent.Packet.Dispose();
                                break;
                        }
                    }

                    yield return null;
                }

                client.Flush();
                ENet.Library.Deinitialize();
            }
        }

        private IEnumerator NetworkUpdater()
        {
            while (_listen)
            {
                yield return null;
                if (_peer.NativeData == IntPtr.Zero) continue;
                if (_packetsSent == 1000000) continue;

                _packetsSent++;
                SendMovementUpdate(new TransformData
                {
                    EventId = (byte)NetworkEvents.UpdatePositionRequest,
                    PlayerId = 1,
                    xPos = transform.position.x,
                    yPos = transform.position.y,
                    zRotation = transform.rotation.z
                });
            }
        }

        private void ParsePacket(ref ENet.Event netEvent)
        {
            var readBuffer = new byte[64];
            var readStream = new MemoryStream(readBuffer);
            var reader = new BinaryReader(readStream);

            readStream.Position = 0;
            _packetsReceived++;
            _packetsLabel.text = $"Packets received: {_packetsReceived}";
            _packetsSize += netEvent.Packet.Length;
            netEvent.Packet.CopyTo(readBuffer);
            var packetId = (NetworkEvents)reader.ReadByte();


            if (packetId == NetworkEvents.UpdatePositionRequest)
            {
                UpdatePosition(reader);
            }
        }

        private void SendMovementUpdate(TransformData data)
        {
            var buffer = _protocol.Serialize(data);
            var packet = default(Packet);
            packet.Create(buffer);
            _peer.Send(0, ref packet);
        }

        private void UpdatePosition(BinaryReader reader)
        {
            TransformData data = _protocol.Deserialize(reader);
            //do something...
        }

        private void Application_logMessageReceivedThreaded(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
                _errorLabel.text = condition;
        }

        private void OnApplicationQuit()
        {
            StopAllCoroutines();
            _listen = false;
        }
    }

    public enum NetworkEvents : byte
    {
        Login = 1,
        Logout = 2,
        UpdatePositionRequest = 3,
        UpdatePositionEvent
    }
}


public struct TransformData
{
    public byte EventId;
    public uint PlayerId;
    public float xPos;
    public float yPos;
    public float zRotation;
}