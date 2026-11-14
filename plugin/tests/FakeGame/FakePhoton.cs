// A pretend version of Photon (PEAK's networking library), with the same class and method
// names as the real one. RPCs are "delivered" immediately on this machine, as if every other
// player's game received them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Photon.Realtime;
using UnityEngine;

namespace Photon.Realtime
{
    public class Player
    {
        public string NickName;
        public int ActorNumber;
        public bool IsMasterClient => Photon.Pun.PhotonNetwork.MasterClient == this;
    }
}

namespace Photon.Pun
{
    public enum RpcTarget { All, Others, MasterClient, AllBuffered, OthersBuffered, AllViaServer, AllBufferedViaServer }

    public struct PhotonMessageInfo { public Player Sender; }

    [AttributeUsage(AttributeTargets.Method)]
    public class PunRPC : Attribute { }

    public class MonoBehaviourPun
    {
        public PhotonView photonView { get; internal set; }
    }

    public class PhotonView
    {
        public int ViewID;
        public Player Owner;
        public bool IsMine => Owner == PhotonNetwork.LocalPlayer;
        public readonly List<object> Components = new List<object>();

        public void RPC(string methodName, RpcTarget target, params object[] parameters) =>
            Deliver(methodName, target.ToString(), parameters);

        public void RPC(string methodName, Player targetPlayer, params object[] parameters) =>
            Deliver(methodName, "player:" + targetPlayer.NickName, parameters);

        private void Deliver(string methodName, string target, object[] parameters)
        {
            PhotonNetwork.RpcLog.Add($"{methodName} -> {target} ({parameters.Length} args)");

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (object component in Components)
            {
                var method = component.GetType().GetMethods(flags)
                    .FirstOrDefault(m => m.Name == methodName && m.GetCustomAttribute<PunRPC>() != null);
                if (method == null) continue;

                // Like real Photon: the argument count must match exactly (not counting PhotonMessageInfo).
                var declared = method.GetParameters();
                bool wantsInfo = declared.Length > 0 && declared[declared.Length - 1].ParameterType == typeof(PhotonMessageInfo);
                if (declared.Length - (wantsInfo ? 1 : 0) != parameters.Length)
                    throw new InvalidOperationException($"RPC {methodName}: wrong number of arguments ({parameters.Length}).");

                var args = parameters.ToList();
                if (wantsInfo) args.Add(new PhotonMessageInfo { Sender = PhotonNetwork.LocalPlayer });
                method.Invoke(component, args.ToArray());
                return;
            }
            throw new InvalidOperationException($"RPC {methodName}: no [PunRPC] method with that name on view {ViewID}.");
        }
    }

    public static class PhotonNetwork
    {
        public static bool InRoom;
        public static Player LocalPlayer;
        public static Player MasterClient;
        public static bool IsMasterClient => LocalPlayer == MasterClient;

        /// <summary>Test bookkeeping: what was spawned and which RPCs were sent.</summary>
        public static readonly List<string> Spawned = new List<string>();
        public static readonly List<string> RpcLog = new List<string>();

        public static GameObject Instantiate(string prefabName, Vector3 position, Quaternion rotation, byte group = 0, object[] data = null)
        {
            if (!InRoom) throw new InvalidOperationException("Not in a room.");
            Spawned.Add(prefabName);
            return null;
        }
    }
}
