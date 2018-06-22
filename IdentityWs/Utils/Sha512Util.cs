using System;
using System.Security.Cryptography;
using System.Text;

namespace IdentityWs.Utils
{
    // Methods to compute and check crypt()-style SHA512 hashes. The hashes will start with "$6$"
    // and are the kind of thing one might find in /etc/shadow on a Unix system. The default number
    // of rounds - 5000 - is always used. The overall structure and logic is taken from PHP's
    // implementation in crypt_sha512.c, although I use .NET Core's scantily documented SHA512
    // object for the actual transformations.
    public static class Sha512Util
    {
        const string SHA512_SALT_PREFIX = "$6$";
        // Must be less than HASH_LENGTH_BYTES.
        const int SALT_LEN_MAX = 16;
        // The length of our generated salts.
        const int SALT_LEN_DEFAULT = 8;
        // This is the default for the algorithm and we always use it.
        const int ROUNDS_DEFAULT = 5000;
        // Table of characters for our bespoke base64 transformation.
        const string B64T = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        const int HASH_LENGTH_BYTES = 512 / 8;

        static RandomNumberGenerator random = RandomNumberGenerator.Create();

        public static string Crypt(string key, string salt)
        {
            if (!salt.StartsWith(SHA512_SALT_PREFIX))
                throw new ArgumentException($"This crypt() implementation supports only SHA512, so the salt must start with '{SHA512_SALT_PREFIX}'");
            if (salt.Length > SALT_LEN_MAX)
                throw new ArgumentException($"The salt exceeds the maximum length of {SALT_LEN_MAX}");
            if (key.Length == 0)
                throw new ArgumentException("No key supplied");
            if (salt.Length == 0)
                throw new ArgumentException("No salt supplied");

            byte[] key_bytes = Encoding.UTF8.GetBytes(key);
            byte[] salt_bytes = Encoding.UTF8.GetBytes(salt.Substring(SHA512_SALT_PREFIX.Length));

            byte[] p_bytes = new byte[key_bytes.Length], s_bytes = new byte[salt_bytes.Length];

            byte[] alt_result, temp_result;
            SHA512 alt_ctx, ctx;
            int cnt;

            using (ctx = SHA512.Create()) {
                ctx.TransformBlock(key_bytes, 0, key_bytes.Length, key_bytes, 0);
                ctx.TransformBlock(salt_bytes, 0, salt_bytes.Length, salt_bytes, 0);

                // Compute alternate SHA512 sum with input KEY, SALT, and KEY. The final result
                // will be added to 'ctx'.
                using (alt_ctx = SHA512.Create()) {
                    alt_ctx.TransformBlock(key_bytes, 0, key_bytes.Length, key_bytes, 0);
                    alt_ctx.TransformBlock(salt_bytes, 0, salt_bytes.Length, salt_bytes, 0);
                    alt_ctx.TransformFinalBlock(key_bytes, 0, key_bytes.Length);
                    alt_result = alt_ctx.Hash;

                    // For each character in the key add one byte of the alternate sum.
                    for (cnt = key_bytes.Length; cnt > HASH_LENGTH_BYTES; cnt -= HASH_LENGTH_BYTES)
                        ctx.TransformBlock(alt_result, 0, HASH_LENGTH_BYTES, alt_result, 0);
                    ctx.TransformBlock(alt_result, 0, cnt, alt_result, 0);

                    // Take the binary representation of the length of the key and for each 1 add
                    // the alternate sum, for each 0 the key.
                    for (cnt = key_bytes.Length; cnt > 0; cnt >>= 1) {
                        if ((cnt & 1) != 0)
                            ctx.TransformBlock(alt_result, 0, 64, null, 0);
                        else
                            ctx.TransformBlock(key_bytes, 0, key_bytes.Length, null, 0);
                    }

                    // Create intermediate result.
                    ctx.TransformFinalBlock(alt_result, 0, 0);
                    alt_result = ctx.Hash;
                }
            }

            // Start computation of P byte sequence.
            using (alt_ctx = SHA512.Create()) {
                // For every character in the password add the entire password.
                for (cnt = 0; cnt < key_bytes.Length - 1; ++cnt)
                    alt_ctx.TransformBlock(key_bytes, 0, key_bytes.Length, key_bytes, 0);

                // Finish the digest.
                alt_ctx.TransformFinalBlock(key_bytes, 0, key_bytes.Length);
                temp_result = alt_ctx.Hash;
            }

            // Create byte sequence P.
            for (int idx = 0; idx < key_bytes.Length; idx += HASH_LENGTH_BYTES)
                Array.Copy(temp_result, 0, p_bytes, idx, Math.Min(key_bytes.Length - idx, HASH_LENGTH_BYTES));
            
            // Start computation of S byte sequence.
            using (alt_ctx = SHA512.Create()) {
                // Do something (?) with the salt.
                for (cnt = 0; cnt < 16 + alt_result[0] - 1; ++cnt)
                    alt_ctx.TransformBlock(salt_bytes, 0, salt_bytes.Length, salt_bytes, 0);
                
                // Finish the digest.
                alt_ctx.TransformFinalBlock(salt_bytes, 0, salt_bytes.Length);
                temp_result = alt_ctx.Hash;
            }

            // Create byte sequence S.
            Array.Copy(temp_result, s_bytes, salt_bytes.Length);

            // Repeatedly run the collected hash value through SHA512 to burn CPU cycles.
            for (cnt = 0; cnt < ROUNDS_DEFAULT; ++cnt) {
                using (ctx = SHA512.Create()) {
                    // Add key or last result.
                    if ((cnt & 1) != 0)
                        ctx.TransformBlock(p_bytes, 0, p_bytes.Length, p_bytes, 0);
                    else
                        ctx.TransformBlock(alt_result, 0, HASH_LENGTH_BYTES, alt_result, 0);

                    // Add salt for numbers not divisible by 3.
                    if (cnt % 3 != 0)
                        ctx.TransformBlock(s_bytes, 0, s_bytes.Length, s_bytes, 0);

                    // Add key for numbers not divisible by 7.
                    if (cnt % 7 != 0)
                        ctx.TransformBlock(p_bytes, 0, p_bytes.Length, p_bytes, 0);

                    // Add key or last result.
                    if ((cnt & 1) != 0)
                        ctx.TransformFinalBlock(alt_result, 0, HASH_LENGTH_BYTES);
                    else
                        ctx.TransformFinalBlock(p_bytes, 0, p_bytes.Length);

                    // Create intermediate result.
                    alt_result = ctx.Hash;
                }
            }

            // Construct the final result string.
            StringBuilder result = new StringBuilder();
            result.Append(salt);
            result.Append('$');
            result.Append(Base64From24Bits(alt_result[0], alt_result[21], alt_result[42], 4));
            result.Append(Base64From24Bits(alt_result[22], alt_result[43], alt_result[1], 4));
            result.Append(Base64From24Bits(alt_result[44], alt_result[2], alt_result[23], 4));
            result.Append(Base64From24Bits(alt_result[3], alt_result[24], alt_result[45], 4));
            result.Append(Base64From24Bits(alt_result[25], alt_result[46], alt_result[4], 4));
            result.Append(Base64From24Bits(alt_result[47], alt_result[5], alt_result[26], 4));
            result.Append(Base64From24Bits(alt_result[6], alt_result[27], alt_result[48], 4));
            result.Append(Base64From24Bits(alt_result[28], alt_result[49], alt_result[7], 4));
            result.Append(Base64From24Bits(alt_result[50], alt_result[8], alt_result[29], 4));
            result.Append(Base64From24Bits(alt_result[9], alt_result[30], alt_result[51], 4));
            result.Append(Base64From24Bits(alt_result[31], alt_result[52], alt_result[10], 4));
            result.Append(Base64From24Bits(alt_result[53], alt_result[11], alt_result[32], 4));
            result.Append(Base64From24Bits(alt_result[12], alt_result[33], alt_result[54], 4));
            result.Append(Base64From24Bits(alt_result[34], alt_result[55], alt_result[13], 4));
            result.Append(Base64From24Bits(alt_result[56], alt_result[14], alt_result[35], 4));
            result.Append(Base64From24Bits(alt_result[15], alt_result[36], alt_result[57], 4));
            result.Append(Base64From24Bits(alt_result[37], alt_result[58], alt_result[16], 4));
            result.Append(Base64From24Bits(alt_result[59], alt_result[17], alt_result[38], 4));
            result.Append(Base64From24Bits(alt_result[18], alt_result[39], alt_result[60], 4));
            result.Append(Base64From24Bits(alt_result[40], alt_result[61], alt_result[19], 4));
            result.Append(Base64From24Bits(alt_result[62], alt_result[20], alt_result[41], 4));
            result.Append(Base64From24Bits(0, 0, alt_result[63], 2));
            return result.ToString();
        }

        // Encode the given password for saving in the database.
        public static string SaltAndHashNewPassword(string password)
        {
            // Generate a random salt.
            byte[] bitey = new byte[SALT_LEN_DEFAULT];
            char[] chary = new char[SALT_LEN_DEFAULT];
            random.GetBytes(bitey);
            for (int i = 0; i < SALT_LEN_DEFAULT; i++)
                chary[i] = B64T[(int)(bitey[i] & 0x3f)];

            return Crypt(password, SHA512_SALT_PREFIX + new string(chary));
        }

        // Test whether the plaintext password provided matches the given hash. The hash may have
        // been generated by SaltAndHashNewPassword() above.
        public static bool TestPassword(string password, string hash) {
            if (hash.Length <= SHA512_SALT_PREFIX.Length)
                throw new ArgumentException("The provided hash is too short");
            int salt_len = hash.IndexOf('$', SHA512_SALT_PREFIX.Length);
            if (salt_len < 0)
                throw new ArgumentException("The salt is unterminated");
            return hash == Crypt(password, hash.Substring(0, salt_len));
        }

        static char[] Base64From24Bits(byte b2, byte b1, byte b0, int n)
        {
            char[] result = new char[n];
            uint w = unchecked((uint)((b2 << 16) | (b1 << 8) | b0));
            for (int i = 0; i < n; i++) {
                result[i] = B64T[(int)(w & 0x3f)];
                w >>= 6;
            }
            return result;
        }
    }
}
