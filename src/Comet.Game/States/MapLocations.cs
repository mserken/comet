namespace Comet.Game.States
{
    using System;
    using System.Collections.Generic;

    public sealed class MapLocation
    {
        public MapLocation(uint mapId, ushort x, ushort y)
        {
            MapID = mapId;
            X = x;
            Y = y;
        }

        public uint MapID { get; }
        public ushort X { get; }
        public ushort Y { get; }
    }

    public static class MapLocations
    {
        private static readonly Dictionary<string, uint> MapIDs =
            new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
            {
                ["dc"] = 1000,
                ["desert_city"] = 1000,

                ["tc"] = 1002,
                ["twin_city"] = 1002,

                ["bv"] = 1010,
                ["birth_village"] = 1010,

                ["pc"] = 1011,
                ["phoenix_castle"] = 1011,

                ["bi"] = 1015,
                ["bird_island"] = 1015,
                ["ri"] = 1015,
                ["reed_island"] = 1015,

                ["ac"] = 1020,
                ["ape_city"] = 1020,

                ["market"] = 1036,
            };

        private static readonly Dictionary<uint, MapLocation> Defaults =
            new Dictionary<uint, MapLocation>
            {
                [1000] = new MapLocation(1000, 500, 650), // DesertCity
                [1002] = new MapLocation(1002, 430, 378), // TwinCity
                [1010] = new MapLocation(1010, 61, 109), // BirthVillage
                [1011] = new MapLocation(1011, 193, 268), // PhoenixCastle
                [1015] = new MapLocation(1015, 717, 571), // ReedIsland
                [1020] = new MapLocation(1020, 565, 562), // ApeCity
                [1036] = new MapLocation(1036, 100, 100) // Market
            };

        public static bool TryGetMapID(string value, out uint mapId)
        {
            if (uint.TryParse(value, out mapId))
                return true;

            return MapIDs.TryGetValue(value, out mapId);
        }

        public static bool TryGetDefault(uint mapId, out MapLocation location)
        {
            return Defaults.TryGetValue(mapId, out location);
        }
    }
}
