using GTESGXEditor.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GTESGXEditor.Entities
{
    public class SGXDEntry
    {
        public uint namePointer, dataOffset;
        public ushort fileSize, unknown;
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
            byte[] WriteVAG_byte = new byte[0];
            byte[] vag_ext = new byte[4] { 0x56, 0x41, 0x47, 0x70 };
            byte[] vag_20 = new byte[4] { 0x00, 0x00, 0x00, 0x20 };
            byte[] vag_zero4 = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
            int vag_length = audioStream.Length - 16;
            byte[] vag_length_byte = new byte[4];
            vag_length_byte = Getbighex4(vag_length);
            //int rpmfrequency10 = rpmfrequency * 10;
            int rpmfrequency10 = 44100;
            byte[] rpmfrequency10_byte = new byte[4];
            rpmfrequency10_byte = Getbighex4(rpmfrequency10);
            Array.Resize(ref WriteVAG_byte, WriteVAG_byte.Length + 4);
            Array.Copy(vag_ext, 0, WriteVAG_byte, WriteVAG_byte.Length - 4, 4);
            Array.Resize(ref WriteVAG_byte, WriteVAG_byte.Length + 4);
            Array.Copy(vag_20, 0, WriteVAG_byte, WriteVAG_byte.Length - 4, 4);
            Array.Resize(ref WriteVAG_byte, WriteVAG_byte.Length + 4);
            Array.Copy(vag_zero4, 0, WriteVAG_byte, WriteVAG_byte.Length - 4, 4);
            Array.Resize(ref WriteVAG_byte, WriteVAG_byte.Length + 4);
            Array.Copy(vag_length_byte, 0, WriteVAG_byte, WriteVAG_byte.Length - 4, 4);
            Array.Resize(ref WriteVAG_byte, WriteVAG_byte.Length + 4);
            Array.Copy(rpmfrequency10_byte, 0, WriteVAG_byte, WriteVAG_byte.Length - 4, 4);
            for (int i = 0; i < 3; i++)
            {
                Array.Resize(ref WriteVAG_byte, WriteVAG_byte.Length + 4);
                Array.Copy(vag_zero4, 0, WriteVAG_byte, WriteVAG_byte.Length - 4, 4);
            }
            Array.Resize(ref WriteVAG_byte, WriteVAG_byte.Length + audioStream.Length);
            Array.Copy(audioStream, 0, WriteVAG_byte, WriteVAG_byte.Length - audioStream.Length, audioStream.Length);

            FileStream fsw = new FileStream(string.Format("{0}.vag", Path.Combine(path, nameChunk.fileName)), 
                                            FileMode.Create, FileAccess.Write);
            fsw.Write(WriteVAG_byte, 0, WriteVAG_byte.Length);
            fsw.Close();
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
