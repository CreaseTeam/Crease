using System;
using System.Collections.Generic;
using System.Reflection;

namespace Crease.Flying.Player.FlightSettings
{
    /// <summary>
    /// Central read/write access for all flight stats. Keeps modifier logic in one place
    /// so adding a stat only requires a matching field on <see cref="FlightSettings"/>
    /// and enum entry in <see cref="FlightStatType"/>.
    /// </summary>
    public static class FlightStatAccessor
    {
        private static readonly FlightStatType[] AllStats;
        private static readonly Dictionary<FlightStatType, FieldInfo> Fields;

        static FlightStatAccessor()
        {
            Fields = new Dictionary<FlightStatType, FieldInfo>();
            AllStats = (FlightStatType[])Enum.GetValues(typeof(FlightStatType));

            foreach (FlightStatType stat in AllStats)
            {
                FieldInfo field = typeof(FlightSettings).GetField(
                    stat.ToString(),
                    BindingFlags.Public | BindingFlags.Instance);

                if (field == null || field.FieldType != typeof(float))
                {
                    throw new InvalidOperationException(
                        $"FlightStatType.{stat} has no matching public float field on FlightSettings.");
                }

                Fields[stat] = field;
            }

            foreach (FieldInfo field in typeof(FlightSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(float))
                    continue;

                if (!Enum.TryParse(field.Name, out FlightStatType _))
                {
                    throw new InvalidOperationException(
                        $"FlightSettings.{field.Name} has no matching FlightStatType entry.");
                }
            }
        }

        public static IReadOnlyList<FlightStatType> All => AllStats;

        public static float Get(FlightSettings settings, FlightStatType stat)
        {
            if (settings == null) return 0f;
            return (float)Fields[stat].GetValue(settings);
        }

        public static void Set(FlightSettings settings, FlightStatType stat, float value)
        {
            if (settings == null) return;
            Fields[stat].SetValue(settings, value);
        }

        public static void CopyFrom(FlightSettings source, FlightSettings destination)
        {
            if (source == null || destination == null) return;

            foreach (FlightStatType stat in AllStats)
                Set(destination, stat, Get(source, stat));
        }

        public static void SetAllZero(FlightSettings settings)
        {
            if (settings == null) return;

            foreach (FlightStatType stat in AllStats)
                Set(settings, stat, 0f);
        }

        public static void AddInto(FlightSettings source, FlightSettings destination)
        {
            if (source == null || destination == null) return;

            foreach (FlightStatType stat in AllStats)
                Set(destination, stat, Get(destination, stat) + Get(source, stat));
        }

        public static void ComputeDelta(FlightSettings target, FlightSettings current, FlightSettings delta)
        {
            if (target == null || current == null || delta == null) return;

            foreach (FlightStatType stat in AllStats)
                Set(delta, stat, Get(target, stat) - Get(current, stat));
        }
    }
}
