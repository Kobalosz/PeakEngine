using System;
using System.Collections.Generic;
using PeakEngine.Core;

namespace PeakEngine.Game
{
    /// <summary>
    /// Status bars: Injury, Hunger, Cold, Poison, Curse, Drowsy...
    ///
    /// Each status is a number from 0 (none) to 1 (fills the whole stamina bar). Together they eat
    /// into your stamina, and when they add up to 1 you pass out.
    ///
    /// WHO IS ALLOWED TO CHANGE THEM
    ///   - Your own: always. Your game owns your statuses and tells everyone else.
    ///   - A friend's: only if you are the lobby HOST. The game has a network method for it,
    ///     and it ignores the call unless it comes from the host.
    /// </summary>
    public static class Statuses
    {
        /// <summary>
        /// Statuses the game recalculates by itself every moment (from what you carry, and from
        /// thorns stuck in you). Changing them has no lasting effect, so the panel shows them read-only.
        /// </summary>
        public static readonly HashSet<string> Computed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Weight", "Thorns" };

        /// <summary>The name of every status in this version of the game, in the game's own order.</summary>
        public static string[] Names => Enum.GetNames(GameTypes.StatusType);

        private static object AfflictionsOf(object character) => Reflect.Get(character, "refs.afflictions");

        private static object Parse(string name)
        {
            try { return Enum.Parse(GameTypes.StatusType, name, true); }
            catch { throw new ApiError(400, $"There is no status called '{name}'."); }
        }

        private static float Current(object afflictions, object status) =>
            Convert.ToSingle(Reflect.Call(afflictions, "GetCurrentStatus", status));

        /// <summary>All statuses of a character as { "Hunger": 0.25, "Cold": 0, ... }.</summary>
        public static JsonObject Read(object character)
        {
            object afflictions = AfflictionsOf(character);
            var values = new JsonObject();
            foreach (string name in Names)
                values[name] = Current(afflictions, Enum.Parse(GameTypes.StatusType, name));
            return values;
        }

        /// <summary>Set one status of a character to an exact amount between 0 and 1.</summary>
        public static void Set(object character, string name, float amount)
        {
            object status = Parse(name);
            amount = Math.Max(0f, Math.Min(1f, amount));
            object afflictions = AfflictionsOf(character);

            if (Players.IsMe(character))
            {
                // CharacterAfflictions.SetStatus(STATUSTYPE type, float amount, ...)
                Reflect.Call(afflictions, "SetStatus", status, amount);
                TellEveryone(afflictions);
                return;
            }

            RequireHostFor(character);
            if (Computed.Contains(name))
                throw new ApiError(400, $"{name} is calculated by the game and cannot be set on other players.");

            var changes = new float[Slots(afflictions)];
            changes[Convert.ToInt32(status)] = amount - Current(afflictions, status);
            SendChanges(afflictions, changes);
        }

        /// <summary>Remove every status from a character (a full heal).</summary>
        public static void ClearAll(object character)
        {
            object afflictions = AfflictionsOf(character);

            if (Players.IsMe(character))
            {
                ClearMine(afflictions);
                return;
            }

            RequireHostFor(character);
            var changes = new float[Slots(afflictions)];
            foreach (string name in Names)
            {
                if (Computed.Contains(name)) continue;
                object status = Enum.Parse(GameTypes.StatusType, name);
                changes[Convert.ToInt32(status)] = -Current(afflictions, status);
            }
            SendChanges(afflictions, changes);
        }

        /// <summary>
        /// Zero your own statuses that are above <paramref name="ignoreBelow"/>.
        /// Returns true if anything actually changed.
        /// </summary>
        internal static bool ClearMine(object afflictions, float ignoreBelow = 0f)
        {
            bool changed = false;
            foreach (string name in Names)
            {
                if (Computed.Contains(name)) continue;
                object status = Enum.Parse(GameTypes.StatusType, name);
                if (Current(afflictions, status) <= ignoreBelow) continue;
                Reflect.Call(afflictions, "SetStatus", status, 0f);
                changed = true;
            }
            if (changed) TellEveryone(afflictions);
            return changed;
        }

        /// <summary>
        /// Used by the "no statuses" toggle. It ignores amounts too small to see (under 1% of the
        /// bar), so it does not send a network update for every tiny bit of hunger.
        /// </summary>
        internal static bool ClearMineQuietly() => Players.Local != null && ClearMine(AfflictionsOf(Players.Local), 0.01f);

        // ---- Networking --------------------------------------------------------------------------

        /// <summary>After changing our own statuses, sync them so friends see the same bars.</summary>
        private static void TellEveryone(object afflictions)
        {
            // CharacterAfflictions.PushStatuses(Player specificPlayer = null)
            // Not fatal if this is missing: the game also syncs statuses on its own timer.
            try { Reflect.Call(afflictions, "PushStatuses"); } catch (MissingGameMemberException) { }
        }

        /// <summary>
        /// Ask another player's game to change their statuses.
        /// [PunRPC] CharacterAfflictions.RPC_ApplyStatusesFromFloatArray(float[] changes)
        /// The array has one entry per status and holds how much to ADD (negative removes).
        /// </summary>
        private static void SendChanges(object afflictions, float[] changes)
        {
            Net.Rpc(afflictions, "RPC_ApplyStatusesFromFloatArray", Net.To.Owner, new object[] { changes });
        }

        private static void RequireHostFor(object character)
        {
            if (!Net.IsHost)
                throw new ApiError(403,
                    $"Only the lobby host can change another player's statuses. Host the lobby to do this to {Players.NameOf(character)}.");
        }

        /// <summary>How many entries the game's status array has. The network call requires an exact match.</summary>
        private static int Slots(object afflictions)
        {
            if (Reflect.GetOr<Array>(afflictions, "currentStatuses", null) is Array current) return current.Length;
            try { return Convert.ToInt32(Reflect.GetStatic(GameTypes.Afflictions, "NumStatusTypes")); }
            catch { return Names.Length; }
        }
    }
}
