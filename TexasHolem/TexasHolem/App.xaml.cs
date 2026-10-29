using System.Configuration;
using System.Data;
using System.Windows;

namespace TexasHoldemClient
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            GameSettings.Load();
            AudioManager.PlayBGM("BGM.mp3");
        }
    }
}