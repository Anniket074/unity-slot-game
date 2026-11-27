using System;
using System.Collections;
using System.IO;
using System.Text;
using SlotGame.Machine;
using UnityEngine;

namespace SlotGame.Diagnostics
{
    /// <summary>
    /// Optional self-check, enabled with <c>SlotMachine.exe -autotest &lt;outputPath&gt;</c>.
    /// Runs a batch of spins through the real scene, verifies that each spin debits the
    /// bet and resolves a payout, writes a short report and quits.
    /// Used to sanity-check a build without a human at the keyboard.
    /// </summary>
    public sealed class SmokeTestRunner : MonoBehaviour
    {
        private const int SpinCount = 30;

        [SerializeField] private SlotMachine machine;

        public void Configure(SlotMachine target)
        {
            machine = target;
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-autotest");
            if (index < 0 || index + 1 >= args.Length || machine == null)
            {
                return;
            }

            StartCoroutine(Run(args[index + 1]));
        }

        private IEnumerator Run(string outputPath)
        {
            yield return new WaitForSeconds(0.5f);

            var report = new StringBuilder();
            int startCredits = machine.Credits;
            int wins = 0;

            for (int i = 0; i < SpinCount; i++)
            {
                int before = machine.Credits;
                int bet = machine.CurrentBet;

                machine.Spin();

                float waited = 0f;
                while (machine.IsBusy && waited < 6f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                int win = machine.PendingWin;
                int afterDebit = machine.Credits;

                if (win > 0)
                {
                    wins++;
                    machine.CollectWin();
                }

                bool debitOk = afterDebit == before - bet;
                report.AppendLine(
                    $"spin {i:D2}: credits {before} -> {afterDebit} (debit {bet}, {(debitOk ? "ok" : "BAD")}), win {win}");

                if (!debitOk)
                {
                    Debug.LogError($"[AutoTest] debit mismatch on spin {i}");
                }

                yield return null;
            }

            report.AppendLine($"RESULT spins={SpinCount} wins={wins} credits={machine.Credits} start={startCredits}");

            // Autoplay: three spins back to back, then the machine must be idle again.
            int beforeAuto = machine.Credits;
            float autoWaited = 0f;
            machine.StartAutoPlay(3);
            while ((machine.IsAutoPlaying || machine.IsBusy) && autoWaited < 30f)
            {
                autoWaited += Time.deltaTime;
                yield return null;
            }

            if (machine.PendingWin > 0)
            {
                machine.CollectWin();
            }

            report.AppendLine(
                $"autoplay: {beforeAuto} -> {machine.Credits} credits, idle={!machine.IsAutoPlaying && !machine.IsBusy} after {autoWaited:F1}s");

            try
            {
                File.WriteAllText(outputPath, report.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AutoTest] could not write report: {exception.Message}");
            }

            Debug.Log($"[AutoTest] finished: {wins}/{SpinCount} wins, credits {startCredits} -> {machine.Credits}");
            yield return new WaitForSeconds(0.2f);
            Application.Quit();
        }
    }
}
