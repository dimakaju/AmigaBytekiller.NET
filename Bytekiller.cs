/* 
 * Amiga Bytekiller.NET (w) by Alexander Dimitriadis
 * Based on a portable C-source by Frank Wille
 * Original Motorola 68000 code by Lord Blitter '87
 * 
 */
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace Teyko.Amiga.Cruncher
{
  public class Bytekiller
  {
    public static void Crunch(string infile, string outfile, uint scanWidth)
    {
      new Bytekiller().BkCrunch(infile, outfile, scanWidth);
    }

    public static void DeCrunch(string infile, string outfile)
    {
      new Bytekiller().BkDecrunch(infile, outfile);
    }

    private readonly uint[] MaxOffsets = { 0x100, 0x200, 0x400, 0x1000 };
    private readonly int[] OffsBits = { 8, 9, 10, 12 };
    private readonly int[] CmdBits = { 2, 3, 3, 3 };
    private readonly uint[] CmdWord = { 1, 4, 5, 6 };
    private readonly uint Headersize = 12;

    private List<byte> ResultBuffer { get; set; } = new List<byte>();

    private int FreeBits { get; set; } = 32;
    private uint BitStream { get; set; } = 0;
    private uint Checksum { get; set; } = 0;
    private bool IsLittleEndian { get; set; } = BitConverter.IsLittleEndian;
    private uint SourcePtr { get; set; } = 0;
    private byte[] SourceBuffer;

    private void BkCrunch(string infile, string outfile, uint scanWidth)
    {
      SourceBuffer = File.ReadAllBytes(infile);
      uint scanend = 0, bestlen = 0, scanptr = 0, copylen = 0, copytype = 0, besttype = 0, dumpcnt = 0;
      uint copyoffs = 0, bestoffs = 0;
      
      while (SourcePtr < SourceBuffer.Length)
      {
        scanend = (SourcePtr + scanWidth > (uint)SourceBuffer.Length) ? (uint)SourceBuffer.Length - 1 : SourcePtr + scanWidth;
        bestlen = 1;
        scanptr = SourcePtr + 1;

        while (scanptr < scanend)
        {
          if (SourceBuffer[SourcePtr] == SourceBuffer[scanptr] && SourceBuffer[SourcePtr + 1] == SourceBuffer[scanptr + 1])
          {
            while ((scanptr + copylen) < scanend && SourceBuffer[SourcePtr + copylen] == SourceBuffer[scanptr + copylen])
              copylen++;

            if (copylen > bestlen)
            {
              copyoffs = scanptr - SourcePtr;
              if (copylen > 4)
              {
                copytype = 3;
                copylen = copylen > 0x100 ? 0x100 : copylen;
              }
              else
              {
                copytype = copylen - 2;
              }

              if (copyoffs < MaxOffsets[copytype])
              {
                bestlen = copylen;
                bestoffs = copyoffs;
                besttype = copytype;
              }
            }

            scanptr += copylen;
            copylen = 0;
          }
          else
          {
            scanptr++;
          }
        }

        if (bestlen > 1)
        {
          BkDump(dumpcnt);
          dumpcnt = 0;
          BkWriteBits(OffsBits[besttype], bestoffs);
          if (besttype == 3)
            BkWriteBits(8, bestlen - 1);
          BkWriteBits(CmdBits[besttype], CmdWord[besttype]);
          SourcePtr += bestlen;
        }
        else
        {
          BkWriteBits(8, SourceBuffer[SourcePtr++]);
          if (++dumpcnt >= 0x108)
          {
            BkDump(dumpcnt);
            dumpcnt = 0;
          }
        }
      }

      BkDump(dumpcnt);
      BitStream |= Convert.ToUInt32(1L << (32 - FreeBits));
      BkWriteBitStream();

      InsertIntoResultBuffer(0, Checksum);
      InsertIntoResultBuffer(0, (uint)SourceBuffer.Length);
      InsertIntoResultBuffer(0, (uint)ResultBuffer.Count - 8);

      File.WriteAllBytes(outfile, ResultBuffer.ToArray());
    }

    private void BkDecrunch(string infile, string outfile)
    {
      SourceBuffer = File.ReadAllBytes(infile);
      uint len = ReadUInt32(SourceBuffer, 4);
      SourcePtr = ReadUInt32(SourceBuffer, 0) + Headersize;
      Checksum = ReadUInt32(SourceBuffer, 8);
      BkNextBitStream();

      do
      {
        if (BkNextBit() == 1)
        {
          uint type = BkReadBits(2);
          if (type < 2)
            BkDoDuplicate(type + 9, type + 2);
          else if (type == 3)
            BkDoJmp(8, 8);
          else
            BkDoDuplicate(12, BkReadBits(8));
        }
        else
        {
          if (BkNextBit() == 1)
            BkDoDuplicate(8, 1);
          else
            BkDoJmp(3, 0);
        }
      }
      while (ResultBuffer.Count < len);
      File.WriteAllBytes(outfile, ResultBuffer.ToArray());
    }

    private void BkDoJmp(uint len, uint off)
    {
      uint count = BkReadBits(len) + off + 1;

      while (count-- > 0)
        InsertIntoResultBuffer(0, (byte)BkReadBits(8));
    }

    private void BkDoDuplicate(uint len, uint count)
    {
      uint off = BkReadBits(len);
      count += 1;

      while (count-- > 0)
        InsertIntoResultBuffer(0, ResultBuffer[(int)off - 1]);
    }

    private uint BkReadBits(uint count)
    {
      uint r = 0;

      while (count-- > 0)
        r = r << 1 | BkNextBit();

      return r;
    }

    private uint BkNextBit()
    {
      uint r = (BitStream & 1);
      BitStream >>= 1;

      if (BitStream == 0)
      {
        BkNextBitStream();
        r = (BitStream & 1);
        BitStream = BitStream >> 1 | 0x80000000;
      }

      return r;
    }

    private void BkNextBitStream()
    {
      SourcePtr -= 4;
      BitStream = ReadUInt32(SourceBuffer, SourcePtr);
      Checksum ^= BitStream;
    }

    private void BkDump(uint n)
    {
      if (n >= 9)
        BkWriteBits(11, 0x0700 | (n - 9));
      else if (n >= 1)
        BkWriteBits(5, n - 1);
    }

    private void BkWriteBits(int bits, uint val)
    {
      while (bits-- > 0)
      {
        BitStream = (BitStream << 1) | (val & 1);
        val >>= 1;
        if (--FreeBits == 0)
          BkWriteBitStream();
      }
    }

    private void BkWriteBitStream()
    {
      AppendToResultBuffer(BitStream);
      Checksum ^= BitStream;
      BitStream = 0;
      FreeBits = 32;
    }

    private void AppendToResultBuffer(uint v) => ResultBuffer.AddRange(ToBytes(v));
    private void InsertIntoResultBuffer(int offset, uint v) => ResultBuffer.InsertRange(offset, ToBytes(v));
    private void InsertIntoResultBuffer(int offset, byte v) => ResultBuffer.Insert(offset, v);
    private byte[] ToBytes(uint v) => IsLittleEndian ? BitConverter.GetBytes(v).Reverse().ToArray() : BitConverter.GetBytes(v).ToArray();
    private uint ReadUInt32(byte[] input, uint startIndex) => IsLittleEndian ? BitConverter.ToUInt32(input.Skip(Convert.ToInt32(startIndex)).Take(4).Reverse().ToArray(), 0) : BitConverter.ToUInt32(input.Skip(Convert.ToInt32(startIndex)).Take(4).ToArray(), 0);
  }
}
