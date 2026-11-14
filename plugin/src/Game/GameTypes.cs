using System;
using PeakEngine.Core;

namespace PeakEngine.Game
{
    /// <summary>
    /// The game classes we talk to, looked up by name.
    ///
    /// These names come from PEAK's own code (Assembly-CSharp.dll) and from Photon, the networking
    /// library PEAK uses for multiplayer. If a game update renames one, this is the file to fix,
    /// and the Diagnostics tab in the control panel tells you which one is missing.
    ///
    /// Want to explore the game's classes yourself? See docs/05-troubleshooting.md for how to
    /// open Assembly-CSharp.dll in a decompiler.
    /// </summary>
    public static class GameTypes
    {
        // -- PEAK ---------------------------------------------------------------------------------

        /// <summary>One player (or bot) in the world. Holds almost everything about them.</summary>
        public static Type Character => Reflect.RequireType("Character");

        /// <summary>The status bars of a character: hunger, cold, poison, injury...</summary>
        public static Type Afflictions => Reflect.RequireType("CharacterAfflictions");

        /// <summary>The list of status kinds. The "+" means it is declared inside CharacterAfflictions.</summary>
        public static Type StatusType => Reflect.RequireType("CharacterAfflictions+STATUSTYPE");

        /// <summary>What a character is holding and carrying.</summary>
        public static Type CharacterItems => Reflect.RequireType("CharacterItems");

        /// <summary>The catalogue of every item that exists in the game.</summary>
        public static Type ItemDatabase => Reflect.RequireType("ItemDatabase");

        /// <summary>One item (rope, coconut, bandages...).</summary>
        public static Type Item => Reflect.RequireType("Item");

        // -- Photon (multiplayer) -----------------------------------------------------------------

        /// <summary>Global multiplayer state: are we in a room, are we the host, spawn networked objects.</summary>
        public static Type PhotonNetwork => Reflect.RequireType("Photon.Pun.PhotonNetwork");

        /// <summary>The network identity attached to a synced object. Used to send messages about it.</summary>
        public static Type PhotonView => Reflect.RequireType("Photon.Pun.PhotonView");

        /// <summary>Who should receive a network message: All, MasterClient (the host), Others...</summary>
        public static Type RpcTarget => Reflect.RequireType("Photon.Pun.RpcTarget");
    }
}
