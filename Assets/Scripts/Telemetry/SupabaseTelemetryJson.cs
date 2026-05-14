using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class SupabaseTelemetryJson
{
    public static string ToJsonArray(IReadOnlyList<TrashBinTelemetryPayload> payloads)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append('[');

        for (int i = 0; i < payloads.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendTrashBin(builder, payloads[i]);
        }

        builder.Append(']');
        return builder.ToString();
    }

    public static string ToJsonArray(IReadOnlyList<TrashTruckTelemetryPayload> payloads)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append('[');

        for (int i = 0; i < payloads.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendTrashTruck(builder, payloads[i]);
        }

        builder.Append(']');
        return builder.ToString();
    }

    private static void AppendTrashBin(StringBuilder builder, TrashBinTelemetryPayload payload)
    {
        builder.Append('{');
        AppendString(builder, "simulation_id", payload.simulation_id);
        AppendString(builder, "bin_id", payload.bin_id, true);
        AppendFloat(builder, "x", payload.x, true);
        AppendFloat(builder, "y", payload.y, true);
        AppendFloat(builder, "z", payload.z, true);
        AppendBool(builder, "is_full", payload.is_full, true);
        AppendFloat(builder, "fill_ratio", payload.fill_ratio, true);
        AppendInt(builder, "current_amount", payload.current_amount, true);
        AppendInt(builder, "capacity", payload.capacity, true);
        AppendString(builder, "updated_at", payload.updated_at, true);
        builder.Append('}');
    }

    private static void AppendTrashTruck(StringBuilder builder, TrashTruckTelemetryPayload payload)
    {
        builder.Append('{');
        AppendString(builder, "simulation_id", payload.simulation_id);
        AppendString(builder, "truck_id", payload.truck_id, true);
        AppendFloat(builder, "x", payload.x, true);
        AppendFloat(builder, "y", payload.y, true);
        AppendFloat(builder, "z", payload.z, true);
        AppendInt(builder, "collected_count", payload.collected_count, true);
        AppendInt(builder, "max_load", payload.max_load, true);
        AppendString(builder, "status", payload.status, true);
        AppendString(builder, "updated_at", payload.updated_at, true);
        builder.Append('}');
    }

    private static void AppendString(StringBuilder builder, string name, string value, bool prependComma = false)
    {
        if (prependComma)
        {
            builder.Append(',');
        }

        builder.Append('"');
        builder.Append(Escape(name));
        builder.Append("\":\"");
        builder.Append(Escape(value));
        builder.Append('"');
    }

    private static void AppendFloat(StringBuilder builder, string name, float value, bool prependComma)
    {
        if (prependComma)
        {
            builder.Append(',');
        }

        builder.Append('"');
        builder.Append(Escape(name));
        builder.Append("\":");
        builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void AppendInt(StringBuilder builder, string name, int value, bool prependComma)
    {
        if (prependComma)
        {
            builder.Append(',');
        }

        builder.Append('"');
        builder.Append(Escape(name));
        builder.Append("\":");
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendBool(StringBuilder builder, string name, bool value, bool prependComma)
    {
        if (prependComma)
        {
            builder.Append(',');
        }

        builder.Append('"');
        builder.Append(Escape(name));
        builder.Append("\":");
        builder.Append(value ? "true" : "false");
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }
}
