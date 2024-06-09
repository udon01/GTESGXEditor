using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace GTESGXEditor.Entities
{
    public class ESGXEntry
    {
        public const string magic = "ESGX", esMagic = "ENGN";
        public uint SGXDPointer, sampleAmount, settingsPointer, soundStartPointer, audioChunkSize;
        public byte[] unk;

        public List<SampleSetting> sampleSettings = new List<SampleSetting>();
        public List<SGXDEntry> sgxdEntries = new List<SGXDEntry>();

        public void ReadFile(string path)
        {
            var bytes = File.ReadAllBytes(path);

            using (var stream = new BinaryStream(new MemoryStream(bytes)))
            {
                if (stream.ReadString(4) != magic)
                    throw new InvalidDataException("Not an ESGX file. Please open an ESGX file and try again.");

                stream.Position += 4;

                SGXDPointer = stream.ReadUInt32();
                sampleAmount = stream.ReadUInt32();
                settingsPointer = stream.ReadUInt32();
                unk = stream.ReadBytes(4);

                stream.Position = settingsPointer;
                for (int i = 0; i < sampleAmount; i++)
                {
                    SampleSetting sample = new SampleSetting();
                    sample.rpmPitch = stream.ReadInt16();
                    sample.rpmStart = stream.ReadInt16();
                    sample.rpmEnd = stream.ReadInt16();
                    sample.rpmVolume = stream.ReadInt16();
                    sample.rpmFrequency = stream.ReadInt32();
                    sample.SGXDOffset = stream.ReadInt32();

                    sampleSettings.Add(sample);
                }

                stream.Position += 12;

                for (int i = 0; i < sampleAmount; i++)
                {
                    stream.Position = sampleSettings[i].SGXDOffset;

                    SGXDEntry entry = new SGXDEntry();
                    entry.waveChunk = new WaveChunk();
                    entry.nameChunk = new NameChunk();

                    stream.Position += 4;

                    entry.namePointer = stream.ReadUInt32();
                    entry.dataOffset = stream.ReadUInt32();
                    entry.fileSize = stream.ReadUInt32();
                    entry.fileSize -= 2147483648;
                    //entry.unknown = stream.ReadUInt16();

                    stream.Position += 4;

                    entry.waveChunk.chunkSize = stream.ReadUInt32();
                    stream.Position += 4;
                    entry.waveChunk.soundAmount = stream.ReadUInt32();
                    entry.waveChunk.flag2 = stream.ReadUInt32();
                    entry.waveChunk.nameOffset = stream.ReadUInt32();
                    entry.waveChunk.codecType = stream.Read1Byte();
                    entry.waveChunk.channels = stream.Read1Byte();
                    stream.Position += 2;
                    entry.waveChunk.soundSampleRate = stream.ReadUInt32();
                    entry.waveChunk.bitRate = stream.ReadUInt32();
                    stream.Position += 4;
                    entry.waveChunk.volumeL = stream.ReadUInt16();
                    entry.waveChunk.volumeR = stream.ReadUInt16();
                    stream.Position += 8;
                    entry.waveChunk.loopStartSample = stream.ReadUInt32();
                    entry.waveChunk.loopEndSample = stream.ReadUInt32();
                    entry.waveChunk.streamSize = stream.ReadUInt32();

                    stream.Position += 20;

                    entry.nameChunk.chunkSize = stream.ReadUInt32();
                    entry.nameChunk.unknown = stream.ReadBytes(24);
                    entry.nameChunk.fileName = stream.ReadString(StringCoding.ZeroTerminated);

                    stream.Position = sampleSettings[i].SGXDOffset + entry.dataOffset;

                    entry.audioStream = stream.ReadBytes((int)entry.fileSize);
                    
                    sgxdEntries.Add(entry);
                }
            }
        }

        public void ReadESFile(string path)
        {
            var bytes = File.ReadAllBytes(path);

            using (var stream = new BinaryStream(new MemoryStream(bytes), ByteConverter.Little))
            {
                if (stream.ReadString(4) != esMagic)
                    throw new InvalidDataException("Not an ES file. Please open an ES file and try again.");

                stream.Position += 4;

                soundStartPointer = stream.ReadUInt32();

                stream.Position += 4;

                audioChunkSize = stream.ReadUInt32();

                stream.Position += 4;

                sampleAmount = stream.ReadUInt32();

                stream.Position += 20;


                for (int i = 0; i < sampleAmount; i++)
                {
                    SampleSetting sample = new SampleSetting();
                    sample.rpmPitch = stream.ReadInt16();
                    sample.rpmStart = stream.ReadInt16();
                    sample.rpmEnd = stream.ReadInt16();
                    sample.rpmVolume = stream.ReadInt16();
                    sample.rpmFrequency = stream.ReadInt32();
                    sample.SGXDOffset = stream.ReadInt32();

                    sampleSettings.Add(sample);
                }

                int j = 0;

                foreach (SampleSetting setting in sampleSettings)
                {
                    stream.Position = soundStartPointer + setting.SGXDOffset + 16;

                    SGXDEntry entry = new SGXDEntry();

                    entry.waveChunk = new WaveChunk();
                    entry.nameChunk.fileName = string.Format("{0}_{1}", Path.GetFileNameWithoutExtension(path), j);
                    entry.waveChunk.soundSampleRate = (uint)setting.rpmFrequency * 10;

                    if (j == 0)
                    {
                        entry.audioStream = stream.ReadBytes(sampleSettings[j].SGXDOffset);
                    }
                    if (j == sampleSettings.Count - 1)
                    {
                        entry.audioStream = stream.ReadBytes(int.Parse(stream.Length.ToString()) - int.Parse(stream.Position.ToString()));
                    }
                    else
                    {
                        entry.audioStream = stream.ReadBytes(sampleSettings[j + 1].SGXDOffset - sampleSettings[j].SGXDOffset - 16);
                    }

                    entry.fileSize = ushort.Parse(entry.audioStream.Length.ToString());

                    sgxdEntries.Add(entry);
                    j++;
                }
            }

            // Just set all RPM Frequencies to 4200 as this seems to work globally
            /*
            foreach (SampleSetting setting in sampleSettings)
            {
                setting.rpmFrequency = 4200;
            }
            */

            // Read loop start and end samples
            foreach (SGXDEntry entry in sgxdEntries)
            {
                //File.WriteAllBytes(string.Format("{0}.tmp", Path.Combine(Path.GetDirectoryName(path), "tempAudio")), entry.audioStream);

                //var test = new AudioFileReader(Path.Combine(Path.GetDirectoryName(path), "tempAudio")).Length;

                byte[] currentLine;
                int loopStart, loopEnd, numSamples;
                using (var stream = new BinaryStream(new MemoryStream(entry.audioStream)))
                {
                    numSamples = (entry.audioStream.Length / 16) * 28;

                    //stream.Position = 16;

                    while (stream.Position <= entry.audioStream.Length)
                    {

                        currentLine = stream.ReadBytes(16);

                        // Read second byte of a line - 6 = loop start, 3 = loop end, determine sample count from where we are in seek
                        if (currentLine[1] == 6)
                        {
                            entry.waveChunk.loopStartSample = (uint)(stream.Position - 16) / 16 * 28 - 28;
                        }

                        if (currentLine[1] == 3)
                        {
                            entry.waveChunk.loopEndSample = (uint)(stream.Position - 16) / 16 * 28;
                            break;
                        }
                    }
                }
            }
        }

        public bool Validate()
        {
            bool isValid = true;
            foreach (var sgxdEntry in sgxdEntries)
            {
                if (sgxdEntry.audioStream.Length == 0)
                {
                    isValid = false;
                }
                if (sgxdEntry.nameChunk.fileName.Length == 0)
                {
                    isValid = false;
                }
            }

            return isValid;
        }

        public void SaveFile(string path)
        {
            using (var file = new FileStream(path, FileMode.Create))
            using (var stream = new BinaryStream(file, ByteConverter.Little))
            {
                int i = 0;
                int[] zero16bool = new int[sgxdEntries.Count()];
                foreach (var sgxdEntry in sgxdEntries)
                {
                    byte[] zero16 = new byte[16] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    byte[] audioStream16 = new byte[16];
                    Array.Copy(sgxdEntry.audioStream, 0, audioStream16, 0, 16);

                    //同一のインスタンスの時は、同じとする
                    if (ReferenceEquals(zero16, audioStream16))
                        zero16bool[i] = 0;

                    //どちらかがNULLか、要素数が異なる時は、同じではない
                    else if (zero16 == null || audioStream16 == null || zero16.Length != audioStream16.Length)
                        zero16bool[i] = 1;

                    else
                    {
                        for (int j = 0; j < zero16.Length; j++)
                        {
                            if (!zero16[j].Equals(audioStream16[j]))
                            {
                                //1つでも等しくない要素があれば、同じではない
                                zero16bool[i] = 1;
                                break;
                            }
                        }
                    }
                    i++;
                }
                i = 0;

                stream.Position = 0;
                stream.WriteString("ESGX", StringCoding.Raw);

                stream.Position += 4;

                stream.WriteUInt32((uint)(0x24 + (0x10 * sampleSettings.Count) + 0xC));
                stream.WriteUInt32(sampleAmount);
                stream.WriteUInt32(36);

                stream.Position += 8;

                stream.WriteBytes(new byte[] { 0x0, 0x10, 0x0, 0x0, 0x0, 0x10 });

                stream.Position += 2;

                int[] namelength = new int[sgxdEntries.Count()];
                foreach (var sgxdEntry in sgxdEntries)
                {
                    namelength[i] = sgxdEntry.nameChunk.fileName.Length;
                    i++;
                }
                i = 0;

                uint cumulativeLength = 0;
                foreach (var sampleSetting in sampleSettings)
                {
                    stream.WriteInt16(sampleSetting.rpmPitch);
                    stream.WriteInt16(sampleSetting.rpmStart);
                    stream.WriteInt16(sampleSetting.rpmEnd);
                    stream.WriteInt16(sampleSetting.rpmVolume);
                    stream.WriteInt32(sampleSetting.rpmFrequency);
                    stream.WriteInt32((int)(0x24 + (0x10 * sampleSettings.Count) + (0xA0 * i) + cumulativeLength + 0xC)); // ESGX header is 0x24 long, Sample Settings 0x10 long, SGXD header 0xA0 long + each audio stream size, then 12 unknown bytes before first SGXD

                    cumulativeLength += sgxdEntries[i].fileSize;
                    if (zero16bool[i] == 0)
                        cumulativeLength -= 16;
                    if (namelength[i] >= 16)
                        cumulativeLength += 16;
                    i++;
                }

                i = 0;

                stream.Position += 12;

                foreach (var sgxdEntry in sgxdEntries)
                {
                    // All hardcoded variables here are those which don't seem to be used by GT5/6, so seems safe to write the same every time

                    stream.WriteString("SGXD", StringCoding.Raw);
                    stream.WriteUInt32(0x80);
                    int namelength_write = 160 + ((sgxdEntry.nameChunk.fileName.Length - 1) / 16 * 16);
                    stream.WriteUInt32((uint)namelength_write);
                    uint filesize = sgxdEntry.fileSize;
                    if (zero16bool[i] == 0)
                        filesize -= 16;
                    stream.WriteUInt32(filesize);
                    stream.Position -= 1;
                    stream.WriteByte(0x80);
                    stream.WriteString("WAVE", StringCoding.Raw);
                    stream.WriteUInt32(72);

                    stream.Position += 4;

                    stream.WriteUInt32(1);
                    stream.WriteUInt32(0);
                    stream.WriteUInt32(0);
                    stream.WriteByte(0x03);
                    stream.WriteByte(0x01);

                    stream.Position += 2;

                    stream.WriteUInt32(44100);

                    stream.Position += 8;

                    stream.WriteUInt16(4096);
                    stream.WriteUInt16(4096);
                    stream.Position += 4;
                    stream.WriteUInt32(sgxdEntry.waveChunk.loopEndSample);
                    stream.WriteUInt32(sgxdEntry.waveChunk.loopStartSample);
                    stream.WriteUInt32(sgxdEntry.waveChunk.loopEndSample);
                    stream.WriteUInt32(filesize);

                    stream.Position += 16;

                    stream.WriteString("NAME", StringCoding.Raw);
                    stream.WriteUInt32(56);

                    // Unknown byte chunk, but doesn't change between files
                    stream.WriteBytes(new byte[] { 0x0, 0x0, 0x0, 0x0, 0x02, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x30, 0x80, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x80, 0x0, 0x0, 0x0 });
                    stream.WriteString(sgxdEntry.nameChunk.fileName, StringCoding.Raw);

                    int namelength_write_2 = 32 + (sgxdEntry.nameChunk.fileName.Length - 1) / 16 * 16;
                    stream.Position += namelength_write_2 - sgxdEntry.nameChunk.fileName.Length;

                    if (zero16bool[i] == 0)
                    {
                        byte[] audioStreamnot16 = new byte[sgxdEntry.audioStream.Length - 16];
                        Array.Copy(sgxdEntry.audioStream, 16, audioStreamnot16, 0, audioStreamnot16.Length);
                        stream.WriteBytes(audioStreamnot16);
                    }

                    if (zero16bool[i] == 1)
                        stream.WriteBytes(sgxdEntry.audioStream);

                    i++;
                }
                i = 0;
            }
        }

        public void SaveFile_es(string path)
        {
            using (var file = new FileStream(path, FileMode.Create))
            using (var stream = new BinaryStream(file, ByteConverter.Little))
            {
                int i = 0;
                int[] zero16bool = new int[sgxdEntries.Count()];
                foreach (var sgxdEntry in sgxdEntries)
                {
                    byte[] zero16 = new byte[16] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    byte[] audioStream16 = new byte[16];
                    Array.Copy(sgxdEntry.audioStream, 0, audioStream16, 0, 16);

                    //同一のインスタンスの時は、同じとする
                    if (ReferenceEquals(zero16, audioStream16))
                        zero16bool[i] = 0;

                    //どちらかがNULLか、要素数が異なる時は、同じではない
                    else if (zero16 == null || audioStream16 == null || zero16.Length != audioStream16.Length)
                        zero16bool[i] = 1;

                    else
                    {
                        for (int j = 0; j < zero16.Length; j++)
                        {
                            if (!zero16[j].Equals(audioStream16[j]))
                            {
                                //1つでも等しくない要素があれば、同じではない
                                zero16bool[i] = 1;
                                break;
                            }
                        }
                    }
                    i++;
                }
                i = 0;

                stream.Position = 0;
                stream.WriteString("ENGN", StringCoding.Raw);

                stream.Position += 4;

                stream.WriteUInt32((uint)(0x30 + (0x10 * sampleSettings.Count)));

                stream.Position += 4;

                int filelength = sampleSettings.Count * 16;
                foreach (var sgxdEntry in sgxdEntries)
                {
                    filelength += sgxdEntry.audioStream.Length;
                    if (zero16bool[i] == 0)
                        filelength -= 16;
                }
                stream.WriteUInt32((uint)filelength);

                stream.WriteUInt32((uint)(0x30 + (0x10 * sampleSettings.Count)));
                stream.WriteUInt32((uint)sampleSettings.Count);
                stream.WriteUInt32(0x30);

                stream.Position += 8;

                stream.WriteUInt32(0x7F);
                stream.WriteUInt32(0x7F);

                uint cumulativeLength = 0;
                foreach (var sampleSetting in sampleSettings)
                {
                    stream.WriteInt16(sampleSetting.rpmPitch);
                    stream.WriteInt16(sampleSetting.rpmStart);
                    stream.WriteInt16(sampleSetting.rpmEnd);
                    stream.WriteInt16(sampleSetting.rpmVolume);
                    stream.WriteInt32(sampleSetting.rpmFrequency);
                    stream.WriteInt32((int)cumulativeLength);

                    cumulativeLength += sgxdEntries[i].fileSize;
                    if (zero16bool[i] == 1)
                        cumulativeLength += 16;
                    i++;
                }
                i = 0;

                foreach (var sgxdEntry in sgxdEntries)
                {
                    if (zero16bool[i] == 0)
                        stream.WriteBytes(sgxdEntry.audioStream);
                    else if (zero16bool[i] == 1)
                    {
                        stream.Position += 16;
                        stream.WriteBytes(sgxdEntry.audioStream);
                    }
                    i++;
                }
                i = 0;
            }
        }
    }
}
