using SpellFlinger.Enum;
using SpellFlinger.Scriptables;
using SpellSlinger.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace SpellFlinger.LoginScene
{
    public class RoomCreationView : MonoBehaviour
    {
        [SerializeField] private Toggle _teamDeathMatchToggle = null;
        [SerializeField] private Toggle _deathMatchToggle = null;
        [SerializeField] private TMP_InputField _roomNameInput = null;
        [SerializeField] private Button _returnButton = null;
        [SerializeField] private Button _createRoomButton = null;
        [SerializeField] private LevelSelectionToggle _levelSelectionTogglePrefab = null;
        [SerializeField] private ToggleGroup _levelSelectionContainer = null;
        [SerializeField] private GameObject _sessionView = null;
        private LevelType _selectedLevelType;

        private void Awake()
        {
            /*
             * Metodu je potrebno nadopuniti s kodom za stvaranje Toggle objekata za izbor scene.
             * Popis podataka o scenama se može dobiti iz instance LevelDataScriptable klase.
             * Stvaranje i inicijalizaciju objekata se provodi na sličan način kao stvaranje
             * Toggle objekata za izbor oružja u Awake metodi SessionView klase.
             * 
             * Nakon toga je potrebno inicijalizirati callback Return gumba da na pritisak gasi 
             * izbornik stvaranja sobe i otvara izbornik odabira otvorenih soba.
             * Također potrebno je inicijalizirati callback Create Room gumba da na pritisak
             * poziva metodu za stvaranje sobe.
             */

            // stvaranje toggle objekata
            foreach (var data in LevelDataScriptable.Instance.Levels)
            {
                Debug.Log(data.LevelType.ToString());
                LevelSelectionToggle levelToggle = Instantiate(_levelSelectionTogglePrefab, _levelSelectionContainer.transform);
                levelToggle.ShowLevel(data.LevelType, _levelSelectionContainer, data.LevelImage, (levelType) => LevelDataScriptable.SetSelectedLevelType(levelType));
            }

            // inicijalizacija callback Return gumba
            _returnButton.onClick.AddListener(() =>
            {
                _sessionView.SetActive(true);
                gameObject.SetActive(false);
            });

            // inicijalizacija callback Create Room gumba
            _createRoomButton.onClick.AddListener(CreateRoom);
        }

        private void CreateRoom()
        {
            /*  
             *  Potrebno je pozvati metodu instance FusionConnection za stvaranje sobe, te joj poslati potrebne parametre.
             */

            Debug.Log("CreateRoom button clicked");

            GameModeType gameMode;
            if (_teamDeathMatchToggle.isOn)
                gameMode = GameModeType.TDM;
            else
                gameMode = GameModeType.DM;

            LevelType level = LevelDataScriptable.Instance.GetSelectedLevelType();
            FusionConnection.Instance.CreateSession(_roomNameInput.text, gameMode, level);
        }
    }
}
