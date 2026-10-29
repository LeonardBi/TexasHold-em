using System.Windows;
using System.Windows.Media;

namespace TexasHoldemClient
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            BgmToggle.IsChecked = GameSettings.IsBgmEnabled;
            BgmSlider.Value = GameSettings.BgmVolume;

            SfxToggle.IsChecked = GameSettings.IsSfxEnabled;
            SfxSlider.Value = GameSettings.SfxVolume;

            if (GameSettings.IsEnglish) LangEN.IsChecked = true;
            else LangZH.IsChecked = true;
        }

        private void ColorBtn_Click(object sender, RoutedEventArgs e)
        {
            string hexColor = (sender as System.Windows.Controls.Button).Tag.ToString();
            GameSettings.TableColorHex = hexColor;
            GameSettings.TableBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
            MessageBox.Show(GameSettings.Translate("牌桌颜色已切换！", "Table color changed!"));
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            // Show game rules in a message box
            string rules = GameSettings.Translate(
                "【德州扑克 (含独家换牌机制) 完整规则】\n\n" +
                "1. 基础发牌：每位玩家开局会获得 2 张底牌（仅自己可见）。桌面中央会依次发出 5 张公共牌（翻牌 3 张、转牌 1 张、河牌 1 张）。\n\n" +
                "2. 玩家行动：在每次发牌间隙，玩家可以根据牌力选择：\n" +
                "   • 看牌/跟注 (Check/Call)：匹配当前最高下注额。\n" +
                "   • 加注 (Raise) 或 梭哈 (All-In)：增加下注额向对手施压。\n" +
                "   • 弃牌 (Fold)：放弃本局，退出竞争。\n\n" +
                "3. 🌟 创新机制 (River Swap)：在河牌阶段的下注结束后，将进入独家的“终极换牌”阶段！你可以选择弃掉 1 张底牌并从牌堆重新抽取 1 张，或者选择保留原牌。这极大增加了绝杀的策略性！\n\n" +
                "4. 摊牌结算 (Showdown)：从最终的 2 张底牌和 5 张公共牌中，系统会自动选出最大的 5 张牌组合。牌型最大者赢得整个底池！\n\n" +
                "🏆 牌型大小排序：\n" +
                "皇家同花顺 > 同花顺 > 四条 > 葫芦 > 同花 > 顺子 > 三条 > 两对 > 一对 > 高牌",

                "【Texas Hold'em (with River Swap) Complete Rules】\n\n" +
                "1. Basic Flow: Each player is dealt 2 hole cards. 5 community cards are dealt to the board in stages (Flop: 3, Turn: 1, River: 1).\n\n" +
                "2. Actions: Between dealing phases, players can choose to:\n" +
                "   • Check/Call: Match the current highest bet.\n" +
                "   • Raise / All-In: Increase the bet to pressure opponents.\n" +
                "   • Fold: Surrender the hand and exit the current round.\n\n" +
                "3. 🌟 INNOVATION (River Swap): After the final betting round on the River, the exclusive 'Swap Phase' begins! You can strategically discard 1 hole card to draw a new one from the deck, or keep your hand.\n\n" +
                "4. Showdown: The system automatically evaluates the best 5-card hand using any combination of your 2 hole cards and the 5 community cards. The best hand wins the pot!\n\n" +
                "🏆 Hand Rankings (Highest to Lowest):\n" +
                "Royal Flush > Straight Flush > 4 of a Kind > Full House > Flush > Straight > 3 of a Kind > 2 Pair > 1 Pair > High Card"
            );
            MessageBox.Show(rules, GameSettings.Translate("游戏规则", "How to Play"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            // Save settings to GameSettings and persist
            GameSettings.IsBgmEnabled = BgmToggle.IsChecked ?? true;
            GameSettings.BgmVolume = BgmSlider.Value;

            GameSettings.IsSfxEnabled = SfxToggle.IsChecked ?? true;
            GameSettings.SfxVolume = SfxSlider.Value;

            GameSettings.IsEnglish = LangEN.IsChecked ?? false;

            GameSettings.Save();

            // Change BGM state immediately based on new settings
            if (GameSettings.IsBgmEnabled)
            {
                AudioManager.UpdateBgmVolume();
                AudioManager.PlayBGM("BGM.mp3");
            }
            else
            {
                AudioManager.StopBGM();
            }

            this.DialogResult = true;
            this.Close();
        }
    }
}