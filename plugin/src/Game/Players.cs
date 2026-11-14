using System;
using System.Collections;
using System.Collections.Generic;
using PeakEngine.Core;
using UnityEngine;

namespace PeakEngine.Game
{
    /// <summary>
    /// Finding players and doing things to them.
    ///
    /// In PEAK every player is a <c>Character</c>. The interesting parts hang off it:
    ///   character.data   - numbers that change all the time (stamina, dead, passed out...)
    ///   character.refs   - links to its other components (afflictions = status bars, items...)
    ///   character.photonView - its network identity
    /// </summary>
    public static class Players
    {
        /// <summary>Your own character, or null when you are not in the world (main menu, loading).</summary>
        public static object Local
        {
            get
            {
                try { return Alive(Reflect.GetStatic(GameTypes.Character, "localCharacter")); }
                catch (MissingGameMemberException) { throw; }
                catch { return null; }
            }
        }

        /// <summary>Every human player currently in the world, including you.</summary>
        public static List<object> All()
        {
            var players = new List<object>();
            if (!(Reflect.GetStatic(GameTypes.Character, "AllCharacters") is IEnumerable all)) return players;

            foreach (object character in all)
            {
                if (Alive(character) == null) continue;
                if (Reflect.GetOr(character, "isBot", false)) continue;
                players.Add(character);
            }
            return players;
        }

        /// <summary>
        /// Unity keeps destroyed objects around as "fake null". This returns real null for those,
        /// so we never touch a character that already left the game.
        /// </summary>
        private static object Alive(object thing)
        {
            if (thing is UnityEngine.Object unityObject) return unityObject != null ? thing : null;
            return thing;
        }

        /// <summary>A number that identifies a player for as long as they stay in the lobby.</summary>
        public static int IdOf(object character) => Reflect.GetOr(character, "photonView.ViewID", 0);

        public static object Find(int id)
        {
            foreach (object character in All())
                if (IdOf(character) == id) return character;
            throw new ApiError(404, "That player is not in the game any more.");
        }

        public static bool IsMe(object character) => ReferenceEquals(character, Local);

        public static string NameOf(object character)
        {
            string nick = Reflect.GetOr<string>(character, "photonView.Owner.NickName", null);
            if (!string.IsNullOrEmpty(nick)) return nick;
            return Reflect.GetOr(character, "characterName", "Player");
        }

        public static Vector3 PositionOf(object character) => (Vector3)Reflect.Get(character, "Center");

        public static bool IsDead(object character) => Reflect.GetOr(character, "data.dead", false);

        /// <summary>Everything the control panel shows about one player.</summary>
        public static JsonObject Describe(object character)
        {
            var info = new JsonObject
            {
                ["id"] = IdOf(character),
                ["name"] = NameOf(character),
                ["isMe"] = IsMe(character),
                ["isHost"] = Reflect.GetOr(character, "photonView.Owner.IsMasterClient", false),
                ["dead"] = IsDead(character),
                ["passedOut"] = Reflect.GetOr(character, "data.passedOut", false) || Reflect.GetOr(character, "data.fullyPassedOut", false),
                ["stamina"] = Reflect.GetOr(character, "data.currentStamina", 0f),
                ["extraStamina"] = Reflect.GetOr(character, "data.extraStamina", 0f),
            };

            try { info["statuses"] = Statuses.Read(character); }
            catch (Exception e) { info["statuses"] = new JsonObject(); info["statusError"] = e.Message; }

            try
            {
                Vector3 p = PositionOf(character);
                info["position"] = new[] { p.x, p.y, p.z };
            }
            catch { info["position"] = null; }

            return info;
        }

        // ---- Actions -----------------------------------------------------------------------------

        /// <summary>
        /// Refill stamina. For yourself it fills both the normal and the bonus bar. For a friend it
        /// sends the game's "morale boost", which fills their bonus stamina bar.
        /// </summary>
        public static void FillStamina(object character)
        {
            if (IsMe(character))
            {
                float max = 1f;
                try { max = Convert.ToSingle(Reflect.Call(character, "GetMaxStamina")); } catch { /* keep 1 */ }
                Reflect.Set(character, "data.currentStamina", max);
                Reflect.Call(character, "SetExtraStamina", 1f);
            }
            else
            {
                // [PunRPC] Character.MoraleBoost(float, int): how much bonus stamina to add, and for how many scouts.
                Net.Rpc(character, "MoraleBoost", Net.To.Owner, 1f, 1);
            }
        }

        /// <summary>Bring a dead or passed-out player back, next to you (or where they are if you are dead too).</summary>
        public static void Revive(object character)
        {
            object me = Local;
            object anchor = me != null && !IsMe(character) && !IsDead(me) ? me : character;
            Vector3 position = PositionOf(anchor) + Vector3.up * 1.5f;

            // [PunRPC] Character.RPCA_ReviveAtPosition(Vector3, bool, int)
            // The bool is "apply the revive penalty" (hunger and curse); false skips it.
            // Some game versions do not have the third argument; Net.Rpc trims it when it is not declared.
            Net.Rpc(character, "RPCA_ReviveAtPosition", Net.To.All, position, false, -1);
        }

        /// <summary>Move <paramref name="who"/> to where <paramref name="destination"/> is standing.</summary>
        public static void Teleport(object who, object destination)
        {
            if (ReferenceEquals(who, destination)) throw new ApiError(400, "That player is already there.");
            Vector3 position = PositionOf(destination) + Vector3.up * 1f;

            // [PunRPC] Character.WarpPlayerRPC(Vector3, bool): where to go, and whether to play the "poof" effect.
            Net.Rpc(who, "WarpPlayerRPC", Net.To.All, position, true);
        }
    }
}
