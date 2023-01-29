using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using TuningStudio.Modules;
using System.Xml.Linq;
using System.Windows.Shapes;
using System.Configuration;

namespace TuningStudio.FileFormats
{
    /// <summary>
    /// Class to read and store Motorola S-Record data.
    /// </summary>
    public class SRecord
    {
        /// <summary>
        /// Error code while reading the S-Record file.
        /// </summary>
        public enum ErrorCode
        {
            None,                           // No error
            NotStartingWithSartCode,        // The line is not starting with the uppercase letter S
            UnknownRecordType,              // Record type not in the known list (S0 to S9)
            LowerThanMinimumLength,         // The line length is lower than the minimum expected (10 characters)
            LengthMismatch,                 // Inconsistency between the bytes number declared and the bytes read
            RawDataNotHex,                  // Some data is not compliant with hexidecimal notation (0 - F)
            ChecksumMismatch,               // The checksum declared doesn't correspond to the one computed
            DataLinesCountMismatch          // The number of data lines (S1/S2/S3) doesn't match with the one calculated
        }

        public enum RecordType
        {
            Header,
            Data,
            LinesCount,
            Termination,
            ExtendedSegmentAddress,
            StartSegmentAddress,
            ExtendedLinearAddress,
            StartLinearAddress,
            Reserved,
            Other
        }

        protected static char _StartCode;
        protected static int _ByteCountPosition;
        protected static int _ByteCountLength;
        protected static int _RecordTypePosition;
        protected static int _RecordTypeLength;
        protected static int _AddressPosition;
        protected static int _ChecksumLength;
        protected static bool _OneComplementCks;
        protected static int _MinLength;
        protected static Dictionary<string, int> _AddressLength = new Dictionary<string, int>();
        protected static List<string> _ElementsCalcCks = new List<string>();
        protected static List<string> _ElementsOrder = new List<string>();

        protected string _FileName = "";
        protected DateTime _FileLastModificationDate;
        protected Dictionary<int, ErrorCode> _Errors = new Dictionary<int, ErrorCode>();
        protected List<DataBlock> _RawDataBlocks = new List<DataBlock>();
        protected List<DataBlock> _DataBlocks = new List<DataBlock>();
        protected bool _HasHeader = false;
        protected string _HeaderData = "";
        protected bool _LoadData = false;
        protected int _DataLinesCount = 0;
        protected bool _StoreDataString = false;
     
        public string FileName { get { return _FileName; } set { _FileName = value; } }
        public Dictionary<int,ErrorCode> Errors { get { return _Errors; } }
        public List<DataBlock> RawDataBlocks { get { return _RawDataBlocks; } }
        public List <DataBlock> DataBlocks { get { return _DataBlocks; } }
        public int DataLinesCount { get { return _DataLinesCount; } }
        public bool HasHeader { get { return _HasHeader; } }
        public string HeaderData { get { return _HeaderData; } set { _HeaderData = value; } }
        public bool LoadData { get { return _LoadData; } set { _LoadData = value; } }
        public bool StoreDataString { get { return _StoreDataString; } set { _StoreDataString = value; } }

        /// <summary>
        /// Creates a new empty instance of a S-Record file.
        /// </summary>
        public SRecord()
        {
            FileName = "";
            LoadData = false;

            ResetValues();
            SetFormatValues();
        }

        /// <summary>
        /// Creates a new instance of a S-Record file
        /// </summary>
        /// <param name="fileName">The full file name and path to be read.</param>
        public SRecord(string fileName, bool loadData = false)
        {
            FileName = fileName;
            LoadData = loadData;

            ResetValues();
            SetFormatValues();
        }

        /// <summary>
        /// Defines specific values for the S-Record data format.
        /// </summary>
        protected virtual void SetFormatValues()
        {
            _StartCode = 'S';
            _ByteCountPosition = 2;
            _ByteCountLength = 2;
            _RecordTypePosition = 1;
            _RecordTypeLength = 1;
            _AddressPosition = 4;
            _ChecksumLength = 2;
            _OneComplementCks = true;
            _MinLength = 10;
            _AddressLength = new Dictionary<string, int>()
            {
                {"0",4},{"1",4},{"2",6},{"3",8},{"5",4},{"6",6},{"7",8},{"8",6},{"9",4}
            };
            _ElementsCalcCks = new List<string>
            {
                "byteCount", "address", "data"
            };
            _ElementsOrder = new List<string>
            {
                "type", "byteCount", "address", "data", "checksum"
            };

        }

        /// <summary>
        /// Resets values of the object (for first initialisation or new reading of the file).
        /// </summary>
        protected void ResetValues()
        {
            _Errors = new Dictionary<int, ErrorCode>();
            _HasHeader = false;
            _DataLinesCount = 0;
            _RawDataBlocks = new List<DataBlock>();
            _DataBlocks = new List<DataBlock>();
        }

        /// <summary>
        /// Checks if a text line is compliant with the current data format.
        /// </summary>
        /// <param name="line">Text line to be checked as string.</param>
        /// <returns>ErrorCode.None if no errors have been found, otherwise returns the first error code found.</returns>
        public ErrorCode CheckLine(string line)
        {
            if (!line.StartsWith(_StartCode))
            {
                return ErrorCode.NotStartingWithSartCode;
            }
            if (line.Length < _MinLength)
            {
                return ErrorCode.LowerThanMinimumLength;
            }
            if (!BaseFunc.IsHex(line.Substring(1)))
            {
                return ErrorCode.RawDataNotHex;
            }
            try 
            {
                int recordType = Convert.ToInt32(line.Substring(_RecordTypePosition, _RecordTypeLength));
            }
            catch
            {
                return ErrorCode.UnknownRecordType;
            }

            string byteCountData = ExtractByteCountData(line);
            int byteCount = BaseFunc.HexToInt(line.Substring(_ByteCountPosition,_ByteCountLength));

            if (byteCount != byteCountData.Length / 2)
            {
                return ErrorCode.LengthMismatch;
            }
            return ErrorCode.None;
        }

        /// <summary>
        /// Provides the hexadecimal string of the bytes taken into account for the byte count value of the record.
        /// </summary>
        /// <param name="inputString">Complete record line as string.</param>
        /// <returns>Hexadecimal string with bytes taken into account for the byte count value of the record.</returns>
        public virtual string ExtractByteCountData(string inputString)
        {
            if (inputString != String.Empty)
            {
                return inputString.Substring(_ByteCountPosition + _ByteCountLength);
            }
            return "";
        }

        /// <summary>
        /// Isolates the hexadecimal data values from a record line.
        /// </summary>
        /// <param name="inputString">Complete record line as string.</param>
        /// <param name="recordType">Hexadecimal data as string.</param>
        /// <returns></returns>
        public virtual string ExtractData(string inputString, string recordType)
        {
            int dataLength = inputString.Length - 1 - _RecordTypeLength - _ByteCountLength - _AddressLength[recordType] - _ChecksumLength;
            if (dataLength > 0)
            {
                return inputString.Substring(_AddressPosition + _AddressLength[recordType], dataLength);
            }
            return "";
        }

        /// <summary>
        /// Parses and isolates all the elements from a record line.
        /// </summary>
        /// <param name="line">Complete record line as string.</param>
        /// <returns>Dictionary with all elements as strings.</returns>
        public Dictionary<string, string> ParseLine(string line)
        { 
            Dictionary<string, string> result = new Dictionary<string, string>();
            result.Add("type", line.Substring(_RecordTypePosition, _RecordTypeLength));
            result.Add("byteCount", line.Substring(_ByteCountPosition, _ByteCountLength));
            result.Add("byteCountInt", BaseFunc.HexToInt(result["byteCount"]).ToString());
            result.Add("addressLength", _AddressLength[result["type"]].ToString());
            result.Add("address", line.Substring(_AddressPosition, _AddressLength[result["type"]]));
            result.Add("addressLong", BaseFunc.HexToInt64(result["address"]).ToString());
            result.Add("data", ExtractData(line, result["type"]));
            result.Add("checksum", line.Substring(line.Length - _ChecksumLength));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _ElementsCalcCks.Count; i++)
            {
                sb.Append(result[_ElementsCalcCks[i]]);
            }
            result.Add("checksumCalc", BaseFunc.HexCheckSumCalc(sb.ToString(), _OneComplementCks));
            return result;
        }

        /// <summary>
        /// Provides the type of record from the record type value.
        /// </summary>
        /// <param name="type">Type value as string.</param>
        /// <returns>Record type as enumeration.</returns>
        public virtual RecordType GetRecordType(string type)
        {
            if (type == "0")
            {
                return RecordType.Header;
            }
            if (type == "5" || type == "6")
            {
                return RecordType.LinesCount;
            }
            if (type == "1" || type == "2" || type == "3")
            {
                return RecordType.Data;
            }
            if (type == "4")
            {
                return RecordType.Reserved;
            }
            return RecordType.Other;
        }

        /// <summary>
        /// Reads the entire SRecord file and stores the processed values.
        /// </summary>
        /// <returns>Returns true if the read is succesful, otherwise returns false.</returns>
        public virtual bool Read()
        {
            if (File.Exists(FileName))
            {
                ResetValues();
                _FileLastModificationDate = File.GetLastWriteTimeUtc(FileName);
                StreamReader sr = new StreamReader(FileName);
                string newFileLine = sr.ReadLine();
                int lineNumber = 0;
                DataBlock newBlock = new DataBlock(dataString: StoreDataString);
                long dataBlockCurrentAddress = -1;
                StringBuilder rawDataSb = new StringBuilder(); 

                while (newFileLine != null)
                {
                    newFileLine = BaseFunc.RemoveWhiteSpaces(newFileLine);
                    List<string> recordLines = BaseFunc.Split(newFileLine, new char[] {_StartCode} ); // Allowing several records to be stored on a single file line. Not 100% compliant for instance with S-Record.

                    if (recordLines.Count > 0)
                    {
                        foreach(string newLine in recordLines)
                        {
                            lineNumber++;
                            ErrorCode ec = CheckLine(newLine);

                            if (ec == ErrorCode.None)
                            {
                                Dictionary<string, string> newRecord = ParseLine(newLine);
                                if (newRecord["checksum"] == newRecord["checksumCalc"])
                                {
                                    RecordType newRecordType = GetRecordType(newRecord["type"]);
                                    switch (newRecordType)
                                    {
                                        case RecordType.Header:
                                            _HasHeader = true;
                                            HeaderData = newRecord["data"];
                                            break;

                                        case RecordType.LinesCount:
                                            int dataLines = BaseFunc.HexToInt(newRecord["address"]);
                                            if (dataLines != _DataLinesCount)
                                            {
                                                _Errors.Add(lineNumber, ErrorCode.DataLinesCountMismatch);
                                            }
                                            break;

                                        case RecordType.Data:
                                            _DataLinesCount++;
                                            if (dataBlockCurrentAddress != Convert.ToInt64(newRecord["addressLong"]))
                                            {
                                                if (_RawDataBlocks.Count == 0 && newBlock.StartAddress == String.Empty)
                                                {
                                                    newBlock.StartAddress = newRecord["address"];
                                                    newBlock.FileStartLine = lineNumber;
                                                }
                                                else
                                                {
                                                    newBlock.EndAddress = (dataBlockCurrentAddress - 1).ToString("X");
                                                    newBlock.FileEndLine = lineNumber - 1;
                                                    newBlock.RawData = rawDataSb.ToString();
                                                    _RawDataBlocks.Add(newBlock);
                                                    rawDataSb = new StringBuilder();
                                                    newBlock = new DataBlock(startAddress: newRecord["address"], fileStartLine: lineNumber, dataString: StoreDataString);
                                                }
                                            }
                                            if (LoadData == true)
                                            {
                                                rawDataSb.Append(newRecord["data"]);
                                            }
                                            dataBlockCurrentAddress = Convert.ToInt64(newRecord["addressLong"]) + newRecord["data"].Length / 2;
                                            newBlock.EndAddress = (dataBlockCurrentAddress - 1).ToString("X");
                                            newBlock.FileEndLine = lineNumber;
                                            break;
                                    }
                                }
                                else
                                {
                                    _Errors.Add(lineNumber, ErrorCode.ChecksumMismatch);
                                }
                            }
                            else
                            {
                                _Errors.Add(lineNumber, ec);
                            }
                        }
                    }
                    
                    newFileLine = sr.ReadLine();
                }
                if (newBlock.StartAddress != String.Empty)
                {
                    newBlock.RawData = rawDataSb.ToString();
                    _RawDataBlocks.Add(newBlock);
                }
                _DataBlocks = GetContinuousBlocks(RawDataBlocks);
                sr.Close();
                sr.Dispose();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the S-Record file has been modified since the first read.
        /// </summary>
        /// <returns>Returns true if the file has changed since the first read.</returns>
        protected bool IsFileChanged()
        {
            if (!File.Exists(FileName))
            {
                return true;
                
            }
            if (File.GetLastWriteTimeUtc(FileName) != _FileLastModificationDate)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the file read contains errors.
        /// </summary>
        /// <returns>false if no errors have been detected, otherwise returns true.</returns>
        public bool HasErrors()
        {
            if (_Errors.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the file contains memory loadable data (S1/S2/S3).
        /// </summary>
        /// <returns>true if the file contains S1/S2/S3 record, otherwise returns false.</returns>
        public bool HasData()
        {
            if (_DataLinesCount > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Provides a list of continuous (merged) data blocks, based on a raw, unsorted data blocks list.
        /// </summary>
        /// <param name="unsortedBlocksList">Raw, unsorted data blocks list.</param>
        /// <returns>A list of continuous data blocks.</returns>
        public List<DataBlock> GetContinuousBlocks(List<DataBlock> unsortedBlocksList)
        {
            BlockComparer blockComparer = new BlockComparer();
            List<DataBlock> tempDataBlocks = unsortedBlocksList;
            List<DataBlock> dataBlocks = new List<DataBlock>();

            tempDataBlocks.Sort(blockComparer);

            DataBlock newMergedBlock = new DataBlock(dataString: StoreDataString);

            foreach (DataBlock block in tempDataBlocks)
            {
                if (newMergedBlock.StartAddress == String.Empty)
                {
                    newMergedBlock = block;
                }
                else
                {
                    if (BaseFunc.HexToInt64(block.StartAddress) - BaseFunc.HexToInt64(newMergedBlock.EndAddress) == 1)
                    {
                        newMergedBlock.EndAddress = block.EndAddress;
                        newMergedBlock.FileEndLine = block.FileEndLine;
                        StringBuilder sb = new StringBuilder();
                        sb.Append(newMergedBlock.RawData);
                        sb.Append(block.RawData);
                        newMergedBlock.RawData = sb.ToString();
                    }
                    else
                    {
                        dataBlocks.Add(newMergedBlock);
                        newMergedBlock = block;
                    }
                }
            }

            if (newMergedBlock.StartAddress != String.Empty)
            {
                dataBlocks.Add(newMergedBlock);
            }
            return dataBlocks;
        }

        /// <summary>
        /// Checks if a data range is available in the data blocks of the S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the range.</param>
        /// <param name="endAddress">End address of the range.</param>
        /// <returns>true if the data range is available in the S-Record file, otherwise returns false.</returns>
        public bool IsInRange(string startAddress, string endAddress)
        {
            bool result = false;

            foreach(DataBlock block in DataBlocks)
            {
                if (BaseFunc.HexToInt64(startAddress) >= BaseFunc.HexToInt64(block.StartAddress))
                {
                    if (BaseFunc.HexToInt64(endAddress) <= BaseFunc.HexToInt64(block.EndAddress))
                    {
                        return true;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Checks if a data range is available in the data blocks of the S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the range.</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>true if the data range is available in the S-Record file, otherwise returns false.</returns>
        public bool IsInRange(string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
                return IsInRange(startAddress, endAddress);
            }
            return false;
        }

        /// <summary>
        /// Returns the data block that contains the specified range.
        /// </summary>
        /// <param name="startAddress">Start address of the range.</param>
        /// <param name="endAddress">End address of the range.</param>
        /// <returns>A data block containing the specified range.</returns>
        public DataBlock GetDataBlockFromRange(string startAddress, string endAddress)
        {
            DataBlock newBlock = null;
            foreach (DataBlock block in DataBlocks)
            {
                if (BaseFunc.HexToInt64(startAddress) >= BaseFunc.HexToInt64(block.StartAddress))
                {
                    if (BaseFunc.HexToInt64(endAddress) <= BaseFunc.HexToInt64(block.EndAddress))
                    {
                        return block;
                    }
                }
            }
            return newBlock;
        }

        /// <summary>
        /// Returns the data block that contains the specified range.
        /// </summary>
        /// <param name="startAddress">Start address of the range.</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>A data block containing the specified range.</returns>
        public DataBlock GetDataBlockFromRange(string startAddress, long dataLength)
        {
            string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
            return GetDataBlockFromRange(startAddress, endAddress);
        }

        /// <summary>
        /// Reads a data range directly from the record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <returns>Data range as a string.</returns>
        public string ReadRangeFromFile(string startAddress, string endAddress)
        {
            string result = "";
            if (File.Exists(FileName))
            {
                StringBuilder rawDataSb = new StringBuilder();
                SortedDictionary<long, string> rawData = new SortedDictionary<long, string>();
                int lineNumber = 0;
                long startAddressLong = BaseFunc.HexToInt64(startAddress);
                long endAddressLong = BaseFunc.HexToInt64(endAddress);
                long extendedAddress = 0;

                if (startAddressLong <= endAddressLong)
                {
                    StreamReader sr = new StreamReader(FileName);
                    string newFileLine = sr.ReadLine();

                    while (newFileLine != null)
                    {
                        newFileLine = BaseFunc.RemoveWhiteSpaces(newFileLine);
                        List<string> recordLines = BaseFunc.Split(newFileLine, new char[] { _StartCode });

                        if (recordLines.Count > 0)
                        {
                            foreach (string newLine in recordLines)
                            {
                                lineNumber++;
                                if (!Errors.ContainsKey(lineNumber))
                                {
                                    Dictionary<string, string> newRecord = ParseLine(newLine);
                                    RecordType newRecordType = GetRecordType(newRecord["type"]);
                                    switch (newRecordType)
                                    {
                                        case RecordType.ExtendedSegmentAddress:
                                            extendedAddress = BaseFunc.HexToInt64(newRecord["data"] + "0");
                                            break;
                                        case RecordType.ExtendedLinearAddress:
                                            extendedAddress = BaseFunc.HexToInt64(newRecord["data"] + "0000");
                                            break;
                                        case RecordType.Data:
                                            long startRecAddress = extendedAddress + Convert.ToInt64(newRecord["addressLong"]);
                                            long endRecAddress = startRecAddress + newRecord["data"].Length / 2 - 1;
                                            if (startRecAddress >= startAddressLong && startRecAddress <= endAddressLong)
                                            {
                                                if (endRecAddress <= endAddressLong)
                                                {
                                                    rawData.Add(startRecAddress, newRecord["data"]);
                                                }
                                                else
                                                {
                                                    rawData.Add(startRecAddress, newRecord["data"].Substring(0, (Convert.ToInt32(endAddressLong - startRecAddress) + 1) * 2));
                                                }
                                            }
                                            else if (startAddressLong >= startRecAddress && startAddressLong <= endRecAddress)
                                            {
                                                if (endRecAddress <= endAddressLong)
                                                {
                                                    rawData.Add(startAddressLong, newRecord["data"].Substring(Convert.ToInt32(startAddressLong - startRecAddress) * 2, (Convert.ToInt32(endRecAddress - startAddressLong) + 1) * 2));
                                                }
                                                else
                                                {
                                                    rawData.Add(startAddressLong, newRecord["data"].Substring(Convert.ToInt32(startAddressLong - startRecAddress) * 2, (Convert.ToInt32(endAddressLong - startAddressLong) + 1) * 2));
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                        newFileLine = sr.ReadLine();
                    }
                    sr.Close();
                    sr.Dispose();
                    foreach (string data in rawData.Values)
                    {
                        rawDataSb.Append(data);
                    }
                    result = rawDataSb.ToString();
                    if (result.Length != (Convert.ToInt32(endAddressLong - startAddressLong) + 1) * 2)
                    {
                        result = "";
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Reads a data range directly from a S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>Data range as a string.</returns>
        public string ReadRangeFromFile(string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
                return ReadRangeFromFile(startAddress, endAddress);
            }
            return "";
        }

        /// <summary>
        /// Reads a data range directly from a S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <returns>Data range as a byte array.</returns>
        public byte[] ReadByteRangeFromFile(string startAddress, string endAddress)
        {
            string result = ReadRangeFromFile(startAddress, endAddress);
            return BaseFunc.HexToByteArray(result);
        }

        /// <summary>
        /// Reads a data range directly from a S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>Data range as a byte array.</returns>
        public byte[] ReadByteRangeFromFile(string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength).ToString("X");
                string result = ReadRangeFromFile(startAddress, endAddress);
                return BaseFunc.HexToByteArray(result);
            }
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Reads a data range directly from previously loaded data in memory (DataBlocks) from S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <returns>Data range as a string.</returns>
        public string ReadRange(string startAddress, string endAddress)
        {
            string result = "";
            if (LoadData == true)
            {
                long startAddressLong = BaseFunc.HexToInt64(startAddress);
                long endAddressLong = BaseFunc.HexToInt64(endAddress);
                if (startAddressLong <= endAddressLong)
                {
                    foreach(DataBlock dataBlock in DataBlocks)
                    {
                        long startBlockAddress = BaseFunc.HexToInt64(dataBlock.StartAddress);
                        long endBlockAddress = BaseFunc.HexToInt64(dataBlock.EndAddress);
                        if (startAddressLong >= startBlockAddress && endAddressLong <= endBlockAddress)
                        {
                            result = dataBlock.RawData.Substring(Convert.ToInt32(startAddressLong - startBlockAddress) * 2, Convert.ToInt32(endAddressLong - startAddressLong + 1) * 2);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Reads a data range directly from previously loaded data in memory (DataBlocks) from S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>Data range as a string.</returns>
        public string ReadRange(string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
                return ReadRange(startAddress, endAddress);
            }
            return "";
        }

        /// <summary>
        /// Reads a data range directly from previously loaded data in memory (DataBlocks) from S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <returns>Data range as a byte array.</returns>
        public byte[] ReadByteRange(string startAddress, string endAddress)
        {
            string result = ReadRange(startAddress, endAddress);
            return BaseFunc.HexToByteArray(result);
        }

        /// <summary>
        /// Reads a data range directly from previously loaded data in memory (DataBlocks) from S-Record file.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>Data range as a byte array.</returns>
        public byte[] ReadByteRange(string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
                string result = ReadRange(startAddress, endAddress);
                return BaseFunc.HexToByteArray(result);
            }
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Safely reads a data range, either from file or from memory.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <returns>Data range as a string.</returns>
        public string ExtractDataRange(string startAddress, string endAddress)
        {
            string result = "";
            if(LoadData == false)
            {
                if (IsFileChanged())
                {
                    Read();
                }
            }
            if(IsInRange(startAddress,endAddress))
            {
                if (LoadData == false)
                {
                    result = ReadRangeFromFile(startAddress, endAddress);
                }
                else
                {
                    result = ReadRange(startAddress, endAddress);
                }
            }
            return result;
        }

        /// <summary>
        /// Safely reads a data range, either from file or from memory.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>Data range as a string.</returns>
        public string ExtractDataRange(string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
                return ExtractDataRange(startAddress, endAddress);
            }
            return "";
        }

        /// <summary>
        /// Safely reads a data range, either from file or from memory.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <returns>Data range as a byte array.</returns>
        public byte[] ExtractByteDataRange(string startAddress, string endAddress)
        {
            
            if (LoadData == false)
            {
                if (IsFileChanged())
                {
                    Read();
                }
            }
            if (IsInRange(startAddress, endAddress))
            {
                if (LoadData == false)
                {
                    return ReadByteRangeFromFile(startAddress, endAddress);
                }
                else
                {
                    return ReadByteRange(startAddress, endAddress);
                }
            }
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Safely reads a data range, either from file or from memory.
        /// </summary>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns>Data range as a byte array.</returns>
        public byte[] ExtractByteDataRange(string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
                return ExtractByteDataRange(startAddress, endAddress);
            }
            return Array.Empty<byte>();
        }

        /// <summary>
        /// Exports a data range in binary format to an output file.
        /// </summary>
        /// <param name="fileName">The full path and file name to which data will be written.</param>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <returns></returns>
        public bool ExportRangeToBin(string fileName, string startAddress, string endAddress)
        {
            byte[] data = ExtractByteDataRange(startAddress, endAddress);
            if (data != null)
            {
                try
                {
                    StreamWriter sw = new StreamWriter(fileName, false);
                    sw.BaseStream.Write(data);
                    sw.Close();
                    sw.Dispose();
                    return true;
                }
                catch
                { 

                }
            }
            return false;
        }

        /// <summary>
        /// Exports a data range in binary format to an output file.
        /// </summary>
        /// <param name="fileName">The full path and file name to which data will be written.</param>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="dataLength">Length of the range, in bytes.</param>
        /// <returns></returns>
        public bool ExportRangeToBin(string fileName, string startAddress, long dataLength)
        {
            if (dataLength > 0)
            {
                string endAddress = (BaseFunc.HexToInt64(startAddress) + dataLength - 1).ToString("X");
                return ExportRangeToBin(fileName, startAddress, endAddress);
            }
            return false;
        }

        /// <summary>
        /// Exports a data range to a S-Record file.
        /// </summary>
        /// <param name="fileName">The full path and file name to which data will be written.</param>
        /// <param name="startAddress">Start address of the data range (in hexadecimal).</param>
        /// <param name="endAddress">End address of the data range (in hexadecimal).</param>
        /// <param name="addressLength">Optional - number of bytes for the address field. Default : 4 bytes.</param>
        /// <param name="dataLength">Optional - number of bytes for the data field. Default : 32 bytes.</param>
        /// <param name="newStartAddress">Optional - start address to be used instead of the actual start address for the original file.</param>
        /// <param name="specificHeader">Optional - string to be used for the header record.</param>
        /// <param name="insertLinesCount">Optional - record count to be inserted (S5/S6 records). Default : true.</param>
        /// <returns>A S-Record file with the selected data range.</returns>
        public virtual bool ExportRangeToFile(string fileName, string startAddress, string endAddress, int addressLength = 4, int dataLength = 32, string newStartAddress = "", string specificHeader = "", bool insertLinesCount = true)
        {
            if (addressLength < 2 || addressLength > 4)
            {
                addressLength = 4;
            }
            int maxDataLength = 255 - 1 - addressLength;
            if (dataLength < 1 || dataLength > maxDataLength)
            {
                dataLength = 32;
            }
            if (newStartAddress != String.Empty)
            {
                if (BaseFunc.IsHex(newStartAddress))
                {
                    if (newStartAddress.Length > 8)
                    {
                        newStartAddress = "";
                    }
                    else
                    {
                        if (newStartAddress.Length > addressLength * 2)
                        {
                            addressLength = newStartAddress.Length / 2;
                        }
                    }
                }
            }
            string data = ExtractDataRange(startAddress, endAddress);
            if (data != "")
            {
                try
                {
                    StringBuilder tempRecord = new StringBuilder();
                    StringBuilder tempData = new StringBuilder();
                    string dataHeader = "";
                    string endHeader = "";
                    switch (addressLength)
                    {
                        case 2:
                            dataHeader = "S1";
                            endHeader = "S9";
                            break;
                        case 3:
                            dataHeader = "S2";
                            endHeader = "S8";
                            break;
                        case 4:
                            dataHeader = "S3";
                            endHeader = "S7";
                            break;
                    }
                    int byteCount = 0;
                    StreamWriter sw = new StreamWriter(fileName, false);
                    // Header
                    tempRecord.Append("S0");
                    byteCount = 3; // Address 0000 (2 bytes) + checksum (1 byte)
                    if (specificHeader != String.Empty)
                    {
                        specificHeader = specificHeader.Substring(0, Math.Min(specificHeader.Length, maxDataLength));
                        specificHeader = BaseFunc.StringToHexString(specificHeader);
                        
                    }
                    else
                    {
                        specificHeader = HeaderData;
                    }
                    byteCount += specificHeader.Length / 2;
                    tempData.Append(byteCount.ToString("X2"));
                    tempData.Append("0000");
                    tempData.Append(specificHeader);
                    tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString()));
                    tempRecord.Append(tempData);
                    sw.WriteLine(tempRecord.ToString());

                    // Data
                    long currentAddress = 0;
                    if (newStartAddress != String.Empty)
                    {
                        currentAddress = BaseFunc.HexToInt64(newStartAddress);
                    }
                    else
                    {
                        currentAddress = BaseFunc.HexToInt64(startAddress);
                    }
                    int maxDataLines = data.Length / (2 * dataLength);
                    if (data.Length % (2 * dataLength) > 0)
                    {
                        maxDataLines += 1;
                    }
                    for (int i = 0; i < maxDataLines; i++)
                    {
                        tempData.Clear();
                        tempRecord.Clear();
                        tempRecord.Append(dataHeader);

                        int startIndex = i * 2 * dataLength;
                        int dataLineLength = Math.Min(dataLength * 2, data.Length - startIndex);
                        byteCount = 1 + addressLength + dataLineLength / 2;
                        
                        tempData.Append(byteCount.ToString("X2"));
                        tempData.Append(currentAddress.ToString("X"+ (addressLength * 2).ToString()));
                        tempData.Append(data.Substring(i * dataLength * 2, dataLineLength));
                        tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString()));
                        tempRecord.Append(tempData);
                        sw.WriteLine(tempRecord.ToString());

                        currentAddress += dataLength;
                    }

                    // Count
                    if (insertLinesCount)
                    {
                        tempData.Clear();
                        tempRecord.Clear();
                        if (maxDataLines <= UInt16.MaxValue)
                        {
                            tempRecord.Append("S5");
                            byteCount = 3;
                        }
                        else
                        {
                            tempRecord.Append("S6");
                            byteCount = 4;
                        }
                        tempData.Append(byteCount.ToString("X2"));
                        tempData.Append(maxDataLines.ToString("X" + ((byteCount - 1) * 2).ToString()));
                        tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString()));
                        tempRecord.Append(tempData);
                        sw.WriteLine(tempRecord.ToString());
                    }

                    // End line
                    tempData.Clear();
                    tempRecord.Clear();
                    tempRecord.Append(endHeader);
                    byteCount = 1 + addressLength;
                    tempData.Append(byteCount.ToString("X2"));
                    tempData.Append(0.ToString("X" + (addressLength * 2).ToString()));
                    tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString()));
                    tempRecord.Append(tempData);
                    sw.WriteLine(tempRecord.ToString());

                    sw.Close();
                    sw.Dispose();
                    return true;
                }
                catch
                {

                }
            }
            return false;
        }

        /// <summary>
        /// Directly modifies inside the original record file the data from the selected range.
        /// </summary>
        /// <param name="startAddress">Starting address where the data is modified.</param>
        /// <param name="data">Hexadecimal data to be inserted.</param>
        /// <returns>true if the modification has been successfully performed, otherwise returns false.</returns>
        protected virtual bool ModifyInFile(string startAddress, string data)
        {
            if (File.Exists(FileName))
            {
                Guid guid = Guid.NewGuid();
                try
                {
                    string fileExtension = System.IO.Path.GetExtension(FileName);
                    string filePath = System.IO.Path.GetDirectoryName(FileName);
                    string tempFileName = guid.ToString() + fileExtension;
                    string tempFile = System.IO.Path.Combine(filePath, tempFileName);

                    long startAddressLong = BaseFunc.HexToInt64(startAddress);
                    long endAddressLong = startAddressLong + data.Length / 2 - 1;
                    long extendedAddress = 0;

                    StreamReader sr = new StreamReader(FileName);
                    StreamWriter sw = new StreamWriter(tempFile, false);

                    string newFileLine = sr.ReadLine();
                    int lineNumber = 0;

                    while(newFileLine != null)
                    {
                        string cleanedFileLine = BaseFunc.RemoveWhiteSpaces(newFileLine);
                        List<string> recordLines = BaseFunc.Split(cleanedFileLine, new char[] { _StartCode });

                        if (recordLines.Count > 0)
                        {
                            foreach (string newLine in recordLines)
                            {
                                lineNumber++;
                                if (!Errors.ContainsKey(lineNumber))
                                {
                                    Dictionary<string, string> newRecord = ParseLine(newLine);
                                    RecordType newRecordType = GetRecordType(newRecord["type"]);
                                    switch (newRecordType)
                                    {
                                        case RecordType.ExtendedSegmentAddress:
                                            extendedAddress = BaseFunc.HexToInt64(newRecord["data"] + "0");
                                            break;
                                        case RecordType.ExtendedLinearAddress:
                                            extendedAddress = BaseFunc.HexToInt64(newRecord["data"] + "0000");
                                            break;
                                        case RecordType.Data:
                                            long startRecAddress = Convert.ToInt64(newRecord["addressLong"]) + extendedAddress;
                                            long endRecAddress = startRecAddress + newRecord["data"].Length / 2 - 1;
                                            if ((startRecAddress >= startAddressLong && startRecAddress <= endAddressLong) || (startAddressLong >= startRecAddress && startAddressLong <= endRecAddress))
                                            {
                                                int startPosition = Convert.ToInt32(Math.Max(startRecAddress, startAddressLong) - startRecAddress) * 2;
                                                int dataLength = Convert.ToInt32(Math.Min(endRecAddress, endAddressLong) * 2 - Math.Max(startRecAddress, startAddressLong) * 2 + 2);

                                                int startPositionData = Convert.ToInt32(Math.Max(startRecAddress, startAddressLong) - startAddressLong) * 2;

                                                StringBuilder tempRecord = new StringBuilder();
                                                StringBuilder tempDataCks = new StringBuilder();

                                                tempRecord.Append(_StartCode);

                                                for (int i = 0; i < _ElementsOrder.Count; i++)
                                                {
                                                    bool inChecksum = false;
                                                    inChecksum = _ElementsCalcCks.Contains(_ElementsOrder[i]);

                                                    switch (_ElementsOrder[i])
                                                    {
                                                        case "data":
                                                            if (startPosition > 0)
                                                            {
                                                                tempRecord.Append(newRecord["data"].Substring(0, startPosition));
                                                                if (inChecksum)
                                                                {
                                                                    tempDataCks.Append(newRecord["data"].Substring(0, startPosition));
                                                                }
                                                            }
                                                            tempRecord.Append(data.Substring(startPositionData, dataLength));
                                                            if (inChecksum)
                                                            {
                                                                tempDataCks.Append(data.Substring(startPositionData, dataLength));
                                                            }
                                                            if (startPosition + dataLength < newRecord["data"].Length)
                                                            {
                                                                tempRecord.Append(newRecord["data"].Substring(startPosition + dataLength, newRecord["data"].Length - startPosition - dataLength));
                                                                if (inChecksum)
                                                                {
                                                                    tempDataCks.Append(newRecord["data"].Substring(startPosition + dataLength, newRecord["data"].Length - startPosition - dataLength));
                                                                }
                                                            }
                                                            break;
                                                        case "checksum":
                                                            tempRecord.Append(BaseFunc.HexCheckSumCalc(tempDataCks.ToString(), _OneComplementCks));
                                                            break;
                                                        default:
                                                            tempRecord.Append(newRecord[_ElementsOrder[i]]);
                                                            if (inChecksum)
                                                            {
                                                                tempDataCks.Append(newRecord[_ElementsOrder[i]]);
                                                            }
                                                            break;
                                                    }

                                                }

                                                newFileLine = newFileLine.Replace(newLine, tempRecord.ToString());
                                            }
                                            break;

                                    }
                                }
                            }
                        }

                        sw.WriteLine(newFileLine);
                        newFileLine = sr.ReadLine();
                    }
                    sr.Close();
                    sr.Dispose();
                    sw.Close();
                    sw.Dispose();
                    File.Delete(FileName);
                    File.Move(tempFile, FileName);
                    _FileLastModificationDate = File.GetLastWriteTimeUtc(FileName);
                    return true;
                }
                catch
                {

                }
            }
            return false;
        }

        /// <summary>
        /// Modifies the data values at the start address mentionned, either from values stored in memory (LoadData == true) or directly from file.
        /// </summary>
        /// <param name="startAddress">Starting address where the data is modified.</param>
        /// <param name="data">Hexadecimal data to be inserted.</param>
        /// <returns>true if the modification has been successfully performed, otherwise returns false.</returns>
        public bool Modify(string startAddress, string data)
        {
            if (!string.IsNullOrEmpty(startAddress))
            {
                if (BaseFunc.IsHex(data))
                {
                    if ((data.Length % 2) == 0)
                    {
                        if (IsInRange(startAddress, data.Length / 2))
                        {
                            if (LoadData)
                            {
                                DataBlock dataBlock = new DataBlock();
                                dataBlock = GetDataBlockFromRange(startAddress, data.Length);
                                dataBlock.ModifyData(startAddress, data);
                                return true;
                            }
                            else
                            {
                                return ModifyInFile(startAddress, data);
                            }
                        }
                    }
                }
            }
            return false;
        }

        public bool ReadFromBin(string binFileName)
        {
            return false;
        }

        public bool SaveAs(string outputFileName)
        {
            return false;
        }

    }

    /// <summary>
    /// Comparer to order data blocks by start address
    /// </summary>
    class BlockComparer : IComparer<DataBlock>
    {
        public int Compare(DataBlock x, DataBlock y)
        {
            if (x == null || y == null)
            {
                return 0;
            }
            long xStartAddress = BaseFunc.HexToInt64(x.StartAddress);
            long yStartAddress = BaseFunc.HexToInt64(y.StartAddress);
            return xStartAddress.CompareTo(yStartAddress);
        }
    }
}
