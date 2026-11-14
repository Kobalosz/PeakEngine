// A pretend version of PEAK's own classes. Names, visibility and method shapes mirror what the
// real game exposes (as documented by open-source PEAK mods), including the awkward parts:
// private members, optional parameters, and network methods that only obey the host.
using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class Character : MonoBehaviourPun
{
    public static Character localCharacter;
    public static List<Character> AllCharacters = new List<Character>();

    public class CharacterRefs
    {
        public CharacterAfflictions afflictions;
        public CharacterItems items;
    }

    public bool isBot;
    public string characterName;
    public CharacterData data = new CharacterData();
    public CharacterRefs refs = new CharacterRefs();
    public bool infiniteStam;
    public Vector3 position;

    public Vector3 Center => position;
    public bool IsLocal => this == localCharacter;

    public float GetMaxStamina() => Mathf.Max(1f - refs.afflictions.statusSum, 0f);

    public void SetExtraStamina(float amount) => data.extraStamina = Mathf.Clamp(amount, 0f, 1f);

    [PunRPC]
    public void MoraleBoost(float staminaAdd, int scoutCount) => data.extraStamina = Mathf.Clamp01(data.extraStamina + staminaAdd);

    [PunRPC]
    internal void RPCA_ReviveAtPosition(Vector3 position, bool applyStatus, int statueSegment)
    {
        data.dead = false;
        data.passedOut = false;
        data.fullyPassedOut = false;
        this.position = position;
    }

    [PunRPC]
    public void WarpPlayerRPC(Vector3 position, bool poof) => this.position = position;
}

public class CharacterData
{
    public float currentStamina = 1f;
    public float extraStamina;
    public bool dead;
    public bool passedOut;
    public bool fullyPassedOut;
    public Vector3 lookDirection_Flat = new Vector3(0f, 0f, 1f);
    internal bool isInvincible;

    public bool IsInvincibleForTests => isInvincible;
}

public class CharacterAfflictions : MonoBehaviourPun
{
    public enum STATUSTYPE { Injury, Hunger, Cold, Poison, Crab, Curse, Drowsy, Weight, Hot, Thorns, Spores, Web }

    public static int NumStatusTypes => Enum.GetNames(typeof(STATUSTYPE)).Length;

    public Character character;
    public float[] currentStatuses = new float[NumStatusTypes];
    public int pushes;

    public float statusSum
    {
        get { float sum = 0f; foreach (float s in currentStatuses) sum += s; return sum; }
    }

    public float GetCurrentStatus(STATUSTYPE statusType) => currentStatuses[(int)statusType];

    public void SetStatus(STATUSTYPE statusType, float amount, bool pushStatus = true)
    {
        if (!character.IsLocal) return; // statuses are owned by each player's own game
        currentStatuses[(int)statusType] = amount;
        if (pushStatus) PushStatuses();
    }

    public void PushStatuses(Player specificPlayer = null) => pushes++;

    [PunRPC]
    private void RPC_ApplyStatusesFromFloatArray(float[] deltas, PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient) return; // only the host is obeyed
        if (deltas.Length != currentStatuses.Length) return;
        for (int i = 0; i < deltas.Length; i++)
            currentStatuses[i] = Mathf.Clamp01(currentStatuses[i] + deltas[i]);
    }
}

public class CharacterItems : MonoBehaviourPun
{
    public readonly List<string> inHand = new List<string>();

    internal void SpawnItemInHand(string itemName) => inHand.Add(itemName);
}

public class SingletonAsset<T> where T : class
{
    public static T Instance { get; set; }
}

public class ItemDatabase : SingletonAsset<ItemDatabase>
{
    public Dictionary<ushort, Item> itemLookup = new Dictionary<ushort, Item>();
}

public class Item
{
    [Serializable]
    public class ItemUIData { public string itemName; }

    public string name;
    public ushort itemID;
    public ItemUIData UIData = new ItemUIData();
}

/// <summary>Builds a small pretend lobby: you plus two friends, and a handful of items.</summary>
public static class FakeWorld
{
    public static void Create(bool youAreHost)
    {
        var you = new Player { NickName = "You", ActorNumber = 1 };
        var bing = new Player { NickName = "Bing", ActorNumber = 2 };
        var bong = new Player { NickName = "Bong <b>", ActorNumber = 3 };

        PhotonNetwork.LocalPlayer = you;
        PhotonNetwork.MasterClient = youAreHost ? you : bing;
        PhotonNetwork.InRoom = true;

        Character.AllCharacters.Clear();
        Character.localCharacter = Spawn(you, 1001, new Vector3(0f, 10f, 0f));
        var friend = Spawn(bing, 2001, new Vector3(5f, 12f, 3f));
        friend.refs.afflictions.currentStatuses[(int)CharacterAfflictions.STATUSTYPE.Hunger] = 0.4f;
        friend.refs.afflictions.currentStatuses[(int)CharacterAfflictions.STATUSTYPE.Cold] = 0.2f;
        var deadFriend = Spawn(bong, 3001, new Vector3(-20f, 2f, 8f));
        deadFriend.data.dead = true;
        var bot = Spawn(new Player { NickName = "Scoutmaster" }, 9001, Vector3.zero);
        bot.isBot = true;

        var database = new ItemDatabase();
        ushort id = 1;
        foreach (var (prefab, display) in new[]
        {
            ("Coconut", "Coconut"), ("RopeSpool", "Rope Spool"), ("Bandages", "Bandages"), ("Lantern", "Lantern"),
            ("BingBong", "Bing Bong"), ("Flare", "Flare"), ("EnergyDrink", "Energy Drink"), ("Piton", "Piton"),
        })
        {
            database.itemLookup[id] = new Item { name = prefab, itemID = id, UIData = { itemName = display } };
            id++;
        }
        ItemDatabase.Instance = database;
    }

    private static Character Spawn(Player owner, int viewId, Vector3 position)
    {
        var view = new PhotonView { ViewID = viewId, Owner = owner };
        var character = new Character { characterName = owner.NickName, position = position };
        var afflictions = new CharacterAfflictions { character = character };
        var items = new CharacterItems();
        character.refs.afflictions = afflictions;
        character.refs.items = items;
        foreach (MonoBehaviourPun component in new MonoBehaviourPun[] { character, afflictions, items })
        {
            component.photonView = view;
            view.Components.Add(component);
        }
        Character.AllCharacters.Add(character);
        return character;
    }

    /// <summary>What the real game does every frame, reduced to the bits the cheats fight against.</summary>
    public static void Simulate(float seconds)
    {
        var me = Character.localCharacter;
        if (me == null) return;
        if (!me.infiniteStam) me.data.currentStamina = Mathf.Max(0f, me.data.currentStamina - 0.02f * seconds);
        var statuses = me.refs.afflictions.currentStatuses;
        int hunger = (int)CharacterAfflictions.STATUSTYPE.Hunger;
        statuses[hunger] = Mathf.Min(1f, statuses[hunger] + 0.01f * seconds);
    }
}
