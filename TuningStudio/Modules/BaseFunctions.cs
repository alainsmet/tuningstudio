using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuningStudio.FileFormats;

namespace TuningStudio.Modules
{
    public class BaseFunc
    {
        /// <summary>
        /// Removes all white space characters from a string
        /// </summary>
        /// <param name="inputString">String to be cleaned</param>
        /// <returns>A string without any white space characters</returns>
        public static string RemoveWhiteSpaces(string inputString)
        {
            string result = inputString.Trim();
            if (result != "")
            {
                StringBuilder sb = new StringBuilder();
                foreach(char c in result) {
                    if (!Char.IsWhiteSpace(c))
                    {
                        sb.Append(c);
                    }
                }
                result = sb.ToString();
            }
            return result;
        }

        /// <summary>
        /// Splits a string based on the delimiters provided. The delimiters can be kept if needed.
        /// </summary>
        /// <param name="inputString">String to be split</param>
        /// <param name="delimiters">Characters where the string must be split</param>
        /// <param name="keepDelimiter">true if the delimiter character is kept at the beginning of the split string</param>
        /// <returns></returns>
        public static List<string> Split(string inputString, char[] delimiters, bool keepDelimiter = true)
        {
            List<string> result = new List<string>();
            StringBuilder sb = new StringBuilder();
            foreach(char c in inputString)
            {
                if (delimiters.Contains(c))
                {
                    if (sb.Length > 0)
                    {
                        result.Add(sb.ToString());
                        sb = new StringBuilder();
                    }
                    if (keepDelimiter)
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0)
            {
                result.Add(sb.ToString());
            }
            return result;
        }

        public static string StringToHexString(string inputString)
        {
            string result = "";
            if (inputString != "")
            {
                byte[] tempBytes = Encoding.UTF8.GetBytes(inputString);
                result = Convert.ToHexString(tempBytes);
            }
            return result;
        }

        /// <summary>
        /// Checks if a string contains only hexadecimal characters (0 to F).
        /// </summary>
        /// <param name="inputString">String to be checked</param>
        /// <returns>true if the string contains only hexadecimal characters, otherwise returns false</returns>
        public static bool IsHex(string inputString)
        {
            foreach (char c in inputString)
            {
                if ((c < '0') || (c > '9' & c < 'A') || (c > 'F' && c < 'a') || (c > 'f'))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Converts a hexadecimal string into a 32-bit integer
        /// </summary>
        /// <param name="inputString">Hexadecimal string to be converted</param>
        /// <returns>A 32-bit integer. Returns the minimum value if the input string can't be converted.</returns>
        public static int HexToInt(string inputString)
        {
            if (!IsHex(inputString))
            {
                return int.MinValue;
            }
            try
            {
                return Convert.ToInt32(inputString, 16);
            }
            catch
            {
                return int.MinValue;
            }
        }

        /// <summary>
        /// Converts a hexadecimal string into a 64-bit integer
        /// </summary>
        /// <param name="inputString">Hexadecimal string to be converted</param>
        /// <returns>A 64-bit integer. Returns the minimum value if the input string can't be converted.</returns>
        public static long HexToInt64(string inputString)
        {
            if (!IsHex(inputString))
            {
                return long.MinValue;
            }
            try
            {
                return Convert.ToInt64(inputString, 16);
            }
            catch
            {
                return long.MinValue;
            }
        }

        /// <summary>
        /// Converts a hexadecimal string into a byte array, taking two characters for one byte.
        /// </summary>
        /// <param name="inputString">Hexadecimal string to be converted</param>
        /// <returns>Byte array of the input hexadecimal string</returns>
        public static byte[] HexToByteArray(string inputString)
        {
            if (IsHex(inputString))
            {
                byte[] bytes = new byte[inputString.Length / 2];
                for (int i = 0; i < inputString.Length; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(inputString.Substring(i, 2), 16);
                }
                return bytes;
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Computes the checksum of a hexadecimal string and returns the result. This function is used for S-Record or Intel HEX.
        /// </summary>
        /// <param name="inputString">Hexadecimal string for which the checksum is calculated.</param>
        /// <param name="oneComplement">Using one's complement (true - S-Record) or two's complement (false - Intel HEX).</param>
        /// <param name="byteNumCks">Defines the number of bytes for the returned checksum.</param>
        /// <param name="zeroPadding">Using zero padding if the number of characters in the checksum doesn't match the desired number of bytes.</param>
        /// <returns></returns>
        public static string HexCheckSumCalc(string inputString, bool oneComplement = true, int byteNumCks = 1, bool zeroPadding = false)
        {
            string result = "";
            if (IsHex(inputString))
            {
                long sum = 0;

                for (int i = 0; i < inputString.Length; i += 2)
                {
                    sum += HexToInt64(inputString.Substring(i, 2));
                }

                if (oneComplement)
                {
                    sum = ~sum;
                }
                else
                {
                    sum = -sum;
                }

                result = sum.ToString("X");
                result = result.Substring(Math.Max(result.Length - byteNumCks*2,0), byteNumCks*2);
                if (zeroPadding)
                {
                    result = result.PadLeft(byteNumCks * 2, '0');
                }
            }
            return result;
        }
    }
    
}