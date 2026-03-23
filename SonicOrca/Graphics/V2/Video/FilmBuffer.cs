// Decompiled with JetBrains decompiler
// Type: SonicOrca.Graphics.V2.Video.FilmBuffer
// Assembly: SonicOrca, Version=2.0.1012.10518, Culture=neutral, PublicKeyToken=null
// MVID: 2E579C53-B7D9-4C24-9AF5-48E9526A12E7
// Assembly location: C:\Games\S2HD_2.0.1012-rc2\SonicOrca.dll

using SonicOrca.Resources;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace SonicOrca.Graphics.V2.Video
{

    public class FilmBuffer : IFilmBuffer, IDisposable, ILoadedResource
    {
      private readonly string _path;
      private Process _ffmpegProcess;
      private Stream _ffmpegStdout;
      private byte[] _bytes = Array.Empty<byte>();
      private int _width;
      private int _height;
      private double _currentFrame;
      private double _numFrames;
      private double _frameRate = 30.0;

      public int Width => this._width;

      public int Height => this._height;

      public double CurrentTime => this._currentFrame / this._frameRate;

      public double Duration => this._numFrames > 0.0 ? this._numFrames / this._frameRate : 0.0;

      public Resource Resource { get; set; }

      public void Dispose()
      {
        this._ffmpegStdout?.Dispose();
        this._ffmpegStdout = null;
        if (this._ffmpegProcess != null)
        {
          try
          {
            if (!this._ffmpegProcess.HasExited)
              this._ffmpegProcess.Kill(true);
          }
          catch
          {
          }
          this._ffmpegProcess.Dispose();
          this._ffmpegProcess = null;
        }
      }

      public void OnLoaded()
      {
        this.TryProbeVideoInfo();
        int num = this._width * this._height * 4;
        this._bytes = num > 0 ? new byte[num] : Array.Empty<byte>();
        this._currentFrame = 0.0;
      }

      public FilmBuffer(string path) => this._path = path;

      public void Decode()
      {
        if (this._width <= 0 || this._height <= 0 || this._bytes.Length == 0)
          return;
        if (!this.EnsureFfmpegStarted())
          return;
        if (!this.ReadExact(this._bytes, this._bytes.Length))
        {
          this._numFrames = Math.Max(this._numFrames, this._currentFrame);
          return;
        }
        ++this._currentFrame;
      }

      public byte[] GetArgbData()
      {
        return this._bytes;
      }

      private bool EnsureFfmpegStarted()
      {
        if (this._ffmpegProcess != null && !this._ffmpegProcess.HasExited && this._ffmpegStdout != null)
          return true;
        this.Dispose();
        ProcessStartInfo processStartInfo = new ProcessStartInfo("ffmpeg", "-loglevel error -i \"" + this._path + "\" -f rawvideo -pix_fmt bgra -vsync 0 -")
        {
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          CreateNoWindow = true
        };
        try
        {
          this._ffmpegProcess = Process.Start(processStartInfo);
          if (this._ffmpegProcess == null)
            return false;
          this._ffmpegStdout = this._ffmpegProcess.StandardOutput.BaseStream;
          return true;
        }
        catch (Exception ex)
        {
          Console.WriteLine("Failed to start ffmpeg for video playback: " + ex.Message);
          return false;
        }
      }

      private bool ReadExact(byte[] buffer, int count)
      {
        if (this._ffmpegStdout == null)
          return false;
        int num1 = 0;
        while (num1 < count)
        {
          int num2 = this._ffmpegStdout.Read(buffer, num1, count - num1);
          if (num2 <= 0)
            return false;
          num1 += num2;
        }
        return true;
      }

      private void TryProbeVideoInfo()
      {
        ProcessStartInfo processStartInfo = new ProcessStartInfo("ffprobe", "-v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate,nb_frames,duration -of default=nokey=1:noprint_wrappers=1 \"" + this._path + "\"")
        {
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          UseShellExecute = false,
          CreateNoWindow = true
        };
        try
        {
          using Process process = Process.Start(processStartInfo);
          if (process == null)
            return;
          string[] strArray = process.StandardOutput.ReadToEnd().Split(new char[2]
          {
            '\r',
            '\n'
          }, StringSplitOptions.RemoveEmptyEntries);
          process.WaitForExit();
          if (strArray.Length >= 5)
          {
            int.TryParse(strArray[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out this._width);
            int.TryParse(strArray[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out this._height);
            this._frameRate = this.ParseFrameRate(strArray[2]);
            if (!double.TryParse(strArray[3], NumberStyles.Float, CultureInfo.InvariantCulture, out this._numFrames))
            {
              if (double.TryParse(strArray[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                this._numFrames = result * this._frameRate;
            }
          }
        }
        catch (Exception ex)
        {
          Console.WriteLine("Failed to probe video metadata: " + ex.Message);
        }
      }

      private double ParseFrameRate(string value)
      {
        if (string.IsNullOrWhiteSpace(value))
          return 30.0;
        if (value.Contains("/"))
        {
          string[] strArray = value.Split('/');
          if (strArray.Length == 2 && double.TryParse(strArray[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double result1) && double.TryParse(strArray[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double result2) && result2 != 0.0)
            return result1 / result2;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 30.0;
      }
    }
}
