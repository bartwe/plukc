using System;
using System.Text;
using System.Globalization;

namespace Compiler
{
    public sealed class HexUtility
    {
        private HexUtility()
        {
        }

        public static byte[] GetBytes(string hex)
        {
            if (hex == null)
                throw new ArgumentNullException("hex");
            Require.True((hex.Length & 1) == 0);
            byte[] result = new byte[hex.Length / 2];

            for (int i = 0; i < result.Length; ++i)
            {
                byte c = byte.Parse(hex.Substring(i * 2, 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                byte d = byte.Parse(hex.Substring(i * 2 + 1, 1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                result[i] = (byte)((c << 4) + d);
            }
            return result;
        }
    }
}