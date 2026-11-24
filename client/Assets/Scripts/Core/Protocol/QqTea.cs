using System;
using System.Security.Cryptography;

namespace Qqtang.Core.Protocol
{
    /// <summary>
    /// Byte-identical port of server/internal/protocol/qqtea/qqtea.go (16-round Tencent QQ-TEA).
    /// </summary>
    public static class QqTea
    {
        private const int BlockSize = 8;
        private const uint Delta = 0x9E3779B9u;
        private const int Rounds = 16;

        public static readonly byte[] LocalKey =
        {
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
            0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F
        };

        public static readonly byte[] LocalServiceTicket =
        {
            0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
            0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2E, 0x2F,
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
            0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F
        };

        public static readonly byte[] QqtPppGameplayKey = System.Text.Encoding.ASCII.GetBytes("qqt is the best!");

        public static byte[] Encrypt(byte[] plaintext, byte[] key)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            int padding = (BlockSize - ((plaintext.Length + 10) % BlockSize)) % BlockSize;
            byte[] randomPrefix = new byte[padding + 3];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomPrefix);
            }
            return EncryptWithPrefix(plaintext, key, randomPrefix);
        }

        public static byte[] EncryptWithPrefix(byte[] plaintext, byte[] key, byte[] randomPrefix)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            if (key == null || key.Length != 16)
                throw new ArgumentException("QQ-TEA key length must be 16.", nameof(key));

            int padding = (BlockSize - ((plaintext.Length + 10) % BlockSize)) % BlockSize;
            if (randomPrefix == null || randomPrefix.Length < padding + 3)
                throw new ArgumentException($"Random prefix must have at least {padding + 3} bytes.", nameof(randomPrefix));

            byte[] formatted = new byte[1 + padding + 2 + plaintext.Length + 7];
            formatted[0] = (byte)((randomPrefix[0] & 0xF8) | (padding & 0x07));
            Buffer.BlockCopy(randomPrefix, 1, formatted, 1, padding + 2);
            Buffer.BlockCopy(plaintext, 0, formatted, padding + 3, plaintext.Length);

            byte[] ciphertext = new byte[formatted.Length];
            byte[] prevCipher = new byte[BlockSize];
            byte[] prevIntermediate = new byte[BlockSize];
            byte[] intermediate = new byte[BlockSize];
            byte[] encrypted = new byte[BlockSize];

            for (int offset = 0; offset < formatted.Length; offset += BlockSize)
            {
                for (int i = 0; i < BlockSize; i++)
                    intermediate[i] = (byte)(formatted[offset + i] ^ prevCipher[i]);

                EncryptBlock(intermediate, key, encrypted);

                for (int i = 0; i < BlockSize; i++)
                    ciphertext[offset + i] = (byte)(encrypted[i] ^ prevIntermediate[i]);

                Buffer.BlockCopy(intermediate, 0, prevIntermediate, 0, BlockSize);
                Buffer.BlockCopy(ciphertext, offset, prevCipher, 0, BlockSize);
            }

            return ciphertext;
        }

        public static byte[] Decrypt(byte[] ciphertext, byte[] key)
        {
            return Decrypt(ciphertext, 0, ciphertext?.Length ?? 0, key);
        }

        public static byte[] Decrypt(byte[] ciphertext, int startOffset, int length, byte[] key)
        {
            if (ciphertext == null) throw new ArgumentNullException(nameof(ciphertext));
            if (key == null || key.Length != 16)
                throw new ArgumentException("QQ-TEA key length must be 16.", nameof(key));
            if (length < 16 || (length % BlockSize) != 0 || startOffset < 0 || startOffset + length > ciphertext.Length)
                throw new ArgumentException($"QQ-TEA ciphertext length {length} must be >= 16 and a multiple of 8.");

            byte[] formatted = new byte[length];
            byte[] prevCipher = new byte[BlockSize];
            byte[] prevPlain = new byte[BlockSize];
            byte[] mixed = new byte[BlockSize];
            byte[] plainBlock = new byte[BlockSize];

            for (int offset = 0; offset < length; offset += BlockSize)
            {
                for (int i = 0; i < BlockSize; i++)
                    mixed[i] = (byte)(ciphertext[startOffset + offset + i] ^ prevPlain[i]);

                DecryptBlock(mixed, key, plainBlock);

                for (int i = 0; i < BlockSize; i++)
                    formatted[offset + i] = (byte)(plainBlock[i] ^ prevCipher[i]);

                Buffer.BlockCopy(plainBlock, 0, prevPlain, 0, BlockSize);
                Buffer.BlockCopy(ciphertext, startOffset + offset, prevCipher, 0, BlockSize);
            }

            int padding = formatted[0] & 0x07;
            int start = 1 + padding + 2;
            int end = formatted.Length - 7;
            if (start > end)
                throw new InvalidOperationException($"QQ-TEA padding {padding} exceeds formatted plaintext.");

            for (int i = end; i < formatted.Length; i++)
            {
                if (formatted[i] != 0)
                    throw new InvalidOperationException($"QQ-TEA trailing zero validation failed at {i}.");
            }

            byte[] result = new byte[end - start];
            Buffer.BlockCopy(formatted, start, result, 0, result.Length);
            return result;
        }

        private static void EncryptBlock(byte[] block, byte[] key, byte[] output)
        {
            uint left = ReadUInt32BE(block, 0);
            uint right = ReadUInt32BE(block, 4);
            uint k0 = ReadUInt32BE(key, 0);
            uint k1 = ReadUInt32BE(key, 4);
            uint k2 = ReadUInt32BE(key, 8);
            uint k3 = ReadUInt32BE(key, 12);
            uint sum = 0;
            unchecked
            {
                for (int r = 0; r < Rounds; r++)
                {
                    sum += Delta;
                    left += ((right << 4) + k0) ^ (right + sum) ^ ((right >> 5) + k1);
                    right += ((left << 4) + k2) ^ (left + sum) ^ ((left >> 5) + k3);
                }
            }
            WriteUInt32BE(output, 0, left);
            WriteUInt32BE(output, 4, right);
        }

        private static void DecryptBlock(byte[] block, byte[] key, byte[] output)
        {
            uint left = ReadUInt32BE(block, 0);
            uint right = ReadUInt32BE(block, 4);
            uint k0 = ReadUInt32BE(key, 0);
            uint k1 = ReadUInt32BE(key, 4);
            uint k2 = ReadUInt32BE(key, 8);
            uint k3 = ReadUInt32BE(key, 12);
            uint sum = 0xE3779B90u; // Delta * 16 mod 2^32
            unchecked
            {
                for (int r = 0; r < Rounds; r++)
                {
                    right -= ((left << 4) + k2) ^ (left + sum) ^ ((left >> 5) + k3);
                    left -= ((right << 4) + k0) ^ (right + sum) ^ ((right >> 5) + k1);
                    sum -= Delta;
                }
            }
            WriteUInt32BE(output, 0, left);
            WriteUInt32BE(output, 4, right);
        }

        internal static uint ReadUInt32BE(byte[] buf, int offset) =>
            ((uint)buf[offset] << 24) |
            ((uint)buf[offset + 1] << 16) |
            ((uint)buf[offset + 2] << 8) |
            buf[offset + 3];

        internal static void WriteUInt32BE(byte[] buf, int offset, uint val)
        {
            buf[offset] = (byte)(val >> 24);
            buf[offset + 1] = (byte)(val >> 16);
            buf[offset + 2] = (byte)(val >> 8);
            buf[offset + 3] = (byte)val;
        }
    }
}
