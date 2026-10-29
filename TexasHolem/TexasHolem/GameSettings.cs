using System;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace TexasHoldemClient
{
    // store settings in a serializable class for easy JSON handling
    public class SettingsData
    {
        public bool IsBgmEnabled { get; set; } = true;
        public double BgmVolume { get; set; } = 0.4;

        public bool IsSfxEnabled { get; set; } = true;
        public double SfxVolume { get; set; } = 0.8;

        public string TableColorHex { get; set; } = "#1B5E20";
        public bool IsEnglish { get; set; } = true;
    }

    public static class GameSettings
    {
        // music and sound effect settings
        public static bool IsBgmEnabled = true;
        public static double BgmVolume = 0.4;

        public static bool IsSfxEnabled = true;
        public static double SfxVolume = 0.8;

        public static string TableColorHex = "#1B5E20";
        public static Brush TableBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1B5E20"));
        public static bool IsEnglish = true;

        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static void Save()
        {
            try
            {
                var data = new SettingsData
                {
                    IsBgmEnabled = IsBgmEnabled,
                    BgmVolume = BgmVolume,
                    IsSfxEnabled = IsSfxEnabled,
                    SfxVolume = SfxVolume,
                    TableColorHex = TableColorHex,
                    IsEnglish = IsEnglish
                };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch { }
        }

        public static void Load()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);

                    if (data != null)
                    {
                        IsBgmEnabled = data.IsBgmEnabled;
                        BgmVolume = data.BgmVolume;
                        IsSfxEnabled = data.IsSfxEnabled;
                        SfxVolume = data.SfxVolume;

                        TableColorHex = data.TableColorHex;
                        TableBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(data.TableColorHex));
                        IsEnglish = data.IsEnglish;
                    }
                }
                catch { }
            }
        }

        public static string Translate(string zh, string en)
        {
            return IsEnglish ? en : zh;
        }
    }
}