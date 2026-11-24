using System;
using System.Collections.Generic;
using System.Text;

namespace Qqtang.Core.Protocol
{
    public sealed class BigEndianBuffer
    {
        private byte[] _buf;
        private int _len;

        public BigEndianBuffer(int initialCapacity = 64)
        {
            _buf = new byte[Math.Max(16, initialCapacity)];
        }

        public int Length => _len;

        private void Ensure(int add)
        {
            if (_len + add <= _buf.Length) return;
            int next = Math.Max(_buf.Length * 2, _len + add);
            Array.Resize(ref _buf, next);
        }

        public void WriteByte(byte v)
        {
            Ensure(1);
            _buf[_len++] = v;
        }

        public void WriteUInt16(ushort v)
        {
            Ensure(2);
            _buf[_len++] = (byte)(v >> 8);
            _buf[_len++] = (byte)v;
        }

        public void WriteUInt32(uint v)
        {
            Ensure(4);
            _buf[_len++] = (byte)(v >> 24);
            _buf[_len++] = (byte)(v >> 16);
            _buf[_len++] = (byte)(v >> 8);
            _buf[_len++] = (byte)v;
        }

        public void WriteUInt32LE(uint v)
        {
            Ensure(4);
            _buf[_len++] = (byte)v;
            _buf[_len++] = (byte)(v >> 8);
            _buf[_len++] = (byte)(v >> 16);
            _buf[_len++] = (byte)(v >> 24);
        }

        public void WriteBytes(byte[] src)
        {
            if (src == null || src.Length == 0) return;
            Ensure(src.Length);
            Buffer.BlockCopy(src, 0, _buf, _len, src.Length);
            _len += src.Length;
        }

        public void WriteBytes(byte[] src, int offset, int count)
        {
            if (count <= 0) return;
            Ensure(count);
            Buffer.BlockCopy(src, offset, _buf, _len, count);
            _len += count;
        }

        public void WriteZeroes(int count)
        {
            if (count <= 0) return;
            Ensure(count);
            Array.Clear(_buf, _len, count);
            _len += count;
        }

        public void WriteFixedSlot(byte[] content, int slotSize)
        {
            Ensure(slotSize);
            Array.Clear(_buf, _len, slotSize);
            if (content != null && content.Length > 0)
            {
                int copy = Math.Min(content.Length, slotSize);
                Buffer.BlockCopy(content, 0, _buf, _len, copy);
            }
            _len += slotSize;
        }

        public byte[] ToArray()
        {
            byte[] res = new byte[_len];
            Buffer.BlockCopy(_buf, 0, res, 0, _len);
            return res;
        }
    }

    public struct BigEndianReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        public int Offset { get; private set; }
        public int Remaining => _end - Offset;

        public BigEndianReader(byte[] data, int offset = 0, int count = -1)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            Offset = offset;
            _end = count < 0 ? data.Length : offset + count;
        }

        public byte ReadByte()
        {
            if (Remaining < 1) throw new InvalidOperationException("Unexpected EOF reading byte.");
            return _data[Offset++];
        }

        public ushort ReadUInt16()
        {
            if (Remaining < 2) throw new InvalidOperationException("Unexpected EOF reading uint16.");
            ushort v = (ushort)((_data[Offset] << 8) | _data[Offset + 1]);
            Offset += 2;
            return v;
        }

        public uint ReadUInt32()
        {
            if (Remaining < 4) throw new InvalidOperationException("Unexpected EOF reading uint32.");
            uint v = ((uint)_data[Offset] << 24) |
                     ((uint)_data[Offset + 1] << 16) |
                     ((uint)_data[Offset + 2] << 8) |
                     _data[Offset + 3];
            Offset += 4;
            return v;
        }

        public uint ReadUInt32LE()
        {
            if (Remaining < 4) throw new InvalidOperationException("Unexpected EOF reading uint32 LE.");
            uint v = _data[Offset] |
                     ((uint)_data[Offset + 1] << 8) |
                     ((uint)_data[Offset + 2] << 16) |
                     ((uint)_data[Offset + 3] << 24);
            Offset += 4;
            return v;
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0 || Remaining < count)
                throw new InvalidOperationException($"Unexpected EOF reading {count} bytes ({Remaining} remain).");
            byte[] res = new byte[count];
            Buffer.BlockCopy(_data, Offset, res, 0, count);
            Offset += count;
            return res;
        }

        public void Skip(int count)
        {
            if (count < 0 || Remaining < count)
                throw new InvalidOperationException($"Unexpected EOF skipping {count} bytes.");
            Offset += count;
        }
    }

    public static class LegacyTextEncoding
    {
        private static readonly Encoding GbkOrFallback = ResolveEncoding();

        private static Encoding ResolveEncoding()
        {
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch
            {
                // shortcut: fallback to UTF-8 when CP936 provider is absent in minimal runtimes, upgrade by bundling System.Text.Encoding.CodePages if non-ASCII room/nicknames are required.
                return Encoding.UTF8;
            }
        }

        public static byte[] EncodeSlot(string text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<byte>();
            byte[] bytes = GbkOrFallback.GetBytes(text);
            if (bytes.Length <= maxBytes) return bytes;
            byte[] truncated = new byte[maxBytes];
            Buffer.BlockCopy(bytes, 0, truncated, 0, maxBytes);
            return truncated;
        }

        public static string DecodeNullTerminated(byte[] raw)
        {
            if (raw == null || raw.Length == 0) return string.Empty;
            int len = raw.Length;
            while (len > 0 && raw[len - 1] == 0) len--;
            if (len == 0) return string.Empty;
            return GbkOrFallback.GetString(raw, 0, len);
        }
    }

    public sealed class DecodedLocalPacket
    {
        public uint OuterSequence;
        public ushort RouteSequence;
        public uint EnvelopeUin;
        public ushort Command;
        public uint InnerUnknown;
        public ushort InnerSequence;
        public ushort Route;
        public ushort Marker;
        public ushort SectionId;
        public byte[] Payload;
    }

    /// <summary>
    /// Encodes/decodes the local QQTang TCP envelope (local_packet.go & room_fast_packet.go).
    /// </summary>
    public static class LocalPacketCodec
    {
        public const int InnerHeaderSize = 14;
        public const int EnvelopeSize = 18;
        public const int ServiceTicketSize = 32;
        public const int EncryptedOffset = EnvelopeSize + ServiceTicketSize; // 50

        public static byte[] EncodeLocalPacket(
            uint outerSeq,
            ushort routeSeq,
            uint uin,
            ushort command,
            ushort route,
            ushort sectionId,
            byte[] payload,
            uint innerUnknown = 0)
        {
            var inner = new BigEndianBuffer(InnerHeaderSize + (payload?.Length ?? 0));
            inner.WriteUInt16(command);
            inner.WriteUInt32(innerUnknown);
            inner.WriteUInt16(routeSeq);
            inner.WriteUInt16(route);
            inner.WriteUInt16(0xFFFF);
            inner.WriteUInt16(sectionId);
            if (payload != null && payload.Length > 0)
                inner.WriteBytes(payload);

            byte[] encrypted = QqTea.Encrypt(inner.ToArray(), QqTea.LocalKey);
            int total = EncryptedOffset + encrypted.Length;
            var frame = new BigEndianBuffer(total);
            frame.WriteUInt32((uint)total);
            frame.WriteUInt32(outerSeq);
            frame.WriteUInt16(routeSeq);
            frame.WriteUInt16(0xFFFF);
            frame.WriteUInt32(uin);
            frame.WriteByte(1);
            frame.WriteByte(ServiceTicketSize);
            frame.WriteBytes(QqTea.LocalServiceTicket);
            frame.WriteBytes(encrypted);
            return frame.ToArray();
        }

        public static DecodedLocalPacket DecodeLocalPacket(byte[] frame)
        {
            if (frame == null || frame.Length < EncryptedOffset + 16)
                throw new InvalidOperationException($"Local packet length {frame?.Length ?? 0} is too short.");

            var r = new BigEndianReader(frame);
            uint declared = r.ReadUInt32();
            if (declared != frame.Length)
                throw new InvalidOperationException($"Declared length {declared} != actual {frame.Length}.");

            uint outerSeq = r.ReadUInt32();
            ushort routeSeq = r.ReadUInt16();
            r.ReadUInt16(); // 0xFFFF marker
            uint uin = r.ReadUInt32();
            byte keyMarker = r.ReadByte();
            byte stLen = r.ReadByte();
            if (keyMarker != 1 || stLen != ServiceTicketSize)
                throw new InvalidOperationException($"Unsupported key/ST marker {keyMarker}/{stLen}.");

            r.Skip(ServiceTicketSize);
            byte[] plaintext = QqTea.Decrypt(frame, EncryptedOffset, frame.Length - EncryptedOffset, QqTea.LocalKey);
            if (plaintext.Length < InnerHeaderSize)
                throw new InvalidOperationException($"Plaintext length {plaintext.Length} too short.");

            var pr = new BigEndianReader(plaintext);
            var decoded = new DecodedLocalPacket
            {
                OuterSequence = outerSeq,
                RouteSequence = routeSeq,
                EnvelopeUin = uin,
                Command = pr.ReadUInt16(),
                InnerUnknown = pr.ReadUInt32(),
                InnerSequence = pr.ReadUInt16(),
                Route = pr.ReadUInt16(),
                Marker = pr.ReadUInt16(),
                SectionId = pr.ReadUInt16(),
                Payload = pr.ReadBytes(pr.Remaining)
            };
            return decoded;
        }

        /// <summary>
        /// Encodes a reliable TCP RoomFastPacket carrying gameplay 0x0FBD packages (room_fast_packet.go).
        /// </summary>
        public static byte[] EncodeRoomFastGameplayPacket(
            uint outerSeq,
            ushort routeSeq,
            uint uin,
            IReadOnlyList<ushort> recipientPlayerIds,
            IReadOnlyList<byte[]> gameplayPackageBytes,
            uint gameTimeMs)
        {
            if (recipientPlayerIds == null || recipientPlayerIds.Count == 0 || recipientPlayerIds.Count > 8)
                throw new ArgumentException("Recipient count must be 1..8.", nameof(recipientPlayerIds));
            if (gameplayPackageBytes == null || gameplayPackageBytes.Count == 0 || gameplayPackageBytes.Count > 64)
                throw new ArgumentException("Gameplay package count must be 1..64.", nameof(gameplayPackageBytes));

            var inner = new BigEndianBuffer(128);
            inner.WriteUInt16(0);      // Command = 0
            inner.WriteUInt32(0);      // InnerUnknown = 0
            inner.WriteUInt16(routeSeq);
            inner.WriteUInt16(1);      // Route = 1
            inner.WriteUInt16(0xFFFF); // Marker = 0xFFFF
            inner.WriteUInt16(1);      // SectionID = 1
            inner.WriteByte((byte)gameplayPackageBytes.Count);
            foreach (byte[] compact in gameplayPackageBytes)
            {
                inner.WriteUInt32(gameTimeMs);
                inner.WriteUInt32(0); // Flag
                inner.WriteUInt16((ushort)compact.Length);
                inner.WriteBytes(compact);
            }

            byte[] encrypted = QqTea.Encrypt(inner.ToArray(), QqTea.LocalKey);
            int stLen = 3 + 2 * recipientPlayerIds.Count;
            int total = EnvelopeSize + stLen + encrypted.Length;
            var frame = new BigEndianBuffer(total);
            frame.WriteUInt32((uint)total);
            frame.WriteUInt32(outerSeq);
            frame.WriteUInt16(routeSeq);
            frame.WriteUInt16(0xFFFF);
            frame.WriteUInt32(uin);
            frame.WriteByte(1);
            frame.WriteByte((byte)stLen);
            frame.WriteUInt16(0x0065); // roomFastTransferCommand
            frame.WriteByte((byte)recipientPlayerIds.Count);
            foreach (ushort pid in recipientPlayerIds)
                frame.WriteUInt16(pid);
            frame.WriteBytes(encrypted);
            return frame.ToArray();
        }
    }
}
