using System;
using System.Collections.Generic;
using System.Net;

namespace Qqtang.Core.Protocol
{
    public enum LegacyUdpMessageType : ushort
    {
        Presence = 1,
        Multicast = 2,
        RoomPeer = 3
    }

    public struct LegacyUdpEndpoint
    {
        public byte[] IPv4; // 4 bytes
        public ushort Port;

        public static LegacyUdpEndpoint FromIPEndPoint(IPEndPoint ep)
        {
            byte[] ip = ep.Address.MapToIPv4().GetAddressBytes();
            return new LegacyUdpEndpoint { IPv4 = ip, Port = (ushort)ep.Port };
        }
    }

    public struct LegacyUdpMulticastTarget
    {
        public ushort PlayerId;
        public uint Uin;
    }

    public sealed class LegacyUdpControlPacket
    {
        public byte Flags;
        public uint PacketNumber;
        public ushort PlayerId;
        public uint Uin;
        public LegacyUdpMessageType Type;
        public LegacyUdpEndpoint? Presence;
        public List<LegacyUdpMulticastTarget> MulticastTargets;
        public byte[] MulticastData;
    }

    /// <summary>
    /// Byte-identical port of server/internal/protocol/game/room_peer_udp.go (QQTPPP UDP).
    /// </summary>
    public static class QqtPppUdpCodec
    {
        public const int ControlHeaderSize = 18;
        public const int PeerHandshakeSize = 15;
        public const byte PeerHandshakeRequest = 0x20;
        public const byte PeerHandshakeResponse = 0x21;

        public static ushort ComputeChecksum(byte[] data)
        {
            uint sum = 0;
            for (int offset = 0; offset + 1 < data.Length; offset += 2)
            {
                ushort word = (ushort)(data[offset] | (data[offset + 1] << 8));
                sum += word;
            }
            if ((data.Length & 1) != 0)
                sum += data[data.Length - 1];
            sum = (sum & 0xFFFFu) + (sum >> 16);
            sum = (sum & 0xFFFFu) + (sum >> 16);
            return (ushort)(~sum);
        }

        public static byte[] EncodePresence(uint packetNumber, ushort playerId, uint uin, LegacyUdpEndpoint localEndpoint, byte flags = 0x01)
        {
            byte[] data = new byte[ControlHeaderSize + 6];
            WriteHeaderPrefix(data, flags, packetNumber, playerId, uin, LegacyUdpMessageType.Presence, 6);
            Buffer.BlockCopy(localEndpoint.IPv4, 0, data, ControlHeaderSize, 4);
            data[ControlHeaderSize + 4] = (byte)(localEndpoint.Port >> 8);
            data[ControlHeaderSize + 5] = (byte)localEndpoint.Port;
            WriteChecksum(data, 15);
            return data;
        }

        public static byte[] EncodeMulticast(
            uint packetNumber,
            ushort playerId,
            uint uin,
            IReadOnlyList<LegacyUdpMulticastTarget> targets,
            byte[] encryptedBatchData,
            byte flags = 0x01)
        {
            if (targets == null || targets.Count == 0 || targets.Count > 8)
                throw new ArgumentException("Multicast targets must be 1..8.", nameof(targets));
            if (encryptedBatchData == null || encryptedBatchData.Length == 0)
                throw new ArgumentException("Multicast data cannot be empty.", nameof(encryptedBatchData));

            int routeBytes = targets.Count * 6;
            byte[] data = new byte[ControlHeaderSize + routeBytes + encryptedBatchData.Length];
            WriteHeaderPrefix(data, flags, packetNumber, playerId, uin, LegacyUdpMessageType.Multicast, (byte)routeBytes);
            int off = ControlHeaderSize;
            foreach (var t in targets)
            {
                data[off++] = (byte)(t.PlayerId >> 8);
                data[off++] = (byte)t.PlayerId;
                QqTea.WriteUInt32BE(data, off, t.Uin);
                off += 4;
            }
            Buffer.BlockCopy(encryptedBatchData, 0, data, off, encryptedBatchData.Length);
            WriteChecksum(data, 15);
            return data;
        }

        public static byte[] EncodePeerHandshake(byte flags, uint uin, LegacyUdpEndpoint endpoint)
        {
            byte[] data = new byte[PeerHandshakeSize];
            data[0] = 0;
            data[1] = PeerHandshakeSize;
            data[2] = flags;
            QqTea.WriteUInt32BE(data, 5, uin);
            Buffer.BlockCopy(endpoint.IPv4, 0, data, 9, 4);
            data[13] = (byte)(endpoint.Port >> 8);
            data[14] = (byte)endpoint.Port;
            ushort cs = ComputeChecksum(data);
            data[3] = (byte)(cs >> 8);
            data[4] = (byte)cs;
            return data;
        }

        public static LegacyUdpControlPacket DecodeControlPacket(byte[] data)
        {
            if (data == null || data.Length < ControlHeaderSize)
                throw new InvalidOperationException("UDP control datagram too short.");
            ushort declared = (ushort)((data[0] << 8) | data[1]);
            if (declared != data.Length)
                throw new InvalidOperationException($"UDP declared length {declared} != {data.Length}.");
            if (ComputeChecksum(data) != 0)
                throw new InvalidOperationException("Invalid QQTPPP UDP checksum.");

            var pkt = new LegacyUdpControlPacket
            {
                Flags = data[2],
                PacketNumber = QqTea.ReadUInt32BE(data, 3),
                PlayerId = (ushort)((data[7] << 8) | data[8]),
                Uin = QqTea.ReadUInt32BE(data, 9),
                Type = (LegacyUdpMessageType)((data[13] << 8) | data[14])
            };
            byte routeBytes = data[17];
            int payloadLen = data.Length - ControlHeaderSize;

            if (pkt.Type == LegacyUdpMessageType.Presence && payloadLen == 6)
            {
                byte[] ip = new byte[4];
                Buffer.BlockCopy(data, ControlHeaderSize, ip, 0, 4);
                pkt.Presence = new LegacyUdpEndpoint
                {
                    IPv4 = ip,
                    Port = (ushort)((data[ControlHeaderSize + 4] << 8) | data[ControlHeaderSize + 5])
                };
            }
            else if (pkt.Type == LegacyUdpMessageType.Multicast && routeBytes >= 6 && routeBytes < payloadLen)
            {
                pkt.MulticastTargets = new List<LegacyUdpMulticastTarget>(routeBytes / 6);
                for (int off = 0; off < routeBytes; off += 6)
                {
                    int p = ControlHeaderSize + off;
                    pkt.MulticastTargets.Add(new LegacyUdpMulticastTarget
                    {
                        PlayerId = (ushort)((data[p] << 8) | data[p + 1]),
                        Uin = QqTea.ReadUInt32BE(data, p + 2)
                    });
                }
                int batchLen = payloadLen - routeBytes;
                pkt.MulticastData = new byte[batchLen];
                Buffer.BlockCopy(data, ControlHeaderSize + routeBytes, pkt.MulticastData, 0, batchLen);
            }
            return pkt;
        }

        private static void WriteHeaderPrefix(byte[] data, byte flags, uint packetNum, ushort playerId, uint uin, LegacyUdpMessageType type, byte routeBytes)
        {
            ushort len = (ushort)data.Length;
            data[0] = (byte)(len >> 8);
            data[1] = (byte)len;
            data[2] = flags;
            QqTea.WriteUInt32BE(data, 3, packetNum);
            data[7] = (byte)(playerId >> 8);
            data[8] = (byte)playerId;
            QqTea.WriteUInt32BE(data, 9, uin);
            data[13] = (byte)((ushort)type >> 8);
            data[14] = (byte)(ushort)type;
            data[15] = 0;
            data[16] = 0;
            data[17] = routeBytes;
        }

        private static void WriteChecksum(byte[] data, int checksumOffset)
        {
            ushort cs = ComputeChecksum(data);
            data[checksumOffset] = (byte)(cs >> 8);
            data[checksumOffset + 1] = (byte)cs;
        }
    }
}
