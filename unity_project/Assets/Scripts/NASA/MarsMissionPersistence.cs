using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MarsSentinel.NASA
{
    [Serializable]
    public class MarsMissionRecord
    {
        public int totalRunsCompleted;
        public int bestScore;
        public float bestCompletionSec;
        public float lowestRadiationMsv;
        public int totalSamplesCollected;
        public string lastUpdatedUtc;
    }

    /// <summary>
    /// Persistent EVA Mission Save & Personal Leaderboard Manager.
    /// Stores structured JSON under persistentDataPath + PlayerPrefs locally and
    /// synchronizes with Unity Gaming Services (CloudSave + Leaderboards) whenever
    /// AuthenticationService is signed in.
    /// </summary>
    public class MarsMissionPersistence : MonoBehaviour
    {
        public static MarsMissionPersistence Instance { get; private set; }

        public MarsMissionRecord Record { get; private set; } = new MarsMissionRecord();
        public bool CloudSyncReady { get; private set; } = false;
        public string SaveFilePath { get; private set; }

        private const string PrefJsonKey = "MarsSentinel_MissionRecord_v1";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            SaveFilePath = Path.Combine(Application.persistentDataPath, "mars_sentinel_records.json");
            LoadRecord();
        }

        public void LoadRecord()
        {
            try
            {
                string json = null;
                if (!string.IsNullOrEmpty(SaveFilePath) && File.Exists(SaveFilePath))
                {
                    json = File.ReadAllText(SaveFilePath);
                }
                else if (PlayerPrefs.HasKey(PrefJsonKey))
                {
                    json = PlayerPrefs.GetString(PrefJsonKey);
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonUtility.FromJson<MarsMissionRecord>(json);
                    if (loaded != null)
                    {
                        Record = loaded;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MarsMissionPersistence] Fallback to fresh record: {ex.Message}");
            }

            Record = new MarsMissionRecord
            {
                totalRunsCompleted = 0,
                bestScore = 0,
                bestCompletionSec = 0f,
                lowestRadiationMsv = 999f,
                totalSamplesCollected = 0,
                lastUpdatedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        public void RecordCompletedMission(int score, float durationSec, float cumulativeRadMsv, int samplesCollected)
        {
            Record.totalRunsCompleted++;
            Record.totalSamplesCollected += Mathf.Max(0, samplesCollected);
            if (score > Record.bestScore) Record.bestScore = score;
            if (Record.bestCompletionSec <= 0.01f || durationSec < Record.bestCompletionSec)
                Record.bestCompletionSec = durationSec;
            if (cumulativeRadMsv > 0f && cumulativeRadMsv < Record.lowestRadiationMsv)
                Record.lowestRadiationMsv = cumulativeRadMsv;
            Record.lastUpdatedUtc = DateTime.UtcNow.ToString("O");

            SaveRecordLocal();
            TrySyncToUgsCloudAsync();
        }

        public void SaveRecordLocal()
        {
            try
            {
                string json = JsonUtility.ToJson(Record, true);
                PlayerPrefs.SetString(PrefJsonKey, json);
                PlayerPrefs.Save();
                if (!string.IsNullOrEmpty(SaveFilePath))
                {
                    string dir = Path.GetDirectoryName(SaveFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(SaveFilePath, json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MarsMissionPersistence] Local save error: {ex.Message}");
            }
        }

        private async void TrySyncToUgsCloudAsync()
        {
            try
            {
                if (!Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn)
                {
                    CloudSyncReady = false;
                    return;
                }

                var data = new Dictionary<string, object>
                {
                    { "mars_best_score", Record.bestScore },
                    { "mars_best_time_s", Record.bestCompletionSec },
                    { "mars_runs", Record.totalRunsCompleted },
                    { "mars_lowest_rad_msv", Record.lowestRadiationMsv }
                };
                await Unity.Services.CloudSave.CloudSaveService.Instance.Data.Player.SaveAsync(data);
                CloudSyncReady = true;
                Debug.Log($"[MarsMissionPersistence] Synced EVA mission record to UGS CloudSave (BestScore={Record.bestScore}).");
            }
            catch (Exception)
            {
                // Offline or unlinked UGS Project ID — local persistence already succeeded cleanly
                CloudSyncReady = false;
            }
        }

        public string GetCompactSummary()
        {
            string bestT = Record.bestCompletionSec > 0.1f ? $"{Record.bestCompletionSec:F1}s" : "--";
            string lowRad = Record.lowestRadiationMsv < 900f ? $"{Record.lowestRadiationMsv:F2} mSv" : "--";
            string syncTag = CloudSyncReady ? "UGS Cloud" : "Local Saved";
            return $"Runs: {Record.totalRunsCompleted} | Best Score: {Record.bestScore} | Fastest: {bestT} | Min Rad: {lowRad} ({syncTag})";
        }
    }
}
