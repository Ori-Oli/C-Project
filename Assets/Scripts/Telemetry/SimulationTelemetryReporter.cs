using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = ProjectSilentDebug;

public class SimulationTelemetryReporter : MonoBehaviour
{
    private const string RuntimeTelemetryObjectName = "SupabaseTelemetry";

    [Header("References")]
    public SupabaseTelemetryUploader uploader;

    [Header("Simulation")]
    public string simulationId = "";
    public bool generateSimulationIdOnStart = true;

    [Header("Upload")]
    [Min(0.25f)] public float uploadIntervalSeconds = 1f;
    public bool uploadTrashBins = true;
    public bool uploadGarbageTrucks = true;

    [Header("Discovery")]
    public bool autoFindUploader = true;
    public bool includeInactiveObjects = false;

    private readonly List<TrashBinTelemetryPayload> trashBinBuffer = new List<TrashBinTelemetryPayload>();
    private readonly List<TrashTruckTelemetryPayload> trashTruckBuffer = new List<TrashTruckTelemetryPayload>();
    private readonly List<TrashCanStatus> trashCanSortBuffer = new List<TrashCanStatus>();
    private readonly Dictionary<TrashCanStatus, string> trashBinNames = new Dictionary<TrashCanStatus, string>();
    private Coroutine uploadRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeTelemetryReporter()
    {
        if (FindAnyObjectByType<SimulationTelemetryReporter>() != null)
        {
            return;
        }

        GameObject telemetryObject = new GameObject(RuntimeTelemetryObjectName);
        telemetryObject.SetActive(false);

        SupabaseTelemetryUploader runtimeUploader = telemetryObject.AddComponent<SupabaseTelemetryUploader>();
        runtimeUploader.logSuccessfulUploads = true;
        SimulationTelemetryReporter runtimeReporter = telemetryObject.AddComponent<SimulationTelemetryReporter>();
        runtimeReporter.uploader = runtimeUploader;
        runtimeReporter.autoFindUploader = false;

        telemetryObject.SetActive(true);
    }

    private void Awake()
    {
        if (autoFindUploader && uploader == null)
        {
            uploader = FindAnyObjectByType<SupabaseTelemetryUploader>();
        }

        if (generateSimulationIdOnStart && string.IsNullOrWhiteSpace(simulationId))
        {
            simulationId = Guid.NewGuid().ToString("N");
        }
    }

    private void OnEnable()
    {
        uploadRoutine = StartCoroutine(UploadLoop());
    }

    private void OnDisable()
    {
        if (uploadRoutine != null)
        {
            StopCoroutine(uploadRoutine);
            uploadRoutine = null;
        }
    }

    public void UploadNow()
    {
        if (uploader == null)
        {
            Debug.LogWarning("[SimulationTelemetryReporter] SupabaseTelemetryUploader is missing.");
            return;
        }

        CollectPayloads();
        uploader.UploadLatestState(trashBinBuffer, trashTruckBuffer);
    }

    private IEnumerator UploadLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.25f, uploadIntervalSeconds));

        while (enabled)
        {
            UploadNow();
            yield return wait;
        }
    }

    private void CollectPayloads()
    {
        string timestampUtc = DateTime.UtcNow.ToString("o");
        trashBinBuffer.Clear();
        trashTruckBuffer.Clear();
        trashBinNames.Clear();

        TrashCanStatus[] trashCans = null;
        if (uploadTrashBins || uploadGarbageTrucks)
        {
            trashCans = FindObjectsByType<TrashCanStatus>(
                includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
            BuildTrashBinNameMap(trashCans);
        }

        if (uploadTrashBins)
        {
            for (int i = 0; i < trashCans.Length; i++)
            {
                TrashBinTelemetryPayload payload = SupabaseTelemetryPayloadFactory.CreateBinPayload(
                    simulationId,
                    trashCans[i],
                    GetTrashBinName(trashCans[i]),
                    timestampUtc);

                if (payload != null)
                {
                    trashBinBuffer.Add(payload);
                }
            }
        }

        if (uploadGarbageTrucks)
        {
            GarbageTruckController[] trucks = FindObjectsByType<GarbageTruckController>(
                includeInactiveObjects ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);

            for (int i = 0; i < trucks.Length; i++)
            {
                TrashTruckTelemetryPayload payload = SupabaseTelemetryPayloadFactory.CreateTruckPayload(
                    simulationId,
                    trucks[i],
                    GetDestinationName(trucks[i]),
                    timestampUtc);

                if (payload != null)
                {
                    trashTruckBuffer.Add(payload);
                }
            }
        }
    }

    private void BuildTrashBinNameMap(TrashCanStatus[] trashCans)
    {
        trashCanSortBuffer.Clear();

        if (trashCans == null)
        {
            return;
        }

        for (int i = 0; i < trashCans.Length; i++)
        {
            if (trashCans[i] != null)
            {
                trashCanSortBuffer.Add(trashCans[i]);
            }
        }

        trashCanSortBuffer.Sort(CompareTrashCansByGrid);

        for (int i = 0; i < trashCanSortBuffer.Count; i++)
        {
            trashBinNames[trashCanSortBuffer[i]] = $"trashbin_{i + 1}";
        }
    }

    private static int CompareTrashCansByGrid(TrashCanStatus left, TrashCanStatus right)
    {
        int xCompare = left.gridPosition.x.CompareTo(right.gridPosition.x);
        if (xCompare != 0)
        {
            return xCompare;
        }

        int yCompare = left.gridPosition.y.CompareTo(right.gridPosition.y);
        if (yCompare != 0)
        {
            return yCompare;
        }

        return string.CompareOrdinal(left.name, right.name);
    }

    private string GetTrashBinName(TrashCanStatus trashCan)
    {
        if (trashCan != null && trashBinNames.TryGetValue(trashCan, out string binName))
        {
            return binName;
        }

        return string.Empty;
    }

    private string GetDestinationName(GarbageTruckController truck)
    {
        if (truck == null)
        {
            return string.Empty;
        }

        return GetTrashBinName(truck.AssignedTrashCan);
    }
}
