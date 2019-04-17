using System;
using System.IO;

namespace IRTools
{
  public class IR
  {
    /// <summary>
    /// ir[position, azimuth, sample] pos0 = left, pos1 = right, pos2 = front, pos3 = back
    /// </summary>
    private double[,,] _ir;
    
    /// <summary>
    /// Data[position, azimuth, sample] position -> 0: left, 1: right, 2: front, 3: back
    /// </summary>
    public double[,,] Data => _ir;

    public int POSINUM => 4;
    public int AZISTEP => 5;
    public int AZIMUTH => 360 / AZISTEP;
    public int SAMPLES => 512;

    public void ReadfromCSV(string folderPath)
    {
      if (!Directory.Exists(folderPath))
      {
        throw new DirectoryNotFoundException();
      }

      _ir = new double[POSINUM, AZIMUTH, SAMPLES];

      string[] filename = { "left", "right", "front", "back" };
      for (int i = 0; i < POSINUM; i++)
      {
        using (FileStream fs = new FileStream(folderPath + @"\" + filename[i] + ".csv", FileMode.Open, FileAccess.Read, FileShare.Read))
        using (StreamReader sr = new StreamReader(fs))
        {
          for (int azi = 0; azi < AZIMUTH; azi++)
          {
            string line = sr.ReadLine();
            string[] values = line.Split(',');
            for (int n = 0; n < SAMPLES; n++)
            {
              _ir[i, azi, n] = Convert.ToDouble(values[n]);
            }
          }
        }
      }

    }
  }
}
