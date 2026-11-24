using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Qqtang.UnityClient
{
    /// <summary>
    /// Runtime PCM WAV loader supporting looping BGM (music-lobby / music-battle) and SFX from StreamingAssets/ArtCache/audio/.
    /// Safely silent when audio files are not downloaded locally (zero audio binaries committed to Git).
    /// </summary>
    public sealed class AudioPlayer : MonoBehaviour
    {
        private AudioSource _sfxSource;
        private AudioSource _bgmSource;
        private string _audioRoot;
        private string _currentBgmKey;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> _missing = new HashSet<string>();

        public float MasterVolume { get; set; } = 0.85f;
        public float BgmVolume { get; set; } = 0.38f;
        public float SfxVolume { get; set; } = 0.72f;
        public bool Muted { get; set; }

        private void Awake()
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;

            _audioRoot = Path.Combine(Application.streamingAssetsPath, "ArtCache", "audio");
        }

        public void ApplyVolumeSettings()
        {
            if (_bgmSource != null)
                _bgmSource.volume = Muted ? 0f : Mathf.Clamp01(MasterVolume * BgmVolume);
        }

        public void PlayBgm(string clipKey)
        {
            if (string.IsNullOrEmpty(clipKey) || clipKey == _currentBgmKey) return;
            _currentBgmKey = clipKey;
            var clip = GetOrLoadClip(clipKey);
            if (clip == null)
            {
                _bgmSource.Stop();
                return;
            }
            _bgmSource.clip = clip;
            ApplyVolumeSettings();
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            _currentBgmKey = null;
            if (_bgmSource != null) _bgmSource.Stop();
        }

        public void PlaySfx(string clipKey, float scale = 1f)
        {
            if (Muted || string.IsNullOrEmpty(clipKey)) return;
            var clip = GetOrLoadClip(clipKey);
            if (clip == null) return;
            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(MasterVolume * SfxVolume * scale));
        }

        private AudioClip GetOrLoadClip(string clipKey)
        {
            if (_missing.Contains(clipKey)) return null;
            if (_clips.TryGetValue(clipKey, out var cached)) return cached;
            string path = Path.Combine(_audioRoot, clipKey + ".wav");
            var clip = TryLoadPcmWav(path, clipKey);
            if (clip == null)
            {
                _missing.Add(clipKey);
                return null;
            }
            _clips[clipKey] = clip;
            return clip;
        }

        private static AudioClip TryLoadPcmWav(string path, string name)
        {
            if (!File.Exists(path)) return null;
            try
            {
                byte[] wav = File.ReadAllBytes(path);
                if (wav.Length < 44 || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F')
                    return null;
                int channels = BitConverter.ToInt16(wav, 22);
                int sampleRate = BitConverter.ToInt32(wav, 24);
                int bitsPerSample = BitConverter.ToInt16(wav, 34);
                if (channels <= 0 || sampleRate <= 0 || bitsPerSample != 16) return null;

                int pos = 12;
                while (pos + 8 <= wav.Length)
                {
                    int chunkSize = BitConverter.ToInt32(wav, pos + 4);
                    if (wav[pos] == 'd' && wav[pos + 1] == 'a' && wav[pos + 2] == 't' && wav[pos + 3] == 'a')
                    {
                        int dataStart = pos + 8;
                        int bytesAvail = Math.Min(chunkSize, wav.Length - dataStart);
                        int sampleCount = bytesAvail / 2;
                        float[] samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                            samples[i] = BitConverter.ToInt16(wav, dataStart + i * 2) / 32768f;
                        var clip = AudioClip.Create(name, sampleCount / channels, channels, sampleRate, false);
                        clip.SetData(samples, 0);
                        return clip;
                    }
                    pos += 8 + chunkSize + (chunkSize & 1);
                }
            }
            catch { }
            return null;
        }
    }
}
