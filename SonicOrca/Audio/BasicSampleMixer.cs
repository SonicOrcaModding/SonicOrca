// Decompiled with JetBrains decompiler
// Type: SonicOrca.Audio.BasicSampleMixer
// Assembly: SonicOrca, Version=2.0.1012.10518, Culture=neutral, PublicKeyToken=null
// MVID: 2E579C53-B7D9-4C24-9AF5-48E9526A12E7
// Assembly location: C:\Games\S2HD_2.0.1012-rc2\SonicOrca.dll

using System;
using System.Collections.Generic;

namespace SonicOrca.Audio
{

    public class BasicSampleMixer : ISampleMixer
    {
      private byte[] _scratchBuffer;
      private readonly List<ISampleProvider> _nonMusic = new List<ISampleProvider>();
      private readonly List<ISampleProvider> _music = new List<ISampleProvider>();

      public void Mix(byte[] buffer, int offset, int length, IEnumerable<ISampleProvider> channels)
      {
        if (_scratchBuffer == null || _scratchBuffer.Length < length)
          _scratchBuffer = new byte[length];

        _nonMusic.Clear();
        _music.Clear();

        foreach (var provider in channels)
        {
          if (!provider.Playing)
            continue;
          if (provider is SampleInstance si && si.Classification == SampleInstanceClassification.Music)
            _music.Add(provider);
          else
            _nonMusic.Add(provider);
        }

        MixList(_nonMusic, buffer, offset, length);
        MixList(_music, buffer, offset, length);
      }

      private void MixList(List<ISampleProvider> providers, byte[] buffer, int offset, int length)
      {
        for (int i = 0; i < providers.Count; i++)
        {
          var sampleProvider = providers[i];
          double calculatedVolume = sampleProvider.CalculatedVolume;
          if (calculatedVolume > 0.0)
          {
            int num1 = sampleProvider.Read(_scratchBuffer, offset, length);
            for (int startIndex = 0; startIndex < num1; startIndex += 2)
            {
              short num2 = this.MixSample(BitConverter.ToInt16(buffer, startIndex), (short) ((double) BitConverter.ToInt16(_scratchBuffer, startIndex) * calculatedVolume));
              buffer[startIndex] = (byte) ((uint) num2 & (uint) byte.MaxValue);
              buffer[startIndex + 1] = (byte) ((int) num2 >> 8 & (int) byte.MaxValue);
            }
          }
        }
      }

      private short MixSample(short a, short b)
      {
        int num1 = (int) a;
        int num2 = (int) b;
        return num1 >= 0 || num2 >= 0 ? (num1 <= 0 || num2 <= 0 ? (short) (num1 + num2) : (short) (num1 + num2 - num1 * num2 / (int) short.MaxValue)) : (short) (num1 + num2 - num1 * num2 / (int) short.MinValue);
      }
    }
}
