using System;
using System.Collections;
using System.IO;
using SlotGame.Machine;
using UnityEngine;

namespace SlotGame.Diagnostics
{
    /// <summary>
    /// Scripted screenshot runner, enabled with
    /// <c>SlotMachine.exe -capture &lt;outputFolder&gt;</c>.
    ///
    /// Captures through <see cref="ScreenCapture"/> so it works even when the player
    /// window is behind another window - useful for generating the README images from
    /// a build without a human driving the game.
    /// </summary>
    public sealed class CaptureRunner : MonoBehaviour
    {
        [SerializeField] private SlotMachine machine;

        public void Configure(SlotMachine target)
        {
            machine = target;
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-capture");
            if (index < 0 || index + 1 >= args.Length || machine == null)
            {
                return;
            }

            StartCoroutine(Run(args[index + 1]));
        }

        private IEnumerator Run(string folder)
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(1.2f);

            yield return Shot(folder, "01-idle");

            machine.Spin();
            yield return new WaitForSeconds(0.35f);
            yield return Shot(folder, "02-spinning");

            while (machine.IsBusy)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.35f);
            yield return Shot(folder, "03-result");

            // Spin until something actually pays, so the win popup can be documented.
            int attempts = 0;
            while (machine.PendingWin <= 0 && attempts < 80)
            {
                attempts++;
                machine.Spin();
                while (machine.IsBusy)
                {
                    yield return null;
                }
            }

            yield return new WaitForSeconds(0.4f);
            yield return Shot(folder, "04-win-line");

            // The gamble popup is deliberately delayed by the view so the winning line
            // is visible first; wait past that delay before grabbing it.
            yield return new WaitForSeconds(1.3f);
            yield return Shot(folder, "05-win-popup");

            // Take the win, then force a full-board match so the mega payout can be
            // documented without waiting for a one-in-several-thousand spin.
            machine.CollectWin();
            yield return new WaitForSeconds(0.6f);
            machine.ForceMegaRound();
            machine.Spin();

            while (machine.IsBusy)
            {
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            yield return Shot(folder, "06-mega-line");
            yield return new WaitForSeconds(1.5f);
            yield return Shot(folder, "07-mega-popup");

            Debug.Log($"[Capture] done; win found after {attempts} extra spins, pending={machine.PendingWin}");
            yield return new WaitForSeconds(0.2f);
            Application.Quit();
        }

        private static IEnumerator Shot(string folder, string name)
        {
            string path = Path.Combine(folder, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
        }
    }
}
