using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace App
{
    public class Protocol
    {
        private void InitWriter(int size)
        {
            m_buffer = new byte[size];
            m_stream = new MemoryStream(m_buffer);
            m_writer = new BinaryWriter(m_stream);
        }

        public byte[] Serialize(TransformData data)
        {
            const int bufSize = sizeof(byte) + sizeof(uint) + sizeof(float) + sizeof(float) + sizeof(float);
            InitWriter(bufSize);
            m_writer.Write(data.EventId);
            m_writer.Write(data.PlayerId);
            m_writer.Write(data.xPos);
            m_writer.Write(data.yPos);
            m_writer.Write(data.zRotation);
            return m_buffer;
        }

        public TransformData Deserialize(BinaryReader reader)
        {
            TransformData data = new TransformData();
            data.PlayerId = reader.ReadUInt32();
            data.xPos = reader.ReadSingle();
            data.yPos = reader.ReadSingle();
            data.zRotation = reader.ReadSingle();
            return data;
        }

        private BinaryWriter m_writer;
        private BinaryReader m_reader;
        private MemoryStream m_stream;
        private byte[] m_buffer;
    }
}
