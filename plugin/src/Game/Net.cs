using System;
using System.Linq;
using PeakEngine.Core;
using UnityEngine;

namespace PeakEngine.Game
{
    /// <summary>
    /// Multiplayer helpers.
    ///
    /// PEAK's multiplayer is built on "RPCs" (remote procedure calls): one player's game says
    /// "run the method called X on this object, on these players' computers". That is how you can
    /// affect your friends: you ask their game to run one of ITS OWN methods.
    ///
    /// Important consequences:
    ///   - you can only call methods the game already marked as network-callable, and
    ///   - some of them check who is asking and ignore everyone except the lobby host.
    /// </summary>
    public static class Net
    {
        /// <summary>Who receives an RPC.</summary>
        public enum To
        {
            /// <summary>Every player in the lobby, including you.</summary>
            All,
            /// <summary>Only the player who controls the object (for a character: that player).</summary>
            Owner,
            /// <summary>Only the lobby host.</summary>
            Host,
        }

        /// <summary>True while you are in a lobby or a run (offline runs count too).</summary>
        public static bool InRoom
        {
            get
            {
                try { return (bool)Reflect.GetStatic(GameTypes.PhotonNetwork, "InRoom"); }
                catch { return false; }
            }
        }

        /// <summary>True if you are the lobby host (Photon calls this the "master client").</summary>
        public static bool IsHost
        {
            get
            {
                try { return InRoom && (bool)Reflect.GetStatic(GameTypes.PhotonNetwork, "IsMasterClient"); }
                catch { return false; }
            }
        }

        public static void RequireRoom()
        {
            if (!InRoom) throw new ApiError(409, "You need to be in a lobby or a run for this.");
        }

        /// <summary>
        /// Send an RPC: ask other players' games to run <paramref name="method"/> on their copy of
        /// <paramref name="component"/> (for example a Character).
        /// </summary>
        public static void Rpc(object component, string method, To to, params object[] args)
        {
            // Check the method really exists in this version of the game, and send exactly as many
            // arguments as it declares. Photon silently drops RPCs with the wrong argument count.
            var parameters = Reflect.RpcParameters(component.GetType(), method)
                ?? throw new MissingGameMemberException(
                    $"The game's '{component.GetType().Name}' has no network method called '{method}'. PEAK may have been updated.");
            args = FitArguments(args, parameters);

            object view = Reflect.Get(component, "photonView");
            if (view == null) throw new ApiError(409, "That object is not networked right now.");

            if (to == To.Owner)
            {
                object owner = Reflect.Get(view, "Owner");
                if (owner != null)
                {
                    // PhotonView.RPC(string methodName, Player targetPlayer, params object[] parameters)
                    Reflect.Call(view, "RPC", method, owner, args);
                    return;
                }
                to = To.All; // no owner known (offline play): fall back to everyone, which is just us
            }

            // PhotonView.RPC(string methodName, RpcTarget target, params object[] parameters)
            object target = Enum.Parse(GameTypes.RpcTarget, to == To.Host ? "MasterClient" : "All");
            Reflect.Call(view, "RPC", method, target, args);
        }

        private static object[] FitArguments(object[] args, System.Reflection.ParameterInfo[] parameters)
        {
            if (args.Length == parameters.Length) return args;
            if (args.Length > parameters.Length) return args.Take(parameters.Length).ToArray();

            var fitted = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < args.Length) fitted[i] = args[i];
                else if (parameters[i].HasDefaultValue) fitted[i] = parameters[i].DefaultValue;
                else if (parameters[i].ParameterType.IsValueType) fitted[i] = Activator.CreateInstance(parameters[i].ParameterType);
            }
            return fitted;
        }

        /// <summary>
        /// Create a networked object from one of the game's prefabs (templates) so every player sees it.
        /// Items live under the "0_Items/" prefab folder.
        /// </summary>
        public static object Spawn(string prefabPath, Vector3 position)
        {
            RequireRoom();
            // PhotonNetwork.Instantiate(string prefabName, Vector3 position, Quaternion rotation, byte group = 0, object[] data = null)
            return Reflect.CallStatic(GameTypes.PhotonNetwork, "Instantiate", prefabPath, position, Quaternion.identity);
        }
    }
}
