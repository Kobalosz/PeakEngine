using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PeakEngine.Core;
using UnityEngine;

namespace PeakEngine.Game
{
    /// <summary>
    /// Listing and spawning items.
    ///
    /// Every item in PEAK is a "prefab" (a template object) registered in the game's ItemDatabase.
    /// To spawn one that all players can see, we ask Photon to create it over the network, using
    /// the prefab's name: "0_Items/" + name.
    /// </summary>
    public static class Items
    {
        private const int MaxPerRequest = 20;
        private const float HeightAboveHead = 6f;

        private static List<JsonObject> _cache;

        /// <summary>Every item in the game as { name, displayName, id }, sorted by display name.</summary>
        public static List<JsonObject> List()
        {
            if (_cache != null && _cache.Count > 0) return _cache;

            var items = new List<JsonObject>();
            var seen = new HashSet<string>();
            foreach (object item in FindItemPrefabs())
            {
                if (item == null) continue;
                string name = Reflect.GetOr<string>(item, "name", null);
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;

                string displayName = Reflect.GetOr<string>(item, "UIData.itemName", null);
                items.Add(new JsonObject
                {
                    ["name"] = name,
                    ["displayName"] = string.IsNullOrEmpty(displayName) ? name : displayName,
                    ["id"] = Reflect.GetOr(item, "itemID", 0),
                });
            }

            _cache = items.OrderBy(i => (string)i["displayName"], StringComparer.OrdinalIgnoreCase).ToList();
            return _cache;
        }

        private static IEnumerable FindItemPrefabs()
        {
            // Preferred: the game's own catalogue. ItemDatabase.Instance.itemLookup is a
            // dictionary of item id -> Item.
            object database = null;
            try { database = Reflect.GetStatic(GameTypes.ItemDatabase, "Instance"); } catch { }
            if (database == null)
            {
                try { database = Resources.Load("ItemDatabase", GameTypes.ItemDatabase); } catch { }
            }
            if (database != null && Reflect.GetOr<IDictionary>(database, "itemLookup", null) is IDictionary lookup && lookup.Count > 0)
                return lookup.Values;

            // Fallback: every Item prefab Unity has loaded.
            try { return Resources.FindObjectsOfTypeAll(GameTypes.Item); }
            catch (MissingGameMemberException) { throw; }
            catch { return new object[0]; }
        }

        /// <summary>
        /// Give an item to a player.
        ///   mode "hand": put one straight into their hands.
        ///   mode "drop": drop <paramref name="count"/> of them on the ground in front of the player.
        ///   mode "above": drop <paramref name="count"/> of them from the air onto the player's head.
        /// </summary>
        public static void Spawn(string itemName, object character, string mode, int count)
        {
            Net.RequireRoom();

            // Only allow names from the item list, so this can never be used to spawn arbitrary prefabs.
            if (!List().Any(i => (string)i["name"] == itemName))
                throw new ApiError(404, $"There is no item called '{itemName}'.");

            if (mode == "hand")
            {
                // CharacterItems.SpawnItemInHand(string itemName) asks the host to create the item
                // directly in this character's hands. It works for you and for friends.
                Reflect.Call(Reflect.Get(character, "refs.items"), "SpawnItemInHand", itemName);
                return;
            }

            if (mode != "drop" && mode != "above") throw new ApiError(400, "mode must be 'hand', 'drop' or 'above'.");

            Vector3 origin;
            if (mode == "above")
            {
                origin = Players.PositionOf(character) + Vector3.up * HeightAboveHead;
            }
            else
            {
                Vector3 forward = Reflect.GetOr(character, "data.lookDirection_Flat", Vector3.forward);
                origin = Players.PositionOf(character) + forward * 1.2f + Vector3.up * 0.5f;
            }

            count = Math.Max(1, Math.Min(MaxPerRequest, count));
            for (int i = 0; i < count; i++)
            {
                // Stack them a little so they do not spawn inside each other.
                Net.Spawn("0_Items/" + itemName, origin + Vector3.up * (0.35f * i));
            }
        }
    }
}
