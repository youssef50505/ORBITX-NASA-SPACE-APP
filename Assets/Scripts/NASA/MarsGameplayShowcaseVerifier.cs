using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace MarsSentinel.NASA
{
    /// <summary>
    /// Automated diagnostic capture harness invoked ONLY when -captureGameplay=<dir> is passed.
    /// Verifies Astronaut spawn/movement, Rover boarding/driving, Agriculture Dome, Medical Bay,
    /// EDL Aircraft/Rocket Pad, and Scientific Outcrop Inspection + NASA raw JPEG loading.
    /// Zero overhead during normal user play.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class MarsGameplayShowcaseVerifier : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachIfCliFlag()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                foreach (string arg in args)
                {
                    if (arg.StartsWith("-captureGameplay=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (FindFirstObjectByType<MarsGameplayShowcaseVerifier>() == null)
                        {
                            var go = new GameObject("MS_GameplayShowcaseVerifier");
                            go.AddComponent<MarsGameplayShowcaseVerifier>().outDir =
                                arg.Substring("-captureGameplay=".Length).Trim().Trim((char)34);
                        }
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore CLI sandbox errors
            }
        }

        private string outDir;

        private IEnumerator Start()
        {
            if (string.IsNullOrEmpty(outDir)) yield break;
            Directory.CreateDirectory(outDir);

            // Wait for datasets + outcrops + facilities to finish loading
            float waitSec = 0f;
            while (waitSec < 8f && (MarsDatasetLoader.Instance == null || !MarsDatasetLoader.Instance.IsLoaded))
            {
                waitSec += Time.deltaTime;
                yield return null;
            }
            yield return WaitFrames(15);

            MarsVisualRealismPolish.Instance?.EnterExploreMode();
            yield return WaitFrames(8);

            var diag = new System.Collections.Generic.List<string>();

            // Shot 1: Astronaut Spawn looking toward Fleet + Base Campus
            yield return CaptureShot(diag, "gp_01_astronaut_eva.png", "astronaut_spawn");

            // Shot 2: Pressurized Rover Driving Mode
            if (MarsRoverVehicleController.Instance != null)
            {
                MarsRoverVehicleController.Instance.BoardDriver();
                yield return WaitFrames(10);
                yield return CaptureShot(diag, "gp_02_rover_driving.png", "rover_driving");
                MarsRoverVehicleController.Instance.DismountDriver();
                yield return WaitFrames(6);
            }

            // Shot 3: Agriculture & Hydroponics Dome
            if (MarsAstronautController.Instance != null)
            {
                MarsAstronautController.Instance.TeleportToFacility("Agri");
                yield return WaitFrames(10);
                yield return CaptureShot(diag, "gp_03_agriculture_dome.png", "agriculture_dome");

                // Shot 4: First Aid / Medical & Radiation Triage Bay
                MarsAstronautController.Instance.TeleportToFacility("Medical");
                yield return WaitFrames(10);
                yield return CaptureShot(diag, "gp_04_medical_bay.png", "medical_bay");

                // Shot 4b: NASA Habitat Demonstration Unit (HDU) + Mark III Spacesuit
                MarsAstronautController.Instance.TeleportToFacility("Habitat");
                yield return WaitFrames(10);
                yield return CaptureShot(diag, "gp_08_nasa_hdu_mark3.png", "nasa_hdu_mark3");

                // Shot 5: EDL Lifting-Body Aircraft & Ascent Rocket Pad
                MarsAstronautController.Instance.TeleportToFacility("EDL");
                yield return WaitFrames(10);
                yield return CaptureShot(diag, "gp_05_edl_aircraft.png", "edl_aircraft");

                MarsAstronautController.Instance.TeleportToFacility("Rocket");
                yield return WaitFrames(10);
                yield return CaptureShot(diag, "gp_06_rocket_pad.png", "rocket_pad");

                // Shot 6: Inspect Cheyava Falls Outcrop with Science Drawer & NASA Photo Open
                MarsAstronautController.Instance.SnapToSurfaceAt(-10.0f, -20.5f);
                yield return WaitFrames(6);
                foreach (var t in MarsInspectableTarget.AllTargets)
                {
                    if (t != null && t.targetId.Contains("cheyava"))
                    {
                        MarsInteractionSystem.Instance?.OpenInspector(t);
                        break;
                    }
                }
                yield return WaitFrames(18);
                yield return CaptureShot(diag, "gp_07_rock_inspector.png", "cheyava_inspector");
            }

            File.WriteAllText(
                Path.Combine(outDir, "gameplay_report.json"),
                "{\n  \"discovered\": " + MarsInspectableTarget.DiscoveredCount +
                ",\n  \"total_targets\": " + MarsInspectableTarget.TotalCount +
                ",\n  \"shots\": [\n" + string.Join(",\n", diag) + "\n  ]\n}\n",
                Encoding.UTF8
            );

            Application.Quit(0);
        }

        private IEnumerator CaptureShot(System.Collections.Generic.List<string> diag, string filename, string label)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(outDir, filename);
            ScreenCapture.CaptureScreenshot(path);
            Camera cam = Camera.main;
            string camPos = cam != null ? cam.transform.position.ToString("F1") : "null";
            diag.Add($"    {{\"label\":\"{label}\",\"file\":\"{filename}\",\"cam\":\"{camPos}\",\"discovered\":{MarsInspectableTarget.DiscoveredCount}}}");
            yield return WaitFrames(4);
        }

        private static IEnumerator WaitFrames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }
    }
}
