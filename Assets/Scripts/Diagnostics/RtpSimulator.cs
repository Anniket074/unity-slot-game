using System;
using System.Globalization;
using System.IO;
using System.Text;
using SlotGame.Core;
using SlotGame.Data;
using SlotGame.Machine;
using UnityEngine;

namespace SlotGame.Diagnostics
{
    /// <summary>
    /// Monte Carlo check of the paytable, enabled with
    /// <c>SlotMachine.exe -rtp [spins] [outputPath]</c>.
    ///
    /// It drives the same <see cref="SpinRules"/> the machine uses, so the RTP it reports
    /// is the RTP the player actually gets. No animation, so hundreds of thousands of
    /// spins finish in seconds.
    /// </summary>
    public sealed class RtpSimulator : MonoBehaviour
    {
        private const int DefaultSpins = 500_000;
        private const int SimulationBet = 10;

        [SerializeField] private GameConfig config;

        public void Configure(GameConfig gameConfig)
        {
            config = gameConfig;
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-rtp");
            if (index < 0 || config == null)
            {
                return;
            }

            int spins = DefaultSpins;
            if (index + 1 < args.Length && int.TryParse(args[index + 1], out int requested) && requested > 0)
            {
                spins = requested;
            }

            string path = index + 2 < args.Length
                ? args[index + 2]
                : Path.Combine(Application.persistentDataPath, "rtp.txt");

            Run(spins, path);
            Application.Quit();
        }

        private void Run(int spins, string outputPath)
        {
            var random = new SystemRandomSource(20240501);
            var generator = new SpinOutcomeGenerator(random, config.Symbols);

            long staked = 0;
            long returned = 0;
            int wins = 0;
            int megas = 0;
            int best = 0;

            for (int i = 0; i < spins; i++)
            {
                SymbolDefinition[,] grid = generator.Generate(config.Reels, config.Rows);
                SpinOutcome outcome = SpinRules.Evaluate(
                    grid,
                    config.Reels,
                    config.Rows,
                    SimulationBet,
                    config.MegaWinMultiplier);

                staked += SimulationBet;
                returned += outcome.TotalWin;

                if (outcome.TotalWin > 0)
                {
                    wins++;
                }

                if (outcome.IsMegaWin)
                {
                    megas++;
                }

                if (outcome.TotalWin > best)
                {
                    best = outcome.TotalWin;
                }
            }

            double rtp = staked > 0 ? returned * 100.0 / staked : 0;
            double hitRate = spins > 0 ? wins * 100.0 / spins : 0;

            var report = new StringBuilder();
            report.AppendLine("Slot Machine - paytable simulation");
            report.AppendLine("=================================");
            report.AppendLine($"spins            {spins:N0}");
            report.AppendLine($"bet per spin     {SimulationBet}");
            report.AppendLine($"total staked     {staked:N0}");
            report.AppendLine($"total returned   {returned:N0}");
            report.AppendLine($"RTP              {rtp.ToString("F2", CultureInfo.InvariantCulture)} %");
            report.AppendLine($"hit rate         {hitRate.ToString("F2", CultureInfo.InvariantCulture)} % ({wins:N0} paid spins)");
            report.AppendLine($"mega wins        {megas:N0} (1 in {(megas > 0 ? spins / (double)megas : 0):N0})");
            report.AppendLine($"best win         {best:N0} G ({(best / (double)SimulationBet).ToString("F0", CultureInfo.InvariantCulture)}x bet)");

            string text = report.ToString();
            Debug.Log("[RtpSimulator]\n" + text);

            try
            {
                File.WriteAllText(outputPath, text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RtpSimulator] could not write report: {exception.Message}");
            }
        }
    }
}
