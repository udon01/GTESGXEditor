using GTESGXEditor_JP.Properties;
using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GTESGXEditor_JP.Entities
{
    public class SGXDEntry
    {
        public uint namePointer, dataOffset;
        public uint fileSize, unknown;
        public NameChunk nameChunk;
        public WaveChunk waveChunk;
        public byte[] audioStream;
        public string audioStreamName;

        public SGXDEntry()
        {
            nameChunk = new NameChunk();
            waveChunk = new WaveChunk();
        }

        public void WriteVAG(string path, int rpmfrequency)
        {
            byte[] zero16 = new byte[16] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            byte[] audioStream16 = new byte[16];
            Array.Copy(audioStream, 0, audioStream16, 0, 16);
            bool isEqual = true;

            //同一のインスタンスの時は、同じとする
            if (ReferenceEquals(zero16, audioStream16))
                isEqual = true;

            //どちらかがNULLか、要素数が異なる時は、同じではない
            else if (zero16 == null || audioStream16 == null || zero16.Length != audioStream16.Length)
                isEqual = false;

            else
            {
                for (int i = 0; i < zero16.Length; i++)
                {
                    if (!zero16[i].Equals(audioStream16[i]))
                    {
                        //1つでも等しくない要素があれば、同じではない
                        isEqual = false;
                        break;
                    }
                }
            }

            using (var file = new FileStream(string.Format("{0}.vag", Path.Combine(path, nameChunk.fileName)), FileMode.Create, FileAccess.Write))
            using (var stream = new BinaryStream(file, ByteConverter.Big))
            {
                stream.Position = 0;
                stream.WriteString("VAGp", StringCoding.Raw);
                stream.WriteUInt32(32);
                stream.Position += 4;
                if (isEqual == true)
                    stream.WriteUInt32((uint)(audioStream.Length - 16));
                else if (isEqual == false)
                    stream.WriteUInt32((uint)(audioStream.Length));
                stream.WriteUInt32((uint)(rpmfrequency * 10));
                //stream.WriteUInt32(44100);
                stream.Position += 12;

                if (isEqual == false)
                    stream.Position += 16;
                stream.WriteBytes(audioStream);
            }
        }

        //intをbyte配列4バイト(ビッグエンディアン)に変換して戻す
        public static byte[] Getbighex4(int hex)
        {
            string hexstr = hex.ToString("X");
            if (hexstr.Length == 1 || hexstr.Length == 3 || hexstr.Length == 5 || hexstr.Length == 7)
                hexstr = "0" + hexstr;

            if (hexstr.Length == 2)
                hexstr = "000000" + hexstr;

            else if (hexstr.Length == 4)
                hexstr = "0000" + hexstr;

            else if (hexstr.Length == 6)
                hexstr = "00" + hexstr;

            byte[] hexbyte = new byte[4];
            hexbyte = StringToBytes(hexstr);
            return hexbyte;
        }

        //byte配列2バイトをintに変換して戻す
        public static int Getbyte2(byte[] bytes, int seek)
        {
            byte[] byte1 = new byte[1];
            Array.Copy(bytes, seek, byte1, 0, 1);
            byte[] byte2 = new byte[1];
            Array.Copy(bytes, seek + 1, byte2, 0, 1);

            string str1 = BitConverter.ToString(byte1);
            string str2 = BitConverter.ToString(byte2);
            int bytelength = 0;

            if (str2 != "00")
            {
                bytelength = 2;
                goto label_byteget;
            }
            else if (str1 != "00")
            {
                bytelength = 1;
                goto label_byteget;
            }

            else
                return 0;

            label_byteget:;

            string str16 = "";
            if (bytelength == 1)
                str16 = str1;
            else if (bytelength == 2)
                str16 = str2 + str1;

            int returnint = Convert.ToInt32(str16, 16);

            return returnint;
        }

        // 16進数文字列 => Byte配列
        public static byte[] StringToBytes(string str)
        {
            var bs = new List<byte>();
            for (int i = 0; i < str.Length / 2; i++)
            {
                bs.Add(Convert.ToByte(str.Substring(i * 2, 2), 16));
            }
            return bs.ToArray();
        }
    }
}
