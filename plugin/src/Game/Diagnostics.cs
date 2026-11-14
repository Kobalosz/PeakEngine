using System;
using System.Collections.Generic;
using PeakEngine.Core;

namespace PeakEngine.Game
{
    /// <summary>
    /// A self-check: does this version of PEAK still have every class, field and method we use?
    ///
    /// The control panel shows the result in its Diagnostics tab. After a game update, this is the
    /// first place to look: a red row tells you exactly which name changed and which feature it breaks.
    /// </summary>
    public static class Diagnostics
    {
        public static List<JsonObject> Run()
        {
            var checks = new List<JsonObject>();

            Type character = Type("Character", "everything", checks);
            Type afflictions = Type("CharacterAfflictions", "statuses", checks);
            Type("CharacterAfflictions+STATUSTYPE", "statuses", checks);
            Type characterItems = Type("CharacterItems", "items in hand", checks);
            Type itemDatabase = Type("ItemDatabase", "item list", checks);
            Type("Item", "item list", checks);
            Type photonNetwork = Type("Photon.Pun.PhotonNetwork", "multiplayer and spawning", checks);
            Type photonView = Type("Photon.Pun.PhotonView", "anything that affects friends", checks);
            Type("Photon.Pun.RpcTarget", "anything that affects friends", checks);

            // The types of character.data and character.refs are found by following the fields.
            Type data = Reflect.MemberType(character, "data");
            Type refs = Reflect.MemberType(character, "refs");

            Member(character, "localCharacter", "finding you", checks);
            Member(character, "AllCharacters", "player list", checks);
            Member(character, "isBot", "player list", checks);
            Member(character, "photonView", "player ids, anything that affects friends", checks);
            Member(character, "Center", "positions, teleport, spawning", checks);
            Member(character, "data", "stamina, dead/passed out", checks);
            Member(character, "refs", "statuses, items in hand", checks);
            Member(character, "infiniteStam", "infinite stamina toggle", checks);
            Method(character, "GetMaxStamina", "fill stamina", checks);
            Method(character, "SetExtraStamina", "fill stamina (you)", checks);
            Method(character, "MoraleBoost", "fill stamina (friends)", checks);
            Method(character, "RPCA_ReviveAtPosition", "revive", checks);
            Method(character, "WarpPlayerRPC", "teleport", checks);

            Member(data, "currentStamina", "stamina", checks, "CharacterData");
            Member(data, "extraStamina", "stamina", checks, "CharacterData");
            Member(data, "dead", "dead indicator, revive", checks, "CharacterData");
            Member(data, "passedOut", "passed-out indicator", checks, "CharacterData");
            Member(data, "isInvincible", "invincible toggle", checks, "CharacterData");
            Member(data, "lookDirection_Flat", "dropping items in front of a player", checks, "CharacterData");

            Member(refs, "afflictions", "statuses", checks, "Character.refs");
            Member(refs, "items", "items in hand", checks, "Character.refs");

            Method(afflictions, "GetCurrentStatus", "reading statuses", checks);
            Method(afflictions, "SetStatus", "changing your statuses", checks);
            Method(afflictions, "PushStatuses", "syncing your statuses to friends", checks);
            Method(afflictions, "RPC_ApplyStatusesFromFloatArray", "changing friends' statuses (host)", checks);
            Member(afflictions, "currentStatuses", "changing friends' statuses (host)", checks);

            Method(characterItems, "SpawnItemInHand", "spawn item in hand", checks);
            Member(itemDatabase, "itemLookup", "item list", checks);
            Method(photonNetwork, "Instantiate", "drop items", checks);
            Member(photonNetwork, "InRoom", "multiplayer", checks);
            Member(photonNetwork, "IsMasterClient", "host check", checks);
            Method(photonView, "RPC", "anything that affects friends", checks);
            Member(photonView, "ViewID", "player ids", checks);
            Member(photonView, "Owner", "player names", checks);

            return checks;
        }

        /// <summary>How many checks failed. Logged when the plugin starts.</summary>
        public static int CountProblems(List<JsonObject> checks)
        {
            int problems = 0;
            foreach (var check in checks)
                if (!(bool)check["ok"]) problems++;
            return problems;
        }

        private static Type Type(string name, string usedFor, List<JsonObject> checks)
        {
            Type type = Reflect.FindType(name);
            checks.Add(Row("class " + name.Replace('+', '.'), type != null, usedFor));
            return type;
        }

        private static void Member(Type owner, string name, string usedFor, List<JsonObject> checks, string ownerLabel = null)
        {
            checks.Add(Row((owner?.Name ?? ownerLabel ?? "?") + "." + name, Reflect.HasMember(owner, name), usedFor));
        }

        private static void Method(Type owner, string name, string usedFor, List<JsonObject> checks)
        {
            checks.Add(Row((owner?.Name ?? "?") + "." + name + "()", Reflect.HasMethod(owner, name), usedFor));
        }

        private static JsonObject Row(string name, bool ok, string usedFor) =>
            new JsonObject { ["name"] = name, ["ok"] = ok, ["usedFor"] = usedFor };
    }
}
