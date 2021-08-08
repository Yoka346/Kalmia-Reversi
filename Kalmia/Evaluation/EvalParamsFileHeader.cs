using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Kalmia.Evaluation
{
    public struct EvalParamsFileHeader
    {
        public const int HEADER_SIZE = 29;
        const int LABEL_OFFSET = 0;
        const int LABEL_SIZE = 16;
        const int VERSION_OFFSET = LABEL_OFFSET + LABEL_SIZE;
        const int VERSION_SIZE = 4;
        const int DATE_TIME_OFFSET = VERSION_OFFSET + VERSION_SIZE;
        const int DATE_TIME_SIZE = 9;

        public string Label { get; }
        public int Version { get; }
        public DateTime ReleasedTime { get; }

        public EvalParamsFileHeader(string label, int version, DateTime releasedTime)
        {
            if (Encoding.UTF8.GetBytes(label).Length > LABEL_SIZE)
                throw new ArgumentOutOfRangeException($"Specified label is too long. label has to be less than or equal 16 bytes.");
            this.Label = label;
            this.Version = version;
            this.ReleasedTime = releasedTime;
        }

        public EvalParamsFileHeader(string path) : this(LoadHeader(path)) { }

        public EvalParamsFileHeader(FileStream fs) : this(LoadHeader(fs)) { }

        EvalParamsFileHeader(byte[] header)
        {
            this.Label = Encoding.UTF8.GetString(header.AsSpan(LABEL_OFFSET, LABEL_SIZE));
            this.Version = BitConverter.ToInt32(header.AsSpan(VERSION_OFFSET, VERSION_SIZE));
            this.ReleasedTime = LoadDateTimeFromBin(header[DATE_TIME_OFFSET..(DATE_TIME_OFFSET + DATE_TIME_SIZE)]);
        }

        public void WriteToFile(string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            WriteToStream(fs);
        }

        public void WriteToStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var labelBin = Encoding.UTF8.GetBytes(this.Label);
            stream.Write(labelBin, 0, labelBin.Length);
            stream.Seek(LABEL_SIZE - labelBin.Length, SeekOrigin.Current);
            stream.Write(BitConverter.GetBytes(this.Version), 0, VERSION_SIZE);
            stream.Write(DateTimeToBin(this.ReleasedTime), 0, DATE_TIME_SIZE);
        }

        static byte[] LoadHeader(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            return LoadHeader(fs);
        }

        static byte[] LoadHeader(FileStream fs)
        {
            var header = new byte[HEADER_SIZE];
            fs.Seek(0, SeekOrigin.Begin);
            fs.Read(header, 0, HEADER_SIZE);
            return header;
        }

        static DateTime LoadDateTimeFromBin(byte[] dateTimeBin)
        {
            var year = BitConverter.ToInt16(dateTimeBin.AsSpan(0, sizeof(short)));
            var monthToSecond = dateTimeBin.AsSpan(sizeof(short), 5);
            return new DateTime(year, monthToSecond[0], monthToSecond[1], monthToSecond[2], monthToSecond[3], monthToSecond[4]);
        }

        static byte[] DateTimeToBin(DateTime dateTime)
        {
            using var memStream = new MemoryStream(new byte[DATE_TIME_SIZE]);
            memStream.Write(BitConverter.GetBytes((short)dateTime.Year), 0, sizeof(short));
            memStream.WriteByte((byte)dateTime.Month);
            memStream.WriteByte((byte)dateTime.Day);
            memStream.WriteByte((byte)dateTime.Hour);
            memStream.WriteByte((byte)dateTime.Minute);
            memStream.WriteByte((byte)dateTime.Second);
            return memStream.ToArray();
        }
    }
}
