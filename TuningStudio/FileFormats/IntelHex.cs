using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuningStudio.Modules;

namespace TuningStudio.FileFormats
{
    public class IntelHex : SRecord
    {

        /// <summary>
        /// Creates a new empty instance of an Intel HEX file.
        /// </summary>
        public IntelHex() : base() { }

        /// <summary>
        /// Creates a new instance of an Intel HEX file
        /// </summary>
        /// <param name="fileName">The full file name and path to be read.</param>
        public IntelHex(string fileName, bool loadData = false) : base(fileName, loadData) { }

        /// <summary>
        /// Defines specific values for the Intel HEX data format.
        /// </summary>
        protected override void SetFormatValues()
        {
            _StartCode = ':';
            _ByteCountPosition = 1;
            _ByteCountLength = 2;
            _RecordTypePosition = 7;
            _RecordTypeLength = 2;
            _AddressPosition = 3;
            _ChecksumLength = 2;
            _OneComplementCks = false;
            _MinLength = 11;
            _AddressLength = new Dictionary<string, int>()
            {
                {"00",4},{"01",4},{"02",4},{"03",4},{"04",4},{"05",4}
            };
            _ElementsCalcCks = new List<string>
            {
                "byteCount", "address", "type", "data"
            };
            _ElementsOrder = new List<string>
            {
                 "byteCount", "address", "type", "data", "checksum"
            };

        }

        /// <summary>
        /// Provides the hexadecimal string of the bytes taken into account for the byte count value of the record.
        /// </summary>
        /// <param name="inputString">Complete record line as string.</param>
        /// <returns>Hexadecimal string with bytes taken into account for the byte count value of the record.</returns>
        public override string ExtractByteCountData(string inputString)
        {
            if (inputString != String.Empty)
            {
                int dataLength = inputString.Length - 1 - _RecordTypeLength - _ChecksumLength - _ByteCountLength - _AddressLength["00"];
                return inputString.Substring(_RecordTypePosition + _RecordTypeLength, dataLength);
            }
            return "";
        }

        /// <summary>
        /// Isolates the hexadecimal data values from a record line.
        /// </summary>
        /// <param name="inputString">Complete record line as string.</param>
        /// <param name="recordType">Hexadecimal data as string.</param>
        /// <returns></returns>
        public override string ExtractData(string inputString, string recordType)
        {
            int dataLength = inputString.Length - 1 - _RecordTypeLength - _ByteCountLength - _AddressLength[recordType] - _ChecksumLength;
            if (dataLength > 0)
            {
                return inputString.Substring(_RecordTypePosition + _RecordTypeLength, dataLength);
            }
            return "";
        }

        /// <summary>
        /// Provides the type of record from the record type value.
        /// </summary>
        /// <param name="type">Type value as string.</param>
        /// <returns>Record type as enumeration.</returns>
        public override RecordType GetRecordType(string type)
        {
            return type switch
            {
                "00" => RecordType.Data,
                "01" => RecordType.Termination,
                "02" => RecordType.ExtendedSegmentAddress,
                "03" => RecordType.StartSegmentAddress,
                "04" => RecordType.ExtendedLinearAddress,
                "05" => RecordType.StartLinearAddress,
                _ => RecordType.Other,
            };
        }

        /// <summary>
        /// Reads the entire Intel HEX file and stores the processed values.
        /// </summary>
        /// <returns>Returns true if the read is succesful, otherwise returns false.</returns>
        public override bool Read()
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
                long extendedAddress = 0;
                StringBuilder rawDataSb = new StringBuilder();

                while (newFileLine != null)
                {
                    newFileLine = BaseFunc.RemoveWhiteSpaces(newFileLine);
                    List<string> recordLines = BaseFunc.Split(newFileLine, new char[] { _StartCode });

                    if (recordLines.Count > 0)
                    {
                        foreach (string newLine in recordLines)
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
                                        case RecordType.ExtendedSegmentAddress:
                                            extendedAddress = BaseFunc.HexToInt64(newRecord["data"] + "0");
                                            break;
                                        case RecordType.ExtendedLinearAddress:
                                            extendedAddress = BaseFunc.HexToInt64(newRecord["data"] + "0000");
                                            break;
                                        case RecordType.Data:
                                            _DataLinesCount++;
                                            long currentRecordAddress = extendedAddress + Convert.ToInt64(newRecord["addressLong"]);
                                            if (dataBlockCurrentAddress != currentRecordAddress)
                                            {
                                                if (_RawDataBlocks.Count == 0 && newBlock.StartAddress == String.Empty)
                                                {
                                                    newBlock.StartAddress = currentRecordAddress.ToString("X");
                                                    newBlock.FileStartLine = lineNumber;
                                                }
                                                else
                                                {
                                                    newBlock.EndAddress = (dataBlockCurrentAddress - 1).ToString("X");
                                                    newBlock.FileEndLine = lineNumber - 1;
                                                    newBlock.RawData = rawDataSb.ToString();
                                                    _RawDataBlocks.Add(newBlock);
                                                    rawDataSb = new StringBuilder();
                                                    newBlock = new DataBlock(startAddress: currentRecordAddress.ToString("X"), fileStartLine: lineNumber, dataString: StoreDataString);
                                                }
                                            }
                                            if (LoadData == true)
                                            {
                                                rawDataSb.Append(newRecord["data"]);
                                            }
                                            dataBlockCurrentAddress = currentRecordAddress + newRecord["data"].Length / 2;
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

        public virtual bool ExportRangeToFile(string fileName, string startAddress, string endAddress, int addressLength = 4, int dataLength = 32, string newStartAddress = "")
        {
            if (addressLength < 2 || addressLength > 4)
            {
                addressLength = 4;
            }
            int maxDataLength = 255;
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
                    string dataHeader = "00";
                    string extendedAddressHeader = "04";
                    string highAddress = "";
                    string lowAddress = "";

                    if (newStartAddress != String.Empty)
                    {
                        highAddress = newStartAddress.PadLeft(8, '0').Substring(0,4);
                        lowAddress = newStartAddress.PadLeft(8, '0').Substring(4, 4);
                    }
                    else
                    {
                        highAddress = startAddress.PadLeft(8, '0').Substring(0, 4);
                        lowAddress = startAddress.PadLeft(8, '0').Substring(4, 4);
                    }

                    int highAddressInt = BaseFunc.HexToInt(highAddress);
                    int lowAddressInt = BaseFunc.HexToInt(lowAddress);
                    int lowAddressLimit = Convert.ToInt32("FFFF", 16);

                    StreamWriter sw = new StreamWriter(fileName, false);

                    // First line for extended address
                    tempRecord.Append(_StartCode);

                    tempData.Append("02");
                    tempData.Append("0000");
                    tempData.Append(extendedAddressHeader);
                    tempData.Append(highAddress);
                    tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString(), _OneComplementCks));
                    
                    tempRecord.Append(tempData.ToString());

                    sw.WriteLine(tempRecord.ToString());

                    // Data
                    int startIndex = 0;

                    while (startIndex < data.Length)
                    {
                        tempData.Clear();
                        tempRecord.Clear();

                        if (lowAddressInt + dataLength - 1 > lowAddressLimit)
                        {
                            int dataRemaining = lowAddressLimit - lowAddressInt + 1;

                            tempRecord.Append(_StartCode);

                            tempData.Append(dataRemaining.ToString("X2"));
                            tempData.Append(lowAddressInt.ToString("X4"));
                            tempData.Append(dataHeader);
                            tempData.Append(data.Substring(startIndex, dataRemaining * 2));
                            tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString(), _OneComplementCks));

                            tempRecord.Append(tempData.ToString());
                            sw.WriteLine(tempRecord.ToString());

                            startIndex += dataRemaining * 2;
                            lowAddressInt = 0;

                            highAddressInt += 1;

                            tempData.Clear();
                            tempRecord.Clear();

                            tempRecord.Append(_StartCode);

                            tempData.Append("02");
                            tempData.Append("0000");
                            tempData.Append(extendedAddressHeader);
                            tempData.Append(highAddressInt.ToString("X4"));
                            tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString(), _OneComplementCks));

                            tempRecord.Append(tempData.ToString());

                            sw.WriteLine(tempRecord.ToString());

                            tempData.Clear();
                            tempRecord.Clear();
                        }

                        tempRecord.Append(_StartCode);

                        int dataRecLength = Math.Min(data.Length - startIndex, dataLength * 2);
                        tempData.Append((dataRecLength / 2).ToString("X2"));
                        tempData.Append(lowAddressInt.ToString("X4"));
                        tempData.Append(dataHeader);
                        tempData.Append(data.Substring(startIndex, dataRecLength));
                        tempData.Append(BaseFunc.HexCheckSumCalc(tempData.ToString(), _OneComplementCks));

                        tempRecord.Append(tempData.ToString());
                        sw.WriteLine(tempRecord.ToString());

                        startIndex += dataLength * 2;
                        lowAddressInt += dataLength;
                    }

                    // End record
                    sw.WriteLine(":00000001FF");

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
    }
}
