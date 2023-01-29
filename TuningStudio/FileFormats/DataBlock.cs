using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuningStudio.Modules;

namespace TuningStudio.FileFormats
{
    public class DataBlock
    {
        private string _StartAddress = "";
        private string _EndAddress = "";
        private int _FileStartLine = 0;
        private int _FileEndLine = 0;
        private byte[] _RawData = Array.Empty<byte>();
        private string _RawDataString = "";
        private bool _StoreDataString = false;

        public string StartAddress { get { return _StartAddress; } set { _StartAddress = value; } }
        public string EndAddress { get { return _EndAddress; } set { _EndAddress = value; } }
        public int FileStartLine { get { return _FileStartLine; } set { _FileStartLine = value; } }
        public int FileEndLine { get { return _FileEndLine; } set { _FileEndLine = value; } }
        public bool StoreDataString { get { return _StoreDataString; } set { _StoreDataString = value; } }
        public string RawData {
            get 
            {
                if (_StoreDataString)
                {
                    return _RawDataString;
                }
                else
                {
                    if (_RawData.Length == 0)
                    {
                        return "";
                    }
                    return Convert.ToHexString(_RawData);
                }
            } 
            set
            {
                if (_StoreDataString)
                {
                    _RawDataString = value;
                }
                else
                {
                    _RawData = new byte[value.Length / 2];
                    for (int i = 0; i < value.Length; i += 2)
                    {
                        _RawData[i / 2] = Convert.ToByte(value.Substring(i, 2), 16);
                    }
                }
            }
        }

        public DataBlock()
        {
            StartAddress = "";
            EndAddress = "";
            FileStartLine = 0;
            FileEndLine = 0;
            RawData = "";
        }

        public DataBlock(string startAddress = "", string endAddress = "", int fileStartLine = 0, int fileEndLine = 0, string rawData = "", bool dataString = false)
        {
            StartAddress = startAddress;
            EndAddress = endAddress;
            FileStartLine = fileStartLine;
            FileEndLine = fileEndLine;
            StoreDataString = dataString;
            RawData = rawData;
        }

        /// <summary>
        /// Returns the block's length in byte
        /// </summary>
        /// <returns>Length in byte</returns>
        public long Length()
        {
            return BaseFunc.HexToInt64(EndAddress) - BaseFunc.HexToInt64(StartAddress) + 1;
        }

        public void ModifyData(string startAddress, string data)
        {
            if (_StoreDataString)
            {
                if (_RawDataString != String.Empty)
                {
                    int startPosition = Convert.ToInt32(BaseFunc.HexToInt64(startAddress) - BaseFunc.HexToInt64(_StartAddress));
                    StringBuilder sb = new StringBuilder();
                    sb.Append(_RawDataString.Substring(0, startPosition * 2));
                    sb.Append(data);
                    sb.Append(_RawDataString.Substring(startPosition * 2 + data.Length));
                    _RawDataString = sb.ToString();
                }
            }
            else
            {
                if (_RawData.Length > 0)
                {
                    int startPosition = Convert.ToInt32(BaseFunc.HexToInt64(startAddress) - BaseFunc.HexToInt64(_StartAddress));
                    for (int i = 0; i < data.Length; i += 2)
                    {
                        _RawData[startPosition + i/2] = Convert.ToByte(data.Substring(i, 2), 16);
                    }
                }
            }
        }

    }
}
