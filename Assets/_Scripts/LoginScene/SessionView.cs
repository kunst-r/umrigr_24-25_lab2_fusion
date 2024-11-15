﻿using Fusion;
using SpellFlinger.Enum;
using SpellFlinger.Scriptables;
using SpellSlinger.Networking;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace SpellFlinger.LoginScene
{
    public class SessionView : Singleton<SessionView>
    {
        [SerializeField] private Button _createRoomButton = null;
        [SerializeField] private Button _refreshButton = null;
        [SerializeField] private Button _joinButton = null;
        [SerializeField] private GameObject _roomCreationView = null;
        [SerializeField] private SessionDataView _sessionDataViewPrefab = null;
        [SerializeField] private ToggleGroup _sessionListContainer = null;
        [SerializeField] private WeaponSelectionToggle _weaponSelectionTogglePrefab = null;
        [SerializeField] private ToggleGroup _weaponSelectionContainer = null;
        private (string, GameModeType, LevelType) _sessionData;
        private List<SessionDataView> _sessions = new List<SessionDataView>();

        private void Awake()
        {
            base.Awake();
            _createRoomButton.onClick.AddListener(() =>
            {
                _roomCreationView.SetActive(true);
                gameObject.SetActive(false);
            });

            _joinButton.interactable = false;

            foreach (var data in WeaponDataScriptable.Instance.Weapons)
            {
                WeaponSelectionToggle weaponToggle = Instantiate(_weaponSelectionTogglePrefab, _weaponSelectionContainer.transform);
                weaponToggle.ShowWeapon(data.WeaponType, _weaponSelectionContainer, data.WeaponImage, (weaponType) => WeaponDataScriptable.SetSelectedWeaponType(weaponType));
            }

            UpdateSessionList();
            _refreshButton.onClick.AddListener(UpdateSessionList);
            _joinButton.onClick.AddListener(() => FusionConnection.Instance.JoinSession(_sessionData.Item1, _sessionData.Item2));
        }

        public void UpdateSessionList()
        {
            /* 
             * U ovoj metodi potrebno je očistiti lokalnu listu prikaza sesija i uništiti njihove game objekte.
             * Potom koristeći listu sesija koja se dohvaća iz Singleton instance klase FusionConnection je potrebno osvježiti listu.
             * Za svaku postojeću sesiju potrebno je stvoriti novu instancu SessionDataView prefab-a, pozvati njenu metodu za prikazivanje
             * i dodati u lokalnu listu.
             * Za roditelja tih objekata potrebno je postaviti lokalnu referencu na kontejner sesija. 
             * Korisnik u sceni može pritisnuti instance SessionDataView objekata, te ih time odabrati za pridruživanje pritiskom na tipku Join.
             * Stvaranje objekata se provodi na sličan način kao stvaranje WeaponSelectionToggle objekata iz metode Awake.
             */

            // destroy old scrollbox SessionDataView objects and clear the _sessions list
            foreach (Transform child in _sessionListContainer.transform)
            {
                Destroy(child.gameObject);
            }
            _sessions.Clear();

            // update the _sessions list and the scrollbox with active sessions
            foreach (SessionInfo data in FusionConnection.Instance.Sessions)
            {
                SessionDataView sessionData = Instantiate(_sessionDataViewPrefab, _sessionListContainer.transform);

                sessionData.ShowSession(sessionName: data.Name,
                    playerCount: data.PlayerCount,
                    maxPlayerCount: data.MaxPlayers,
                    levelType: (LevelType)data.Properties["Level"].PropertyValue,
                    gameModeType: (GameModeType)data.Properties["GameMode"].PropertyValue,
                    onToggle: (isOn, sessionData) =>
                    {
                        /*
                        if (isOn)
                        {
                            _sessionData.Item1 = data.Name;
                            _sessionData.Item2 = (GameModeType)data.Properties["GameMode"].PropertyValue;
                            _sessionData.Item3 = (LevelType)data.Properties["Level"].PropertyValue;
                            _joinButton.interactable = true;
                        }
                        else
                            _joinButton.interactable = false;
                        */
                        SessionOnToggle(isOn, sessionData);
                    },
                    toggleGroup: _sessionListContainer);
                _sessions.Add(sessionData);
            }
        }

        private void SessionOnToggle(bool isOn, (string, GameModeType, LevelType) sessionData)
        {
            if (isOn)
            {
                _sessionData = sessionData;
                Debug.LogFormat("SessionView::SessionOnToggle");
                Debug.LogFormat("session name: {0}, game mode: {1}, level type: {2}",
                    sessionData.Item1, sessionData.Item2, sessionData.Item3);
                _joinButton.interactable = true;
            }
            else if (sessionData == _sessionData)
            {
                _joinButton.interactable = false;
            }
        }
    }
}
