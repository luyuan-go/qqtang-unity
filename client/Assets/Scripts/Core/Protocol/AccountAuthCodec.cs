using System;
using System.Security.Cryptography;
using System.Text;

namespace Qqtang.Core.Protocol
{
    public enum AccountAuthMessageType : byte
    {
        Hello = 1,
        Challenge = 2,
        Proof = 3,
        Result = 4
    }

    public sealed class AccountAuthMessage
    {
        public AccountAuthMessageType Type;
        public uint Uin;
        public uint Iterations;
        public byte[] Salt;
        public byte[] Nonce;
        public byte[] Proof;
        public bool Accepted;
        public string Reason = string.Empty;
        public string Nickname = string.Empty;
        public byte Gender;
    }

    /// <summary>
    /// Byte-identical port of server/internal/accountauth (QQTAUTH1 challenge-response).
    /// </summary>
    public static class AccountAuthCodec
    {
        public const int SaltMinSize = 16;
        public const int KeySize = 32;
        public const int NonceSize = 32;

        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("QQTAUTH1");
        private static readonly byte[] ClientKeyLabel = Encoding.ASCII.GetBytes("Client Key");
        private static readonly byte[] AuthSignaturePrefix = Encoding.ASCII.GetBytes("QQTang local account login\0");

        public static bool IsProtocolFrame(byte[] frame)
        {
            if (frame == null || frame.Length < 13) return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (frame[4 + i] != Magic[i]) return false;
            }
            return true;
        }

        public static byte[] MarshalHello(uint uin)
        {
            if (uin == 0) throw new ArgumentException("UIN must be non-zero.", nameof(uin));
            var w = new BigEndianBuffer(17);
            w.WriteUInt32(17);
            w.WriteBytes(Magic);
            w.WriteByte((byte)AccountAuthMessageType.Hello);
            w.WriteUInt32(uin);
            return w.ToArray();
        }

        public static byte[] MarshalProof(uint uin, byte[] proof)
        {
            if (uin == 0) throw new ArgumentException("UIN must be non-zero.", nameof(uin));
            if (proof == null || proof.Length != KeySize)
                throw new ArgumentException($"Proof length must be {KeySize}.", nameof(proof));
            var w = new BigEndianBuffer(49);
            w.WriteUInt32(49);
            w.WriteBytes(Magic);
            w.WriteByte((byte)AccountAuthMessageType.Proof);
            w.WriteUInt32(uin);
            w.WriteBytes(proof);
            return w.ToArray();
        }

        public static AccountAuthMessage Unmarshal(byte[] frame)
        {
            if (frame == null || frame.Length < 13 || !IsProtocolFrame(frame))
                throw new InvalidOperationException("Invalid QQTAUTH1 frame.");

            uint declared = QqTea.ReadUInt32BE(frame, 0);
            if (declared != frame.Length)
                throw new InvalidOperationException($"QQTAUTH1 declared length {declared} != {frame.Length}.");

            var msg = new AccountAuthMessage { Type = (AccountAuthMessageType)frame[12] };
            int offset = 13;
            int remaining = frame.Length - offset;

            switch (msg.Type)
            {
                case AccountAuthMessageType.Hello:
                    if (remaining != 4) throw new InvalidOperationException("Invalid Hello length.");
                    msg.Uin = QqTea.ReadUInt32BE(frame, offset);
                    return msg;

                case AccountAuthMessageType.Challenge:
                    if (remaining < 9 + NonceSize) throw new InvalidOperationException("Invalid Challenge length.");
                    msg.Uin = QqTea.ReadUInt32BE(frame, offset);
                    msg.Iterations = QqTea.ReadUInt32BE(frame, offset + 4);
                    int saltLen = frame[offset + 8];
                    if (remaining != 9 + saltLen + NonceSize)
                        throw new InvalidOperationException("Challenge salt/nonce length mismatch.");
                    msg.Salt = new byte[saltLen];
                    Buffer.BlockCopy(frame, offset + 9, msg.Salt, 0, saltLen);
                    msg.Nonce = new byte[NonceSize];
                    Buffer.BlockCopy(frame, offset + 9 + saltLen, msg.Nonce, 0, NonceSize);
                    return msg;

                case AccountAuthMessageType.Proof:
                    if (remaining != 4 + KeySize) throw new InvalidOperationException("Invalid Proof length.");
                    msg.Uin = QqTea.ReadUInt32BE(frame, offset);
                    msg.Proof = new byte[KeySize];
                    Buffer.BlockCopy(frame, offset + 4, msg.Proof, 0, KeySize);
                    return msg;

                case AccountAuthMessageType.Result:
                    if (remaining < 2) throw new InvalidOperationException("Invalid Result length.");
                    msg.Accepted = frame[offset] == 1;
                    int reasonLen = frame[offset + 1];
                    if (remaining < 2 + reasonLen) throw new InvalidOperationException("Truncated Result reason.");
                    msg.Reason = Encoding.UTF8.GetString(frame, offset + 2, reasonLen);
                    int profileOff = offset + 2 + reasonLen;
                    int profileRem = frame.Length - profileOff;
                    if (profileRem > 0)
                    {
                        if (profileRem < 2 || profileRem != 2 + frame[profileOff + 1])
                            throw new InvalidOperationException("Invalid Result profile tail.");
                        msg.Gender = frame[profileOff];
                        msg.Nickname = Encoding.UTF8.GetString(frame, profileOff + 2, frame[profileOff + 1]);
                    }
                    return msg;

                default:
                    throw new InvalidOperationException($"Unknown QQTAUTH1 message type {msg.Type}.");
            }
        }

        /// <summary>
        /// Exact PBKDF2-HMAC-SHA256 1-block derivation matching Go accountauth.DeriveKey.
        /// </summary>
        public static byte[] DeriveKey(byte[] password, byte[] salt, uint iterations)
        {
            using (var hmac = new HMACSHA256(password))
            {
                byte[] block = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, block, 0, salt.Length);
                block[salt.Length + 3] = 1;

                byte[] u = hmac.ComputeHash(block);
                byte[] result = new byte[u.Length];
                Buffer.BlockCopy(u, 0, result, 0, u.Length);

                for (uint round = 1; round < iterations; round++)
                {
                    u = hmac.ComputeHash(u);
                    for (int i = 0; i < result.Length; i++)
                        result[i] ^= u[i];
                }
                return result;
            }
        }

        public static byte[] ComputeClientProof(string password, byte[] salt, uint iterations, byte[] nonce, uint uin)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6 || password.Length > 16)
                throw new ArgumentException("Password must be 6-16 ASCII characters.", nameof(password));
            byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
            byte[] saltedPassword = DeriveKey(passwordBytes, salt, iterations);

            byte[] clientKey;
            using (var hmac = new HMACSHA256(saltedPassword))
            {
                clientKey = hmac.ComputeHash(ClientKeyLabel);
            }

            byte[] storedKey;
            using (var sha = SHA256.Create())
            {
                storedKey = sha.ComputeHash(clientKey);
            }

            byte[] signature;
            using (var hmac = new HMACSHA256(storedKey))
            {
                byte[] msg = new byte[AuthSignaturePrefix.Length + 4 + nonce.Length];
                Buffer.BlockCopy(AuthSignaturePrefix, 0, msg, 0, AuthSignaturePrefix.Length);
                QqTea.WriteUInt32BE(msg, AuthSignaturePrefix.Length, uin);
                Buffer.BlockCopy(nonce, 0, msg, AuthSignaturePrefix.Length + 4, nonce.Length);
                signature = hmac.ComputeHash(msg);
            }

            byte[] proof = new byte[KeySize];
            for (int i = 0; i < KeySize; i++)
                proof[i] = (byte)(clientKey[i] ^ signature[i]);

            Array.Clear(passwordBytes, 0, passwordBytes.Length);
            Array.Clear(saltedPassword, 0, saltedPassword.Length);
            Array.Clear(clientKey, 0, clientKey.Length);
            Array.Clear(signature, 0, signature.Length);
            return proof;
        }
    }
}
