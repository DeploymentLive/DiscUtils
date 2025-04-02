//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using DiscUtils.Streams;
using static System.Net.Mime.MediaTypeNames;

namespace DiscUtils.Fat
{
    internal class DirectoryEntry
    {
        private readonly FatType _fatVariant;
        private readonly FatFileSystemOptions _options;
        private byte _attr;
        private ushort _creationDate;
        private ushort _creationTime;
        private byte _creationTimeTenth;
        private uint _fileSize;
        private ushort _firstClusterHi;
        private ushort _firstClusterLo;
        private ushort _lastAccessDate;
        private ushort _lastWriteDate;
        private ushort _lastWriteTime;

        internal DirectoryEntry(FatFileSystemOptions options, Stream stream, FatType fatVariant)
        {
            _options = options;
            _fatVariant = fatVariant;
            byte[] buffer = StreamUtilities.ReadExact(stream, 32);
            Load(buffer, 0, options.FileNameEncoding);
        }

        internal DirectoryEntry(FatFileSystemOptions options, FileName name, FatAttributes attrs, FatType fatVariant)
        {
            _options = options;
            _fatVariant = fatVariant;
            Name = name;
            _attr = (byte)attrs;
        }

        internal DirectoryEntry(DirectoryEntry toCopy)
        {
            _options = toCopy._options;
            _fatVariant = toCopy._fatVariant;
            Name = toCopy.Name;
            _attr = toCopy._attr;
            _creationTimeTenth = toCopy._creationTimeTenth;
            _creationTime = toCopy._creationTime;
            _creationDate = toCopy._creationDate;
            _lastAccessDate = toCopy._lastAccessDate;
            _firstClusterHi = toCopy._firstClusterHi;
            _lastWriteTime = toCopy._lastWriteTime;
            _firstClusterLo = toCopy._firstClusterLo;
            _fileSize = toCopy._fileSize;
        }

        public FatAttributes Attributes
        {
            get { return (FatAttributes)_attr; }
            set { _attr = (byte)value; }
        }

        public DateTime CreationTime
        {
            get { return FileTimeToDateTime(_creationDate, _creationTime, _creationTimeTenth); }
            set { DateTimeToFileTime(value, out _creationDate, out _creationTime, out _creationTimeTenth); }
        }

        public int FileSize
        {
            get { return (int)_fileSize; }
            set { _fileSize = (uint)value; }
        }

        public uint FirstCluster
        {
            get
            {
                if (_fatVariant == FatType.Fat32)
                {
                    return (uint)(_firstClusterHi << 16) | _firstClusterLo;
                }
                return _firstClusterLo;
            }

            set
            {
                if (_fatVariant == FatType.Fat32)
                {
                    _firstClusterHi = (ushort)((value >> 16) & 0xFFFF);
                }

                _firstClusterLo = (ushort)(value & 0xFFFF);
            }
        }

        public DateTime LastAccessTime
        {
            get { return FileTimeToDateTime(_lastAccessDate, 0, 0); }
            set { DateTimeToFileTime(value, out _lastAccessDate); }
        }

        public DateTime LastWriteTime
        {
            get { return FileTimeToDateTime(_lastWriteDate, _lastWriteTime, 0); }
            set { DateTimeToFileTime(value, out _lastWriteDate, out _lastWriteTime); }
        }

        public FileName Name { get; set; }

        internal static byte CheckSum(string name )
        {
            byte sum = 0;
            for (int i = 0; i < 11; i++)
            {
                sum = (byte) ( (((sum & 1) << 7) | ((sum & 0xfe) >> 1)) + name[i] );
            }
            return sum;
        }

        internal void WriteLongFile(string LongFilename, byte Sequence, Stream stream)
        {
            byte[] array = new byte[32];

            if ( LongFilename.Length > 13 )
            {
                Console.WriteLine("Write: " + LongFilename.Substring(0, 13));
                // Palce the long name into the stream, end first. Recursion to the rescue!! 
                WriteLongFile(LongFilename.Substring(13), Sequence++, stream);
            }
            else
            {
                Sequence += 0x40;
            }

            array[0] = Sequence; // attribute byte
            array[11] = (byte) (FatAttributes.ReadOnly | FatAttributes.Hidden | FatAttributes.System | FatAttributes.VolumeId); 
            array[12] = 0; // always 0 
            string ShortName = Name.LongName.Substring(0, 8);
            if (Name.LongName.IndexOf('.') > 1) { ShortName += Name.LongName.Substring(Name.LongName.IndexOf('.') + 1, 3); }
            array[13] = CheckSum(ShortName.ToUpperInvariant());
            // EndianUtilities.WriteBytesLittleEndian(_firstClusterLo, array, 26);

            byte[] NameBuffer = Encoding.Unicode.GetBytes(Name.LongName);
            //         (Name, Start, Dest, Start, Len)
            Array.Copy(NameBuffer,  0, array,  1, 10); // First 5 chars
            Array.Copy(NameBuffer, 10, array, 14, 12); // Second 6 chars
            Array.Copy(NameBuffer, 22, array, 28,  4); // Third 2 chars

            stream.Write(array, 0, array.Length);

        }

        internal void WriteTo(Stream stream)
        {
            byte[] buffer = new byte[32];

            if (!String.IsNullOrEmpty(Name.LongName))
            {
                // from https://www.kernel.org/doc/Documentation/filesystems/vfat.txt
                WriteLongFile(Name.LongName, 1, stream);

            }

            Name.GetBytes(buffer, 0);
            buffer[11] = _attr;
            buffer[13] = _creationTimeTenth;
            EndianUtilities.WriteBytesLittleEndian(_creationTime, buffer, 14);
            EndianUtilities.WriteBytesLittleEndian(_creationDate, buffer, 16);
            EndianUtilities.WriteBytesLittleEndian(_lastAccessDate, buffer, 18);
            EndianUtilities.WriteBytesLittleEndian(_firstClusterHi, buffer, 20);
            EndianUtilities.WriteBytesLittleEndian(_lastWriteTime, buffer, 22);
            EndianUtilities.WriteBytesLittleEndian(_lastWriteDate, buffer, 24);
            EndianUtilities.WriteBytesLittleEndian(_firstClusterLo, buffer, 26);
            EndianUtilities.WriteBytesLittleEndian(_fileSize, buffer, 28);

            stream.Write(buffer, 0, buffer.Length);
        }

        private static DateTime FileTimeToDateTime(ushort date, ushort time, byte tenths)
        {
            if (date == 0 || date == 0xFFFF)
            {
                // Return Epoch - this is an invalid date
                return FatFileSystem.Epoch;
            }

            int year = 1980 + ((date & 0xFE00) >> 9);
            int month = (date & 0x01E0) >> 5;
            int day = date & 0x001F;
            int hour = (time & 0xF800) >> 11;
            int minute = (time & 0x07E0) >> 5;
            int second = (time & 0x001F) * 2 + tenths / 100;
            int millis = tenths % 100 * 10;

            return new DateTime(year, month, day, hour, minute, second, millis);
        }

        private static void DateTimeToFileTime(DateTime value, out ushort date)
        {
            byte tenths;
            ushort time;
            DateTimeToFileTime(value, out date, out time, out tenths);
        }

        private static void DateTimeToFileTime(DateTime value, out ushort date, out ushort time)
        {
            byte tenths;
            DateTimeToFileTime(value, out date, out time, out tenths);
        }

        private static void DateTimeToFileTime(DateTime value, out ushort date, out ushort time, out byte tenths)
        {
            if (value.Year < 1980)
            {
                value = FatFileSystem.Epoch;
            }

            date =
                (ushort)((((value.Year - 1980) << 9) & 0xFE00) | ((value.Month << 5) & 0x01E0) | (value.Day & 0x001F));
            time =
                (ushort)(((value.Hour << 11) & 0xF800) | ((value.Minute << 5) & 0x07E0) | ((value.Second / 2) & 0x001F));
            tenths = (byte)(value.Second % 2 * 100 + value.Millisecond / 10);
        }

        private void Load(byte[] data, int offset, Encoding encoding)
        {
            Name = new FileName(data, offset, encoding);
            _attr = data[offset + 11];
            _creationTimeTenth = data[offset + 13];
            _creationTime = EndianUtilities.ToUInt16LittleEndian(data, offset + 14);
            _creationDate = EndianUtilities.ToUInt16LittleEndian(data, offset + 16);
            _lastAccessDate = EndianUtilities.ToUInt16LittleEndian(data, offset + 18);
            _firstClusterHi = EndianUtilities.ToUInt16LittleEndian(data, offset + 20);
            _lastWriteTime = EndianUtilities.ToUInt16LittleEndian(data, offset + 22);
            _lastWriteDate = EndianUtilities.ToUInt16LittleEndian(data, offset + 24);
            _firstClusterLo = EndianUtilities.ToUInt16LittleEndian(data, offset + 26);
            _fileSize = EndianUtilities.ToUInt32LittleEndian(data, offset + 28);
        }
    }
}