using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Tables")]
    public string trashBinStateTable = "trash_bin_state_latest";
    public string trashTruckStateTable = "trash_truck_state_latest";

    [Header("Request")]
    [Min(1)] public int requestTimeoutSeconds = 10;
    public bool dropUploadWhenBusy = true;
    public bool logSuccessfulUploads = false;
    public bool logUploadErrors = true;

    public bool IsUploading { get; private set; }

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
}
