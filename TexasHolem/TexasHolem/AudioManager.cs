using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace TexasHoldemClient
{
    public static class AudioManager
    {
        private static MediaPlayer _bgmPlayer = new MediaPlayer();
        private static string _audioPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio");

        // Keep SFX players resident in memory to prevent them from being garbage-collected
        private static List<MediaPlayer> _sfxPool = new List<MediaPlayer>();
        private static int _poolIndex = 0;
        private const int POOL_SIZE = 10;

        private static bool _hasShownError = false;

        static AudioManager()
        {
            // Initialize the sound effect pool
            for (int i = 0; i < POOL_SIZE; i++)
            {
                _sfxPool.Add(new MediaPlayer());
            }
        }

        // Play background music
        public static void PlayBGM(string fileName)
        {
            if (!GameSettings.IsBgmEnabled) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    string path = Path.Combine(_audioPath, fileName);
                    if (File.Exists(path))
                    {
                        _bgmPlayer.Open(new Uri(path, UriKind.Absolute));
                        _bgmPlayer.Volume = GameSettings.BgmVolume <= 0.05 ? 0.4 : GameSettings.BgmVolume;
                        _bgmPlayer.MediaEnded += (s, e) => { _bgmPlayer.Position = TimeSpan.Zero; _bgmPlayer.Play(); };
                        _bgmPlayer.Play();
                    }
                }
                catch { }
            });
        }

        public static void StopBGM() { Application.Current.Dispatcher.Invoke(() => _bgmPlayer.Stop()); }
        public static void UpdateBgmVolume() { Application.Current.Dispatcher.Invoke(() => { _bgmPlayer.Volume = GameSettings.BgmVolume; }); }

        // Play a one-shot sound effect
        public static void PlaySound(string fileName)
        {
            if (!GameSettings.IsSfxEnabled) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    string path = Path.Combine(_audioPath, fileName);

                    // Check whether the file exists
                    if (!File.Exists(path))
                    {
                        if (!_hasShownError)
                        {
                            MessageBox.Show($"Can't find file: {fileName}\nPlease set the file properties in Visual Studio to Copy if newer'", "File lost", MessageBoxButton.OK, MessageBoxImage.Error);
                            _hasShownError = true;
                        }
                        return;
                    }

                    // Rotate through the pool to obtain a player
                    var player = _sfxPool[_poolIndex];

                    // Force-reset the player
                    player.Stop();
                    player.Open(new Uri(path, UriKind.Absolute));
                    player.Volume = GameSettings.SfxVolume <= 0.05 ? 1.0 : GameSettings.SfxVolume;
                    player.Play();

                    _poolIndex = (_poolIndex + 1) % POOL_SIZE;
                }
                catch { }
            });
        }
    }
}