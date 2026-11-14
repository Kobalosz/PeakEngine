using System;
using System.Collections.Generic;
using System.Linq;
using PeakEngine.Core;
using UnityEngine;

namespace PeakEngine.Game
{
    /// <summary>
    /// Harmless-but-annoying things to do to your friends (or to yourself, to try them out).
    ///
    /// Every prank is one or two of the game's own network methods (RPCs) on the Character class.
    /// None of them check who is asking, so they work whether or not you are the host.
    ///
    /// TO ADD A PRANK: add one entry to the list below. It shows up in the panel's Mischief tab
    /// automatically; give it a nicer label in PRANK_TEXT in panel/js/mischief.js.
    /// </summary>
    public static class Pranks
    {
        private sealed class Prank
        {
            public string Name;
            /// <summary>The game methods this prank calls. Used to grey it out if the game lacks one.</summary>
            public string[] Needs;
            /// <summary>Does it. The number is the "power" slider from the panel, 1 to 10.</summary>
            public Action<object, float> Do;
        }

        private static readonly List<Prank> All = new List<Prank>
        {
            new Prank
            {
                // Go limp and fall over for a few seconds.
                // [PunRPC] Character.RPCA_Fall(float seconds, float ...)
                Name = "trip",
                Needs = new[] { "RPCA_Fall" },
                Do = (who, power) => Net.Rpc(who, "RPCA_Fall", Net.To.All, 1f + power * 0.5f, 0f),
            },
            new Prank
            {
                // Throw them into the air. They are made limp first, otherwise their legs absorb the push.
                // [PunRPC] Character.RPCA_AddForceAtPosition(Vector3 force, Vector3 position, float radius)
                Name = "launch",
                Needs = new[] { "RPCA_Fall", "RPCA_AddForceAtPosition" },
                Do = (who, power) =>
                {
                    Net.Rpc(who, "RPCA_Fall", Net.To.All, 3f, 0f);
                    Vector3 push = Vector3.up * (power * ForcePerPowerStep);
                    Net.Rpc(who, "RPCA_AddForceAtPosition", Net.To.All, push, Players.PositionOf(who), 4f);
                },
            },
            new Prank
            {
                // Make them hop.
                // [PunRPC] Character.JumpRpc(bool ...)
                Name = "jump",
                Needs = new[] { "JumpRpc" },
                Do = (who, power) => Net.Rpc(who, "JumpRpc", Net.To.All, false),
            },
            new Prank
            {
                // Make them let go of whatever they are climbing.
                // [PunRPC] Character.StopClimbingRpc(float ...)
                Name = "letGo",
                Needs = new[] { "StopClimbingRpc" },
                Do = (who, power) => Net.Rpc(who, "StopClimbingRpc", Net.To.All, 0f),
            },
            new Prank
            {
                // Pass out on the spot. Revive (on the Players tab) wakes them up again.
                // [PunRPC] Character.RPCA_PassOut()
                Name = "knockOut",
                Needs = new[] { "RPCA_PassOut" },
                Do = (who, power) => Net.Rpc(who, "RPCA_PassOut", Net.To.All),
            },
            new Prank
            {
                // Stop them from moving until you unfreeze them.
                // [PunRPC] Character.RPC_SetBlockedState(bool blocked)
                Name = "freeze",
                Needs = new[] { "RPC_SetBlockedState" },
                Do = (who, power) => Net.Rpc(who, "RPC_SetBlockedState", Net.To.All, true),
            },
            new Prank
            {
                Name = "unfreeze",
                Needs = new[] { "RPC_SetBlockedState" },
                Do = (who, power) => Net.Rpc(who, "RPC_SetBlockedState", Net.To.All, false),
            },
            new Prank
            {
                // Teleport them straight up and let gravity do the rest. Higher power = higher up.
                // [PunRPC] Character.WarpPlayerRPC(Vector3, bool)
                Name = "skyDrop",
                Needs = new[] { "WarpPlayerRPC" },
                Do = (who, power) =>
                {
                    Vector3 above = Players.PositionOf(who) + Vector3.up * (power * MetresPerPowerStep);
                    Net.Rpc(who, "WarpPlayerRPC", Net.To.All, above, true);
                },
            },
        };

        /// <summary>
        /// How hard "launch" pushes per step of the power slider. The right number depends on the
        /// game's physics, so this is a starting guess: raise it if launches feel weak in game.
        /// </summary>
        private const float ForcePerPowerStep = 40f;

        private const float MetresPerPowerStep = 15f;

        private static List<JsonObject> _described;

        /// <summary>Every prank as { name, available } so the panel can draw a button for each.</summary>
        public static List<JsonObject> Describe()
        {
            // Which methods the game has cannot change while it runs, so work this out once.
            if (_described != null) return _described;

            Type character = Reflect.FindType("Character");
            var described = All.Select(prank => new JsonObject
            {
                ["name"] = prank.Name,
                ["available"] = character != null && prank.Needs.All(method => Reflect.HasMethod(character, method)),
            }).ToList();

            if (character != null) _described = described;
            return described;
        }

        /// <summary>The game methods all pranks rely on, for the Diagnostics tab.</summary>
        public static IEnumerable<string> NeededMethods => All.SelectMany(prank => prank.Needs).Distinct();

        public static void Do(string name, object character, float power)
        {
            var prank = All.Find(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ApiError(404, $"There is no prank called '{name}'.");
            Net.RequireRoom();
            prank.Do(character, Math.Max(1f, Math.Min(10f, power)));
        }

        /// <summary>Make two players trade places.</summary>
        public static void Swap(object first, object second)
        {
            if (ReferenceEquals(first, second)) throw new ApiError(400, "Pick two different players to swap.");
            Net.RequireRoom();

            // Read both positions BEFORE moving anyone, or the second player would end up where they started.
            Vector3 firstSpot = Players.PositionOf(first);
            Vector3 secondSpot = Players.PositionOf(second);
            Net.Rpc(first, "WarpPlayerRPC", Net.To.All, secondSpot + Vector3.up, true);
            Net.Rpc(second, "WarpPlayerRPC", Net.To.All, firstSpot + Vector3.up, true);
        }
    }
}
