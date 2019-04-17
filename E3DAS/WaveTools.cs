using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Security.Cryptography;
using IRTools;

namespace WaveTools
{
  public class WavHeader
  {
    /// <summary>
    /// Contains the letters "RIFF" in ASCII form (0x52494646 big-endian form).
    /// </summary>
    public byte[] RiffID;
    /// <summary>
    /// The size of the entire file in bytes minus 8 bytes. RiffID and FileSize are not included in this count
    /// </summary>
    public int FileSize;
    /// <summary>
    /// Contains the letters "WAVE" (0x57415645 big-endian form).
    /// </summary>
    public byte[] WaveID;
    /// <summary>
    /// Contains the letters "fmt " (0x666d7420 big-endian form).
    /// </summary>
    public byte[] FmtID;
    /// <summary>
    /// This is the size of the rest of the Subchunk which follows this number. 16 for PCM (FmtCode = 1). 
    /// </summary>
    public int FmtSize = 16;
    /// <summary>
    /// PCM = 1 (i.e. Linear quantization). Values other than 1 indicate some form of compression.
    /// </summary>
    public short FmtCode = 1;
    /// <summary>
    /// Mono = 1, Stereo = 2, etc.
    /// </summary>
    public short Channels;
    /// <summary>
    /// 44100, 48000, 96000, etc. The number of samples per durationonds.
    /// </summary>
    public int SampleRate;
    /// <summary>
    /// == SampleRate * Channels * BitDepth / 8. The number of bytes per durationonds.
    /// </summary>
    public int ByteRate;
    /// <summary>
    /// == Channels * BitDepth / 8. The number of bytes for one sample including all channels.
    /// </summary>
    public short BlockAlign;
    /// <summary>
    /// The number of bits per sample. 8, 16, 24, 32, etc.
    /// </summary>
    public short BitDepth;
    /// <summary>
    /// Contains the letters "data" (0x64617461 big-endian form).
    /// </summary>
    public byte[] DataID;
    /// <summary>
    /// == (Number of Samples) * Channels * BitDepth / 8. The number of bytes in the data.
    /// </summary>
    public int DataSize;
  }

  public class Wave
  {
    private WavHeader _wavHeaderArgs = new WavHeader();
    private byte[] _wavRawData;
    private double[,] _waveData;

    /// <summary>
    /// Time length of audio source [msec] 
    /// </summary>
    private int _timeLength;

    /// <summary>
    /// Order of Channels. 0: Left, 1: Right, 2: Center, 3: LFE, 4: BL, 5: BR, 6: SL, 7: sampleRate 
    /// </summary>
    private int _channelOrder;

    public WavHeader Header
    {
      get { return _wavHeaderArgs; }
      protected set { }
    }

    /// <summary>
    /// Values of wave data. Data[channel, value] or Data[azimuth, value]
    /// </summary>
    public double[,] Data
    {
      get { return _waveData; }
      protected set { }
    }

    /// <summary>
    /// Time length of wave file [mduration]
    /// </summary>
    public int TimeLength
    {
      get { return _timeLength; }
      protected set { }
    }

    public void WavRead(string folderPath, string fileName)
    {
      string filePath = $"{folderPath}\\{fileName}.wav";
      if (!File.Exists(filePath))
      {
        throw new FileNotFoundException();
      }

      using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
      using (BinaryReader br = new BinaryReader(fs))
      {
        _wavHeaderArgs.RiffID = br.ReadBytes(4);
        _wavHeaderArgs.FileSize = br.ReadInt32();
        _wavHeaderArgs.WaveID = br.ReadBytes(4);

        bool hasFmtChunk = false;
        bool hasDataChunk = false;
        while (!hasFmtChunk || !hasDataChunk)
        {
          byte[] chunkID = br.ReadBytes(4);

          if (chunkID.SequenceEqual(Encoding.ASCII.GetBytes("fmt ")))
          {
            _wavHeaderArgs.FmtID = chunkID;
            _wavHeaderArgs.FmtSize = br.ReadInt32();
            _wavHeaderArgs.FmtCode = br.ReadInt16();
            _wavHeaderArgs.Channels = br.ReadInt16();
            _wavHeaderArgs.SampleRate = br.ReadInt32();
            _wavHeaderArgs.ByteRate = br.ReadInt32();
            _wavHeaderArgs.BlockAlign = br.ReadInt16();
            _wavHeaderArgs.BitDepth = br.ReadInt16();

            hasFmtChunk = true;
          }
          else if (chunkID.SequenceEqual(Encoding.ASCII.GetBytes("data")))
          {
            _wavHeaderArgs.DataID = chunkID;
            _wavHeaderArgs.DataSize = br.ReadInt32();
            _wavRawData = br.ReadBytes(_wavHeaderArgs.DataSize);

            CalculateTimeLength(_wavHeaderArgs);
            DivideEachChannel(_wavHeaderArgs, _wavRawData);

            hasDataChunk = true;
          }
          else
          {
            Int32 size = br.ReadInt32();
            if (size > 0)
            {
              br.ReadBytes(size);
            }
          }
        }
      }
    }

    private void DivideEachChannel(WavHeader WavHeader, byte[] waveRawData)
    {
      if (WavHeader.BitDepth < 16 && WavHeader.BitDepth > 32)
        throw new ArgumentOutOfRangeException();

      int bytePerChannel = WavHeader.DataSize / WavHeader.Channels;
      int dataLengthPerCH = bytePerChannel / (WavHeader.BitDepth / 8);
      _waveData = new double[WavHeader.Channels, dataLengthPerCH];

      int index;
      Int16 LowByte16;
      Int16 HighByte16;
      Int32 LowByte;
      Int32 MidByte;
      Int32 HighByte;
      Int32 LLByte32;
      Int32 MLByte32;
      Int32 MHByte32;
      Int32 HHByte32;
      switch (WavHeader.BitDepth)
      {
        case 16:
          for (_channelOrder = 0; _channelOrder < WavHeader.Channels; _channelOrder++)
          {
            index = 0;
            for (int byteCnt = _channelOrder * 2; byteCnt < _wavRawData.Length; byteCnt += WavHeader.BlockAlign)
            {
              LowByte16 = _wavRawData[byteCnt];
              HighByte16 = (Int16)(_wavRawData[byteCnt + 1] << 8);
              _waveData[_channelOrder, index] = (HighByte16 | LowByte16);
              _waveData[_channelOrder, index] /= 0x8000; // Normalize
              index++;
            }
          }
          break;

        case 24:
          for (_channelOrder = 0; _channelOrder < WavHeader.Channels; _channelOrder++)
          {
            index = 0;
            for (int byteCnt = _channelOrder * 3; byteCnt < _wavRawData.Length; byteCnt += WavHeader.BlockAlign)
            {
              LowByte = _wavRawData[byteCnt];
              MidByte = _wavRawData[byteCnt + 1] << 8;
              HighByte = _wavRawData[byteCnt + 2] << 16;
              _waveData[_channelOrder, index] = (HighByte | MidByte | LowByte);
              if (_waveData[_channelOrder, index] > 0x800000) // Sign check
                _waveData[_channelOrder, index] -= 0x1000000;
              _waveData[_channelOrder, index] /= 0x800000; // Normalize
              index++;
            }
          }
          break;

        case 32: // experimental
          for (_channelOrder = 0; _channelOrder < WavHeader.Channels; _channelOrder++)
          {
            index = 0;
            for (int byteCnt = _channelOrder * 4; byteCnt < _wavRawData.Length; byteCnt += WavHeader.BlockAlign)
            {
              LLByte32 = _wavRawData[byteCnt];
              MLByte32 = _wavRawData[byteCnt + 1] << 8;
              MHByte32 = _wavRawData[byteCnt + 2] << 16;
              HHByte32 = _wavRawData[byteCnt + 3] << 24;
              _waveData[_channelOrder, index] = (HHByte32 | MHByte32 | MLByte32 | LLByte32);
              //_waveData[_channelOrder, index] /= 0x80000000; // Normalize
              index++;
            }
          }

          break;

        default:
          throw new InvalidDataException();
      }
    }

    private void CalculateTimeLength(WavHeader WavHeader)
    {
      int BPS = WavHeader.SampleRate * WavHeader.Channels * WavHeader.BlockAlign;
      _timeLength = (WavHeader.DataSize / BPS) * 1000;
    }

    public void ConvertStereoToMono()
    {
      if (_waveData == null | _waveData.GetLength(0) != 2)
      {
        throw new InvalidDataException();
      }

      int DataLength = _waveData.GetLength(1);

      double[,] temp = new double[2, DataLength];
      for (int chcnt = 0; chcnt < 2; chcnt++)
        for (int datcnt = 0; datcnt < DataLength; datcnt++)
        {
          temp[chcnt, datcnt] = _waveData[chcnt, datcnt];
        }

      _waveData = new double[1, DataLength];
      for (UInt32 datcnt = 0; datcnt < DataLength; datcnt++)
      {
        _waveData[0, datcnt] = (temp[0, datcnt] + temp[1, datcnt]) / 2.0;
      }

      GenerateHeaderData();
    }

    public void WaveGainChanger(int channel, double gain)
    {
      if (_waveData == null)
      {
        throw new InvalidDataException();
      }
      else if (!(channel < _wavHeaderArgs.Channels))
      {
        throw new ArgumentOutOfRangeException();
      }
      else if (gain > 1.0)
      {
        throw new ArgumentOutOfRangeException();
      }

      int DataLength = _waveData.GetLength(1);
      for (UInt32 datcnt = 0; datcnt < DataLength; datcnt++)
      {
        _waveData[channel, datcnt] *= gain;
      }

      GenerateHeaderData();
    }

    /// <summary>
    /// Change data length of sound data for the power of 2. 
    /// </summary>
    public void WaveLengthChanger()
    {
      if (_waveData == null | _wavHeaderArgs == null)
      {
        throw new InvalidDataException();
      }

      int Channels = _waveData.GetLength(0);
      int DataLength = _waveData.GetLength(1);

      double[,] temp = new double[Channels, DataLength];
      Array.Copy(_waveData, temp, _waveData.Length);

      int power = 0;
      while (DataLength > Math.Pow(2, power))
      {
        power++;
      }

      int NewDataLength = (int)Math.Pow(2, power);
      _waveData = new double[Channels, NewDataLength];
      for (int chcnt = 0; chcnt < Channels; chcnt++)
      {
        int datcnt;
        for (datcnt = 0; datcnt < DataLength; datcnt++)
        {
          _waveData[chcnt, datcnt] = temp[chcnt, datcnt];
        }
        for (datcnt = DataLength; datcnt < NewDataLength; datcnt++)
        {
          _waveData[chcnt, datcnt] = 0.0;
        }
      }

      GenerateHeaderData();
    }

    /// <summary>
    /// Change time length of sound data.
    /// </summary>
    /// <param name="newTimeLength">Play time[msec]</param>
    public void WaveLengthChanger(int newTimeLength)
    {
      if (_waveData == null | _wavHeaderArgs == null)
      {
        throw new InvalidDataException();
      }

      int Channels = _waveData.GetLength(0);
      int DataLength = _waveData.GetLength(1);

      double[,] temp = new double[Channels, DataLength];
      Array.Copy(_waveData, temp, _waveData.Length);
      
      int NewDataLength = (int)(_wavHeaderArgs.SampleRate * newTimeLength / 1000.0);
      _waveData = new double[Channels, NewDataLength];
      if (NewDataLength > DataLength)
      {
        for (int chcnt = 0; chcnt < Channels; chcnt++)
        {
          int datcnt;
          for (datcnt = 0; datcnt < DataLength; datcnt++)
          {
            _waveData[chcnt, datcnt] = temp[chcnt, datcnt];
          }
          for (datcnt = DataLength; datcnt < NewDataLength; datcnt++)
          {
            _waveData[chcnt, datcnt] = 0.0;
          }
        }
      }
      else
      {
        for (int chcnt = 0; chcnt < Channels; chcnt++)
        {
          for (int datcnt = 0; datcnt < NewDataLength; datcnt++)
          {
            _waveData[chcnt, datcnt] = temp[chcnt, datcnt];
          }
        }
      }

      GenerateHeaderData();
    }
    
    public void CopyWavData(Wave wave)
    {
      if (wave == null)
      {
        throw new InvalidDataException();
      }
      
      _waveData = new double[wave.Data.GetLength(0), wave.Data.Length];
      Array.Copy(wave.Data, _waveData, wave.Data.Length);

      GenerateHeaderData();
    }
    
    /// <summary>
    /// Convolution sound source and impulse response
    /// </summary>
    /// <param name="irFolderPath">Set folder path include IR csv data. Must rename "left", "right", "front" and "back" for those csv.</param>
    /// <param name="position">position -> 0: left, 1: right, 2: front, 3: back</param>
    /// <param name="AziStep">Step of Azimuth</param>
    /// <returns>Wave amplitude of each azimuth</returns>
    public double ConvolveIR(string irFolderPath, int position, int AziStep = 15)
    {
      var ir = new IR();
      ir.ReadfromCSV(irFolderPath);
      int DataLength = _waveData.GetLength(1);
      if (ir.Data == null)
      {
        throw new InvalidDataException();
      }
      else if (_waveData == null)
      {
        throw new InvalidDataException();
      }
      else if (!IsPowerOfTwo(DataLength))
      {
        throw new InvalidDataException();
      }
      else if(AziStep % 5 != 0 | AziStep < 5 | AziStep > 180)
      {
        throw new ArgumentException();
      }

      Complex[] zeros = new Complex[ir.SAMPLES];
      for (int i = 0; i < ir.SAMPLES; i++)
      {
        zeros[i] = Complex.Zero;
      }

      int SplitNum = DataLength / ir.SAMPLES;
      List<Complex>[] source = new List<Complex>[SplitNum];
      for (int splt = 0; splt < SplitNum; splt++)
      {
        source[splt] = new List<Complex>();
        for (int n = 0; n < ir.SAMPLES; n++)
        {
          source[splt].Add(_waveData[0, n + ir.SAMPLES * splt]);
        }
        source[splt].AddRange(zeros);
      }

      int AziNum = 360 / AziStep;
      int StepRate = AziStep / ir.AZISTEP;
      List<Complex>[] impulse = new List<Complex>[AziNum];
      for (int azi = 0; azi < AziNum; azi++)
      {
        impulse[azi] = new List<Complex>();
        for (int n = 0; n < ir.SAMPLES; n++)
        {
          impulse[azi].Add(ir.Data[position, StepRate * azi, n]);
        }
        impulse[azi].AddRange(zeros);
      }

      Console.Write("Convolving.");
      DataLength += ir.SAMPLES;
      double[,] data = new double[AziNum, DataLength];
      for (int azi = 0; azi < AziNum; azi++)
      {
        Complex[] pls = impulse[azi].ToArray();
        Fourier.Radix2Forward(pls, FourierOptions.Matlab);
        for (int splt = 0; splt < SplitNum; splt++)
        {
          Complex[] src = source[splt].ToArray();
          Fourier.Radix2Forward(src, FourierOptions.Matlab);
          for (int n = 0; n < 2 * ir.SAMPLES; n++)
          {
            src[n] = new Complex(src[n].Real * pls[n].Real - src[n].Imaginary * pls[n].Imaginary, src[n].Real * pls[n].Imaginary + pls[n].Real * src[n].Imaginary);
          }
          Fourier.Radix2Inverse(src, FourierOptions.Matlab);
          for (int n = 0; n < 2 * ir.SAMPLES; n++)
          {
            data[azi, n + splt * ir.SAMPLES] += src[n].Real;
          }
        }
        Console.Write(".");
      }
      Console.WriteLine();

      int revIndex = DataLength;
      if (data[0, revIndex - 1] == 0)
      {
        while (data[0, revIndex - 1] == 0)
        {
          revIndex--;
        }
      }

      _waveData = new double[AziNum, revIndex];
      var amp = new double[AziNum];
      for (int azi = 0; azi < AziNum; azi++)
      {
        double Max = 0;
        for (int datcnt = 0; datcnt < revIndex; datcnt++)
        {
          if (Math.Abs(data[azi, datcnt]) > Math.Abs(Max))
          {
            Max = data[azi, datcnt];
          }
        }
        amp[azi] = Math.Abs(Max);
      }
      for (int azi = 0; azi < AziNum; azi++)
      {
        for (int datcnt = 0; datcnt < revIndex; datcnt++)
        {
          _waveData[azi, datcnt] = data[azi, datcnt] / amp.Max();
        }
      }
      
      GenerateHeaderData();

      return amp.Max();
    }

    private bool IsPowerOfTwo(int num)
    {
      return (num & (num - 1)) == 0;
    }

    public void ConvineToMultiCh(Wave[] wave, int convCh, double[] amp, bool is6ch = true)
    {
      int ChNum = wave.Length;
      int DataLength = wave[0].Data.GetLength(1);

      for (int ch = 0; ch < ChNum; ch++)
      {
        if (wave[ch] == null)
        {
          throw new InvalidDataException();
        }
        else if (ChNum != amp.Length)
        {
          throw new InvalidDataException();
        }
        else if (DataLength != wave[ch].Data.GetLength(1))
        {
          FixToSameLength(wave);
        }
      }

      if (is6ch)
      {
        _waveData = new double[6, DataLength];
      }
      else
      {
        _waveData = new double[2, DataLength];
      }
      
      int chcnt = 0;
      while (chcnt != 6)
      {
        for (int datcnt = 0; datcnt < DataLength; datcnt++)
        {
          if (chcnt < ChNum)
          {
            _waveData[chcnt, datcnt] = wave[chcnt].Data[convCh, datcnt] * amp[chcnt] / amp.Max();
          }
          else
          {
            _waveData[chcnt, datcnt] = 0.0;
          }
        }
        chcnt++;
        if (!is6ch && chcnt == 2)
        {
          break;
        }
      }

      GenerateHeaderData();
    }

    private void FixToSameLength(Wave[] wave)
    {
      int DatNum = wave.Length;
      int[] len = new int[DatNum];
      for (int datcnt = 0; datcnt < DatNum; datcnt++)
      {
        len[datcnt] = wave[datcnt].Data.GetLength(1);
      }
      for (int datcnt = 0; datcnt < DatNum; datcnt++)
      {
        if (len[datcnt] < len.Max())
        {
          wave[datcnt].WaveLengthChanger((int)(1000.0 * len.Max() / wave[datcnt].Header.SampleRate));
        }
      }
    }

    public void ConvineToMultiChAmpExp(Wave[] wave, double[] amp)
    {
      int ChNum = wave.Length;
      int DataLength = wave[0].Data.GetLength(1);

      for (int ch = 0; ch < ChNum; ch++)
      {
        if (wave[ch] == null)
        {
          throw new InvalidDataException();
        }
        else if (ChNum != amp.Length)
        {
          throw new InvalidDataException();
        }
        else if (DataLength != wave[ch].Data.GetLength(1))
        {
          FixToSameLength(wave);
        }
      }
      
      _waveData = new double[6, DataLength];

      int[] order = { 3, 1, 0, 2 };
      int chcnt = 0;
      while (chcnt != 6)
      {
        for (int datcnt = 0; datcnt < DataLength; datcnt++)
        {
          if (chcnt < ChNum)
          {
            _waveData[chcnt, datcnt] = wave[chcnt].Data[order[chcnt], datcnt]; //  * amp[chcnt] / amp.Max()
          }
          else
          {
            _waveData[chcnt, datcnt] = 0.0;
          }
        }
        chcnt++;
      }

      GenerateHeaderData();
    }

    /// <summary>
    /// Write out wav file. _waveData is required. 
    /// </summary>
    public void WavWrite(string folderPath, string fileName, short bitDepth = 24, int sampleRate = 48000)
    {
      string outPath = $"{folderPath}\\{fileName}.wav";

      using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
      using (BinaryWriter bw = new BinaryWriter(fs))
      {
        GenerateHeaderData(bitDepth, sampleRate);
        bw.Write(HeaderBytes());

        int DataLength = _waveData.GetLength(1);
        for (UInt32 datcnt = 0; datcnt < DataLength; datcnt++)
        {
          for (int chcnt = 0; chcnt < _wavHeaderArgs.Channels; chcnt++)
          {
            switch (bitDepth)
            {
              case 16:
                short Data16 = (short)(_waveData[chcnt, datcnt] * 0x8000);
                bw.Write(BitConverter.GetBytes(Data16));
                break;

              case 24:
                Int32 Data24 = (Int32)(_waveData[chcnt, datcnt] * 0x800000);
                bw.Write((byte)(Data24 & 0x000000FF));
                bw.Write((byte)((Data24 & 0x0000FF00) >> 8));
                bw.Write((byte)((Data24 & 0x00FF0000) >> 16));
                break;
            }
          }
        }
      }
    }

    /// <summary>
    /// Generate meta data for header. _waveData is required. 
    /// </summary>
    private void GenerateHeaderData(short bitDepth = 24, int sampleRate = 48000)
    {
      if (_waveData == null)
      {
        throw new InvalidDataException();
      }
      _wavHeaderArgs.BitDepth = bitDepth;
      _wavHeaderArgs.SampleRate = sampleRate;
      _wavHeaderArgs.Channels = (short)_waveData.GetLength(0);
      int BytePerSample = ((ushort)(Math.Ceiling((double)_wavHeaderArgs.BitDepth / 8)));
      _wavHeaderArgs.BlockAlign = (short)(BytePerSample * _wavHeaderArgs.Channels);
      _wavHeaderArgs.ByteRate = _wavHeaderArgs.SampleRate * _wavHeaderArgs.Channels * BytePerSample;
      int DataLength = _waveData.GetLength(1);
      _wavHeaderArgs.DataSize = _wavHeaderArgs.BlockAlign * DataLength;
      _wavHeaderArgs.FileSize = _wavHeaderArgs.DataSize + 44;

      CalculateTimeLength(_wavHeaderArgs);
    }

    private byte[] HeaderBytes()
    {
      byte[] header = new byte[44];

      Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, header, 0, 4);
      Array.Copy(BitConverter.GetBytes((UInt32)(_wavHeaderArgs.FileSize - 8)), 0, header, 4, 4);
      Array.Copy(Encoding.ASCII.GetBytes("WAVE"), 0, header, 8, 4);
      Array.Copy(Encoding.ASCII.GetBytes("fmt "), 0, header, 12, 4);
      Array.Copy(BitConverter.GetBytes((UInt32)(_wavHeaderArgs.FmtSize)), 0, header, 16, 4);
      Array.Copy(BitConverter.GetBytes((UInt16)(_wavHeaderArgs.FmtCode)), 0, header, 20, 2);
      Array.Copy(BitConverter.GetBytes((UInt16)(_wavHeaderArgs.Channels)), 0, header, 22, 2);
      Array.Copy(BitConverter.GetBytes((UInt32)(_wavHeaderArgs.SampleRate)), 0, header, 24, 4);
      Array.Copy(BitConverter.GetBytes((UInt32)(_wavHeaderArgs.ByteRate)), 0, header, 28, 4);
      Array.Copy(BitConverter.GetBytes((UInt16)(_wavHeaderArgs.BlockAlign)), 0, header, 32, 2);
      Array.Copy(BitConverter.GetBytes((UInt16)(_wavHeaderArgs.BitDepth)), 0, header, 34, 2);
      Array.Copy(Encoding.ASCII.GetBytes("data"), 0, header, 36, 4);
      Array.Copy(BitConverter.GetBytes((UInt32)(_wavHeaderArgs.DataSize)), 0, header, 40, 4);

      return (header);
    }

    /// <summary>
    /// Generate Sine wave sound data.
    /// </summary>
    /// <param name="frequency">Frequency of sine wave[Hz]</param>
    /// <param name="length">Play time[msec] or The power of sample points number. 2^(length)</param>
    /// <param name="isTime">Is 'length' Time or Power?</param>
    /// <param name="sampleRate">SampleRate[Hz]</param>
    public void GenerateSine(int frequency, int length, bool isTime, int sampleRate = 48000)
    {
      int DataLength;
      if (isTime)
      {
        DataLength = sampleRate * length / 1000;
      }
      else
      {
        DataLength = (int)Math.Pow(2, length);
      }
      _waveData = new double[1, DataLength];
      for (UInt32 datcnt = 0; datcnt < DataLength; datcnt++)
      {
        double Radian = (double)datcnt / sampleRate;
        Radian *= 2 * Math.PI;
        double SineWave = Math.Sin(Radian * frequency);

        _waveData[0, datcnt] = SineWave;
      }
    }

    /// <summary>
    /// Generate Swept-sine wave sound data.
    /// </summary>
    /// <param name="duration">Play time[sec]</param>
    /// <param name="sampleRate">SampleRate[Hz]</param>
    public void GenerateLogSweptSineInTimeDomein(int duration, int sampleRate = 48000)
    {
      double fs = 20.0; // Start value of frequency
      double fe = 200000.0; // End value of frequency
      double amp = 1.0; // Amplitude of Sewpt-sine wave

      int DataLength = sampleRate * duration;
      _waveData = new double[1, DataLength];
      for (int n = 0; n < 2; n++)
      {
        for (int datcnt = 0; datcnt < DataLength / 2.0; datcnt++)
        {
          double R = (Math.Log(fe / fs / Math.Log(2.0))) / (duration / 2.0);
          double sweepfi = ((fs * (-1 + Math.Pow(2.0, (R * datcnt / sampleRate)))) / (R * Math.Log(2.0)));
          _waveData[0, datcnt + (int)(n * duration * sampleRate / 2.0)] = Math.Sin(2.0 * Math.PI * sweepfi) * amp;
        }
      }
    }

    /// <param name="power">The power of sample points number. 2^(power)</param>
    public void GenerateLinearSweptSine(int power)
    {
      int signalLength = (int)Math.Pow(2, power);
      int effectiveLength = signalLength / 2;

      Complex[] SweptSineFreq = new Complex[signalLength];
      for (int k = 0; k < signalLength / 2; k++)
      {
        double phase = 2.0 * Math.PI * effectiveLength * Math.Pow((double)k / signalLength, 2.0);
        SweptSineFreq[k] = new Complex(Math.Cos(phase), -1.0 * Math.Sin(phase));
      }
      for (int k = signalLength / 2; k < signalLength; k++)
      {
        double phase = 2.0 * Math.PI * effectiveLength * Math.Pow(1.0 - ((double)k / signalLength), 2.0);
        SweptSineFreq[k] = new Complex(Math.Cos(phase), Math.Sin(phase));
      }

      Fourier.Radix2Inverse(SweptSineFreq, FourierOptions.NoScaling);
      Complex[] SweptSineTime = new Complex[signalLength];
      Complex Max = new Complex();
      for (int k = 0; k < signalLength; k++)
      {
        SweptSineTime[k] = SweptSineFreq[(k + signalLength - (effectiveLength / 2)) % signalLength];
        if (Complex.Abs(SweptSineTime[k]) > Complex.Abs(Max))
        {
          Max = SweptSineTime[k];
        }
      }

      int DataLength = SweptSineTime.Length * 2;
      _waveData = new double[1, DataLength];
      double Amp = 1.0 / Math.Abs(Max.Real);
      for (int datcnt = 0; datcnt < DataLength; datcnt++)
      {
        _waveData[0, datcnt] = Amp * SweptSineTime[datcnt % (DataLength / 2)].Real;
      }
    }

    /// <param name="power">The power of sample points number. 2^(power)</param>
    public void GenerateLogSweptSine(int power)
    {
      int signalLength = (int)Math.Pow(2, power);
      int effectiveLength = signalLength / 2;

      double Const = effectiveLength * Math.PI / ((signalLength / 2) * Math.Log(signalLength / 2));
      Complex[] SweptSineFreq = new Complex[signalLength];
      SweptSineFreq[0] = 1.0;
      for (int k = 1; k < signalLength / 2; k++)
      {
        double phase = Const * k * Math.Log(k);
        SweptSineFreq[k] = new Complex(Math.Cos(phase) / Math.Sqrt(k), -1.0 * Math.Sin(phase) / Math.Sqrt(k));
      }
      for (int k = signalLength / 2; k < signalLength; k++)
      {
        double phase = Const * (signalLength - k) * Math.Log(signalLength - k);
        SweptSineFreq[k] = new Complex(Math.Cos(phase) / Math.Sqrt(signalLength - k), Math.Sin(phase) / Math.Sqrt(signalLength - k));
      }

      Fourier.Radix2Inverse(SweptSineFreq, FourierOptions.NoScaling);
      Complex[] SweptSineTime = new Complex[signalLength];
      Complex Max = new Complex();
      for (int k = 0; k < signalLength; k++)
      {
        SweptSineTime[k] = SweptSineFreq[(k + signalLength - (effectiveLength / 2)) % signalLength];
        if (Complex.Abs(SweptSineTime[k]) > Complex.Abs(Max))
        {
          Max = SweptSineTime[k];
        }
      }

      int DataLength = SweptSineTime.Length * 2;
      _waveData = new double[1, DataLength];
      double Amp = 1.0 / Math.Abs(Max.Real);
      for (int datcnt = 0; datcnt < DataLength; datcnt++)
      {
        _waveData[0, datcnt] = Amp * SweptSineTime[datcnt % (DataLength / 2)].Real;
      }
    }

    /// <summary>
    /// Get uniform rundom number (-1 ~ +1)
    /// </summary>
    private double GetRandom()
    {
      RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
      byte[] bs = new byte[sizeof(Int64)];
      rng.GetBytes(bs);
      Int64 iR = BitConverter.ToInt64(bs, 0);
      return (double)iR / Int64.MaxValue;
    }

    /// <summary>
    /// Get random number following N(0,1):Standard normal distribution
    /// </summary>
    private double GetNormRandom()
    {
      double dR1 = Math.Abs(GetRandom());
      double dR2 = Math.Abs(GetRandom());
      return (Math.Sqrt(-2 * Math.Log(dR1, Math.E)) * Math.Cos(2 * Math.PI * dR2));
    }

    /// <summary>
    /// Generate white noise sound data.
    /// </summary>
    /// <param name="duration">Play time[sec]</param>
    /// <param name="sampleRate">SampleRate[Hz]</param>
    public void GenerateWhiteNoise(int duration, int sampleRate = 48000)
    {
      int DataLength = sampleRate * duration;
      _waveData = new double[1, DataLength];
      for (UInt32 datcnt = 0; datcnt < DataLength; datcnt++)
      {
        _waveData[0, datcnt] = GetNormRandom();
      }
    }

    /// <summary>
    /// Generate white noise sound data.
    /// </summary>
    /// <param name="power">The power of sample points number. 2^(power)</param>
    public void GenerateWhiteNoise(int power)
    {
      int DataLength = (int)Math.Pow(2, power);
      _waveData = new double[1, DataLength];
      for (UInt32 datcnt = 0; datcnt < DataLength; datcnt++)
      {
        _waveData[0, datcnt] = GetRandom();
      }
    }

    public void WriteToText(string folderPath, string fileName = "Output")
    {
      for (int chcnt = 0; chcnt < Header.Channels; chcnt++)
      {
        string outPath = $"{folderPath}\\{fileName}-Ch{Convert.ToString(chcnt)}.txt";
        using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
        using (StreamWriter sw = new StreamWriter(fs))
        {
          for (int datcnt = 0; datcnt < _waveData.GetLength(1); datcnt++)
          {
            sw.Write(_waveData[chcnt, datcnt]);
            sw.WriteLine();
          }
        }
      }
    }

    public void WriteToCSV(string folderPath, string fileName = "Output")
    {
      string outPath = $"{folderPath}\\{fileName}.csv";
      using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
      using (StreamWriter sw = new StreamWriter(fs))
      {
        for (int datcnt = 0; datcnt < _waveData.GetLength(1); datcnt++)
        {
          for (int chcnt = 0; chcnt < Header.Channels; chcnt++)
          {
            if (chcnt != 0) sw.Write(",");
            sw.Write(_waveData[chcnt, datcnt]);
          }
          sw.WriteLine();
        }
      }
    }

  }
}
