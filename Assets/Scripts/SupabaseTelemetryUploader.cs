using System;
using System.Collections;
using System.Collections.Generic;
#if !UNITY_WEBGL
using System.IO;
#endif
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SupabaseTelemetryUploader : MonoBehaviour
{
    [Header("Supabase")]
    [Tooltip("Example: https://your-project.supabase.co")]
    public string supabaseUrl = "";
    [Tooltip("Use only the anon public key in Unity/WebGL builds.")]
    public string anonKey = "";

    [Header("Local Environment")]
    public bool loadLocalEnvironment = true;
    public string localEnvironmentFileName = ".env";

    [Header("Tables")]
    public string trashBinStateTable = "trash_bin_state_latest";
    public string trashTruckStateTable = "trash_truck_state_latest";

    [Header("Request")]
    [Min(1)] public int requestTimeoutSeconds = 10;
    public bool dropUploadWhenBusy = true;
    public bool logSuccessfulUploads = false;
    public bool logUploadErrors = true;

    public bool IsUploading { get; private set; }

    private void Awake()
    {
        LoadConfigFromEnvironment();
    }

    public void UploadLatestState(
        IReadOnlyList<TrashBinTelemetryPayload> trashBins,
        IReadOnlyList<TrashTruckTelemetryPayload> trashTrucks)
    {
        if (IsUploading && dropUploadWhenBusy)
        {
            return;
        }

        if (!HasSupabaseConfig())
        {
            if (logUploadErrors)
            {
                Debug.LogWarning("[SupabaseTelemetryUploader] Supabase URL or anon key is empty. Telemetry upload skipped.");
            }

            return;
        }

        StartCoroutine(UploadLatestStateRoutine(trashBins, trashTrucks));
    }

    private IEnumerator UploadLatestStateRoutine(
        IReadOnlyList<TrashBinTelemetryPayload> trashBins,
        IReadOnlyList<TrashTruckTelemetryPayload> trashTrucks)
    {
        IsUploading = true;

        if (trashBins != null && trashBins.Count > 0)
        {
            string json = SupabaseTelemetryJson.ToJsonArray(trashBins);
            yield return UpsertRows(trashBinStateTable, "simulation_id,bin_id", json);
        }

        if (trashTrucks != null && trashTrucks.Count > 0)
        {
            string json = SupabaseTelemetryJson.ToJsonArray(trashTrucks);
            yield return UpsertRows(trashTruckStateTable, "simulation_id,truck_id", json);
        }

        IsUploading = false;
    }

    private IEnumerator UpsertRows(string tableName, string conflictColumns, string json)
    {
        string endpoint = BuildRestEndpoint(tableName, conflictColumns);
        using UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = Mathf.Max(1, requestTimeoutSeconds);
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("apikey", anonKey);
        request.SetRequestHeader("Authorization", $"Bearer {anonKey}");
        request.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            if (logSuccessfulUploads)
            {
                Debug.Log($"[SupabaseTelemetryUploader] Uploaded telemetry rows to '{tableName}'.");
            }

            yield break;
        }

        if (logUploadErrors)
        {
            Debug.LogError($"[SupabaseTelemetryUploader] Upload failed for '{tableName}': {request.error} {request.downloadHandler.text}");
        }
    }

    private string BuildRestEndpoint(string tableName, string conflictColumns)
    {
        string trimmedUrl = supabaseUrl.TrimEnd('/');
        string escapedTable = Uri.EscapeDataString(tableName);
        string escapedConflict = Uri.EscapeDataString(conflictColumns);
        return $"{trimmedUrl}/rest/v1/{escapedTable}?on_conflict={escapedConflict}";
    }

    private bool HasSupabaseConfig()
    {
        return !string.IsNullOrWhiteSpace(supabaseUrl)
            && !string.IsNullOrWhiteSpace(anonKey);
    }

    private void LoadConfigFromEnvironment()
    {
        if (!loadLocalEnvironment)
        {
            return;
        }

        ApplyConfigValue("SUPABASE_URL", Environment.GetEnvironmentVariable("SUPABASE_URL"));
        ApplyConfigValue("SUPABASE_ANON_KEY", Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY"));

#if !UNITY_WEBGL
        if (HasSupabaseConfig() || string.IsNullOrWhiteSpace(localEnvironmentFileName))
        {
            return;
        }

        string envPath = Path.Combine(Application.dataPath, "..", localEnvironmentFileName);
        if (!File.Exists(envPath))
        {
            return;
        }

        string[] lines = File.ReadAllLines(envPath);
        for (int i = 0; i < lines.Length; i++)
        {
            ApplyEnvironmentLine(lines[i]);
        }
#endif
    }

    private void ApplyEnvironmentLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        string trimmed = line.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return;
        }

        int separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0)
        {
            return;
        }

        string key = trimmed.Substring(0, separatorIndex).Trim();
        string value = trimmed.Substring(separatorIndex + 1).Trim();
        value = UnquoteEnvironmentValue(value);
        ApplyConfigValue(key, value);
    }

    private void ApplyConfigValue(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (key == "SUPABASE_URL" && string.IsNullOrWhiteSpace(supabaseUrl))
        {
            supabaseUrl = value;
        }
        else if (key == "SUPABASE_ANON_KEY" && string.IsNullOrWhiteSpace(anonKey))
        {
            anonKey = value;
        }
    }

    private string UnquoteEnvironmentValue(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[value.Length - 1] == '"')
                || (value[0] == '\'' && value[value.Length - 1] == '\'')))
        {
            return value.Substring(1, value.Length - 2);
        }

        return value;
    }
}
