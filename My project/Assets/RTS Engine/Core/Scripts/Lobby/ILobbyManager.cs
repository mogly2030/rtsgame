using System;
using System.Collections.Generic;

using RTSEngine.Event;
using RTSEngine.Service;
using RTSEngine.Lobby.Service;
using RTSEngine.Lobby.Utilities;
using RTSEngine.Lobby.UI;

namespace RTSEngine.Lobby
{
    public interface ILobbyManagerBase : IServicePublisher<ILobbyService> { }

    public interface ILobbyManager<T> : IMonoBehaviour, ILobbyManagerBase where T : ILobbyFactionSlot
    {
        string GameCode { get; }

        IReadOnlyList<T> FactionSlots { get; }
        int FactionSlotCount { get; }
        T GetFactionSlot(int factionSlotID);
        int GetFactionSlotID(T slot);
        T LocalFactionSlot { get; }

        IReadOnlyList<LobbyMapData> Maps { get; }
        LobbyMapData CurrentMap { get; }
        LobbyMapData GetMap(int mapID);

        ColorSelector FactionColorSelector { get; }

        DefeatConditionDropdownSelector DefeatConditionSelector { get; }
        TimeModifierDropdownSelector TimeModifierSelector { get; }
        ResourceInputDropdownSelector InitialResourcesSelector { get; }

        LobbyGameData CurrentLobbyGameData { get; }

        bool IsStartingLobby { get; }

        event CustomEventHandler<T, EventArgs> FactionSlotAdded;
        event CustomEventHandler<T, EventArgs> FactionSlotRemoved;

        event CustomEventHandler<LobbyGameData, EventArgs> LobbyGameDataUpdated;

        bool IsLobbyGameDataMaster();

        void AddFactionSlot(T newSlot);
        bool CanRemoveFactionSlot(T slot);

        void StartLobby();
        void LeaveLobby();
        bool InterruptStartLobby();

        T GetHostFactionSlot();
    }
}
