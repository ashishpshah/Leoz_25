using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leoz_25.Infra
{
    public static class SecureEncryption
    {
        public static string Encryptdata(string input)
        {
            string strmsg = string.Empty;
            byte[] encode = new byte[input.Length];
            encode = Encoding.UTF8.GetBytes(input);
            strmsg = Convert.ToBase64String(encode);
            return strmsg;
        }
        public static string Decryptdata(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                string decryptpwd = string.Empty;
                UTF8Encoding encodepwd = new UTF8Encoding();
                Decoder Decode = encodepwd.GetDecoder();
                byte[] todecode_byte = Convert.FromBase64String(input.Replace(' ', '+'));
                int charCount = Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                decryptpwd = new String(decoded_char);
                return decryptpwd;
            }
            return "";
        }
    }
}
