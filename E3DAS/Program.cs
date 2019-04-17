using System;
using System.IO;
using System.Collections.Generic;
using WaveTools;

namespace E3DAS
{
  class Program
  {
    const int TIMELEN = 4000; // msec
    static double AmpRate = Math.Pow(10, -1.0 - ((5.0 - 1.0) / 15.0) * 5);
    static string Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    static void Main(string[] args)
    {
      var source = new Wave();
      //source.WavRead(Desktop + @"\sawai\soundfiles", "Marimba");
      //source.WaveLengthChanger();
      //source.GenerateSine(1000, 18, false);
      source.GenerateWhiteNoise(18);
      CreateSpatialized6chFiles(source, Desktop + @"\sawai\hrir", Desktop, 2);
      
      /*
      for (int i = 0; i < 15; i++)
      {
        double amp = Math.Pow(10, -1.0 - ((5.0 - 1.0) / 15.0) * i);
        string name = "amp" + amp.ToString("F5");
        CreateDiffAmpExp(source, Desktop + @"\sawai\hrir", Desktop, name, amp);
      }
      */
    }

    static void CreateSpatialized6chFiles(Wave source, string irFolderPath, string outPath, int channelNum, int aziStep = 15)
    {
      if (channelNum != 2 && channelNum != 4 && channelNum != 6)
      {
        throw new ArgumentException();
      }
      
      Console.WriteLine($"Create {channelNum}ch spatialized sound data in 6ch wave file");
      string[] position = { "left", "right", "front", "back" , "center", "woofer" };
      int PosiNum = channelNum;
      Wave[] wave = new Wave[PosiNum];
      List<double> amp = new List<double>();
      for (int posi = 0; posi < PosiNum; posi++)
      {
        wave[posi] = new Wave();
        wave[posi].CopyWavData(source);
        amp.Add(wave[posi].ConvolveIR(irFolderPath, posi, aziStep));
        wave[posi].WaveLengthChanger(TIMELEN);
        Console.WriteLine($"Position: {position[posi]} finish.");
      }

      for (int ch = 0; ch < 360 / aziStep; ch++)
      {
        wave[0].WaveGainChanger(ch, AmpRate);
        wave[1].WaveGainChanger(ch, AmpRate);
      }
      
      Console.Write("Writing wav and csv files.");
      Directory.CreateDirectory(outPath + @"\data");
      Directory.CreateDirectory(outPath + @"\data" + @"\csv");
      for (int azi = 0; azi < 360 / aziStep; azi++)
      {
        var output = new Wave();
        output.ConvineToMultiCh(wave, azi, amp.ToArray());
        output.WavWrite(outPath + @"\data", $"{azi}");
        output.WriteToCSV(outPath + @"\data" + @"\csv", $"{azi}");
        Console.Write(".");
      }
      Console.WriteLine();
      Console.Write("Finish!");
    }

    static void CreateSpatialized2chFiles(Wave source, string irFolderPath, string outPath, int aziStep = 15)
    {
      Console.WriteLine("Create 2ch spatialized sound data in 2ch wav file");
      string[] position = { "left", "right" };
      int PosiNum = position.Length;
      Wave[] wave = new Wave[PosiNum];
      List<double> amp = new List<double>();
      for (int posi = 0; posi < PosiNum; posi++)
      {
        wave[posi] = new Wave();
        wave[posi].CopyWavData(source);
        amp.Add(wave[posi].ConvolveIR(irFolderPath, posi, aziStep));
        wave[posi].WaveLengthChanger(TIMELEN);
        Console.WriteLine($"Position: {position[posi]} finish.");
      }

      Console.Write("Writing wav and csv files.");
      Directory.CreateDirectory(outPath + @"\data");
      Directory.CreateDirectory(outPath + @"\data" + @"\csv");
      for (int azi = 0; azi < 360 / aziStep; azi++)
      {
        var output = new Wave();
        output.ConvineToMultiCh(wave, azi, amp.ToArray(), false);
        output.WavWrite(outPath + @"\data", $"{azi}");
        output.WriteToCSV(outPath + @"\data" + @"\csv", $"{azi}");
        Console.Write(".");
      }
      Console.WriteLine();
      Console.Write("Finish!");
    }


    static void CreateDiffAmpExp(Wave source, string irFolderPath, string outPath, string outName, double ampRate)
    {
      int channelNum = 4;
      int aziStep = 90;
      int timeLen = 2000;

      Console.WriteLine($"Create {channelNum}ch sound data for amp experiment");
      string[] position = { "left", "right", "front", "back", "center", "woofer" };
      int PosiNum = channelNum;
      Wave[] wave = new Wave[PosiNum];
      List<double> amp = new List<double>();
      for (int posi = 0; posi < PosiNum; posi++)
      {
        wave[posi] = new Wave();
        wave[posi].CopyWavData(source);
        amp.Add(wave[posi].ConvolveIR(irFolderPath, posi, aziStep));
        wave[posi].WaveLengthChanger(timeLen);
        Console.WriteLine($"Position: {position[posi]} finish.");
      }

      for (int ch = 0; ch < 360 / aziStep; ch++)
      {
        wave[0].WaveGainChanger(ch, ampRate);
        wave[1].WaveGainChanger(ch, ampRate);
      }

      Console.Write("Writing wav files.");
      Directory.CreateDirectory(outPath + @"\data");
      var output = new Wave();
      output.ConvineToMultiChAmpExp(wave, amp.ToArray());
      output.WavWrite(outPath + @"\data", outName);
      Console.Write(".");
      Console.WriteLine();
    }
  }
}
