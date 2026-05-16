using UnityEngine;

[System.Serializable]
public class TrashBinTelemetryPayload
{
    public string simulation_id;
    public string bin_id;
    public float x;
    public float y;
    public float z;
    public bool is_full;
    public int fill_level;
    public string updated_at;
}

[System.Serializable]
public class TrashTruckTelemetryPayload
{
    public string simulation_id;
    public string truck_id;
    public float x;
    public float y;
    public float z;
    public int collected_count;
    public int max_load;
    public string status;
    public string updated_at;
}

public static class SupabaseTelemetryPayloadFactory
{
    public static TrashBinTelemetryPayload CreateBinPayload(string simulationId, TrashCanStatus trashCan, string timestampUtc)
    {
        if (trashCan == null)
        {
            return null;
        }

        TrashCanFillSensor fillSensor = trashCan.fillSensor != null
            ? trashCan.fillSensor
            : trashCan.GetComponentInChildren<TrashCanFillSensor>();
        Vector3 position = trashCan.transform.position;
        int fillLevel = fillSensor != null ? fillSensor.TelemetryFillLevel : 0;
        bool isFull = trashCan.IsFull
            || (fillSensor != null && (fillSensor.IsFull || fillLevel >= fillSensor.fullTelemetryLevel));

        return new TrashBinTelemetryPayload
        {
            simulation_id = simulationId,
            bin_id = CreateTrashBinId(trashCan),
            x = position.x,
            y = position.y,
            z = position.z,
            is_full = isFull,
            fill_level = fillLevel,
            updated_at = timestampUtc
        };
    }

    public static TrashTruckTelemetryPayload CreateTruckPayload(string simulationId, GarbageTruckController truck, string timestampUtc)
    {
        if (truck == null)
        {
            return null;
        }

        Vector3 position = truck.transform.position;

        return new TrashTruckTelemetryPayload
        {
            simulation_id = simulationId,
            truck_id = CreateTruckId(truck),
            x = position.x,
            y = position.y,
            z = position.z,
            collected_count = truck.currentLoad,
            max_load = truck.maxLoad,
            status = ToTelemetryStatus(truck.State),
            updated_at = timestampUtc
        };
    }

    private static string CreateTrashBinId(TrashCanStatus trashCan)
    {
        if (trashCan.gridPosition.x >= 0 && trashCan.gridPosition.y >= 0)
        {
            return $"bin_{trashCan.gridPosition.x}_{trashCan.gridPosition.y}";
        }

        return CreateFallbackObjectId("bin", trashCan.gameObject);
    }

    private static string CreateTruckId(GarbageTruckController truck)
    {
        return CreateFallbackObjectId("truck", truck.gameObject);
    }

    private static string CreateFallbackObjectId(string prefix, GameObject target)
    {
        string safeName = target != null && !string.IsNullOrEmpty(target.name)
            ? target.name.Trim().Replace(" ", "_").ToLowerInvariant()
            : "unknown";

        return $"{prefix}_{safeName}";
    }

    private static string ToTelemetryStatus(GarbageTruckState state)
    {
        switch (state)
        {
            case GarbageTruckState.MovingToTrash:
            case GarbageTruckState.Collecting:
                return "collecting";
            case GarbageTruckState.ReturningToDepot:
                return "returning";
            default:
                return "idle";
        }
    }
}
