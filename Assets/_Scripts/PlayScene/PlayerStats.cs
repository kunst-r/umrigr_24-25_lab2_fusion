using Fusion;
using SpellFlinger.Enum;
using SpellFlinger.Scriptables;
using SpellSlinger.Networking;
using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace SpellFlinger.PlayScene
{
    public class PlayerStats : NetworkBehaviour
    {
        [SerializeField] private TextMeshPro _playerNameText = null;
        [SerializeField] private Slider _healthBar = null;
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private GameObject _playerModel = null;
        private bool _init = false;
        private PlayerScoreboardData _playerScoreboardData = null;
        private PlayerCharacterController _playerCharacterController = null;

        public Action OnSpawnedCallback;

        public PlayerCharacterController PlayerCharacterController => _playerCharacterController;
        public bool IsSlowed => SlowDuration > 0.001f;

        [Networked, OnChangedRender(nameof(PlayerNameChanged))] public NetworkString<_32> PlayerName { get; set; }
        [Networked, OnChangedRender(nameof(TeamChanged))] public TeamType Team { get; set; }
        [Networked, OnChangedRender(nameof(WeaponChanged))] public WeaponType SelectedWeapon { get; set; }
        [Networked, OnChangedRender(nameof(HealthChanged))] public int Health { get; set; }
        [Networked, OnChangedRender(nameof(KillsChanged))] public int Kills { get; set; }
        [Networked, OnChangedRender(nameof(DeathsChanged))] public int Deaths { get; set; }
        [Networked] public float SlowDuration { get; set; }

        // update _playerNameText transform
        private void LateUpdate()
        {
            _playerNameText.transform.LookAt(CameraController.Instance.transform);
            _playerNameText.transform.Rotate(0, 180, 0);
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority && SlowDuration > 0.001f) 
                SlowDuration -= Runner.DeltaTime;
        }

        // initialize player data, UI, join to team, etc.
        public override void Spawned()
        {
            Debug.LogFormat("PlayerStats::Spawned");
            _playerCharacterController = GetComponent<PlayerCharacterController>();
            _playerScoreboardData = UiManager.Instance.CreatePlayerScoarboardData();
            PlayerManager.Instance.RegisterPlayer(this);
            
            if (HasInputAuthority)
            {
                _playerNameText.gameObject.SetActive(false);
                RPC_InitializeData(FusionConnection.Instance.PlayerName, WeaponDataScriptable.SelectedWeaponType);
                if (FusionConnection.GameModeType == GameModeType.DM) 
                    UiManager.Instance.ShowSoloScore();
                else if (FusionConnection.GameModeType == GameModeType.TDM) 
                    UiManager.Instance.ShowTeamScore();
            }

            if (PlayerName.Value != default) 
                PlayerNameChanged();
            if (Team != default) 
                TeamChanged();
            if (SelectedWeapon != default) 
                WeaponChanged();
            if (Health != default) 
                HealthChanged();
            if (Kills != default) 
                KillsChanged();
            if (Deaths != default) 
                DeathsChanged();

            if (!HasInputAuthority && FusionConnection.GameModeType == GameModeType.DM) 
                PlayerManager.Instance.SetPlayerColor(this);
        }

        // initialize player data via RPC
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_InitializeData(string playerName, WeaponType selectedWeapon, RpcInfo info = default)
        {
            Kills = 0;
            Deaths = 0;
            Health = _maxHealth;
            PlayerName = playerName;
            SelectedWeapon = selectedWeapon;
        }

        // propagate player name change to UI
        private void PlayerNameChanged()
        {
            //Debug.LogFormat("PlayerStats::PlayerNameChanged");
            _playerNameText.text = PlayerName.ToString();
            _playerScoreboardData.Init(PlayerName.ToString());
        }

        // propagate player team change to PlayerManager singleton and scoreboard UI 
        private void TeamChanged()
        {
            //Debug.LogFormat("PlayerStats::TeamChanged");
            if (HasInputAuthority) 
                PlayerManager.Instance.SetFriendlyTeam(Team);
            else 
                PlayerManager.Instance.SetPlayerColor(this);
            _playerScoreboardData.SetTeamType(Team);
        }

        // propagate weapon change
        private void WeaponChanged()
        {
            //Debug.LogFormat("PlayerStats::WeaponChanged");
            var weaponData = WeaponDataScriptable.Instance.GetWeaponData(SelectedWeapon);
            _playerCharacterController.SetWeapon(weaponData);
        }

        // change UI, either for this or other players
        private void HealthChanged()
        {
            /*
             * Ako su promjenjeni životni bodovi vlastitog igrača potrebno je ažurirati prikaz 
             * života na dnu ekrana pozivom metode instance singleton klase UiManager,
             * a ako su promjenjeni životni bodovi nekog drugog igrača potrebno je ažurirati
             * pripadni prikaz životnih bodova koji se nalazi iznad glave igrača.
             */
            
            //Debug.LogFormat("current health: {0}", Health);

            if (HasInputAuthority)
            {
                UiManager.Instance.UpdateHealthBar(Health, (float)Health / _maxHealth);
            }
            else
            {
                _healthBar.value = (float)Health / _maxHealth;
            }
            
        }

        // change UI when the player gets a kill
        private void KillsChanged()
        {
            /*
             * U ovoj metodi je potrebno ažurirati prikaz instance PlayerScoreboardData ovog igrača.
             * Također je potrebno ažurirati prikaz bodova na vrhu ekrana pozivom pripadnih metoda instance 
             * singleton klase UiManager ovisno je li u pitanju timska igra ili svatko protiv svakoga.
             */
            
            // update scoreboard
            _playerScoreboardData.UpdateScore(Kills, Deaths);
            // update UI
            if (Team == TeamType.None)
            {
                UiManager.Instance.UpdateSoloScore(Kills);
            }
            else
            {
                UiManager.Instance.UpdateTeamScore();
            }
        }

        // propagate the number of player deaths to scoreboard
        private void DeathsChanged()
        {
            //Debug.LogFormat("PlayerStats::DeathsChanged");
            _playerScoreboardData.UpdateScore(Kills, Deaths);
        }
            
        public void DealDamage(int damage, PlayerStats attacker)
        {
            /*
             * U ovoj metodi je potrebno smanjiti živote igrača za štetu.
             * U slučaju da su životni bodovi prije poziva bili veći od 0, 
             * a nakon smanjivanja padnu na nula, potrebno je povećati broj
             * deathova igrača, obavijestiti PlayerCharacterController o smrti igrača,
             * te povećati broj killova igrača koji je napravio štetu, također u 
             * slučaju načina igre svatko protiv svakoga potrebno je povećati broj 
             * bodova tima igrača kojemu se dodaje bod.
             * 
             * Ako je broj bodova igrača u načinu svatko protiv svakoga ili broj bodova
             * tima u načinu timske igre ima isti iznos kao broj bodova potrebnih za pobjedu
             * iz klase GameManager potrebno je pozvati pripadnu metodu za kraj igre singleton 
             * instance klase GameManager.
             */

            if (Health - damage > 0)
            {
                Health -= damage;
            }
            else
            {
                Health = 0;
                
                // handle death logic
                Debug.LogFormat("DEAAAAATH");
                Deaths++;
                _playerCharacterController.PlayerKilled();
                attacker.Kills++;
                // increase team score in TDM
                if (FusionConnection.GameModeType == GameModeType.TDM)
                {
                    GameManager.Instance.AddTeamKill(attacker.Team);
                    UiManager.Instance.UpdateTeamScore();
                }
                // check if game over
                if (FusionConnection.GameModeType == GameModeType.DM && attacker.Kills >= GameManager.Instance.SoloKillsForWin)
                {
                    GameManager.Instance.GameEnd((string)attacker.PlayerName);
                }
                else if (FusionConnection.GameModeType == GameModeType.TDM && GameManager.Instance.GetTeamKills(attacker.Team) >= GameManager.Instance.TeamKillsForWin)
                {
                    GameManager.Instance.GameEnd(attacker.Team);
                }
            }
        }

        public void Heal(int healAmount)
        {
            Debug.LogFormat("PlayerStats::Heal");
            Health += healAmount;
            if (Health > _maxHealth) 
                Health  = _maxHealth;
        }

        public void ApplySlow(float duration)
        {
            /*
             * U ovoj metodi je potrebno postaviti trajanje usporenog kretanja nakon pogotka ledenim projektilom.
             */
            Debug.LogFormat("PlayerStats::ApplySlow");
            SlowDuration = duration;
        }

        public void ResetHealth()
        {
            Debug.LogFormat("PlayerStats::ResetHealth");
            Health = _maxHealth;
        }
            

        public void SetTeamMaterial(Material material, Color color)
        {
            Debug.LogFormat("PlayerStats::SetTeamMaterial");
            _playerNameText.color = color;
            if (!HasInputAuthority)
            {
                _playerModel.GetComponent<Renderer>().material = material;
            }
        }

        public void ResetGameInfo()
        {
            Debug.LogFormat("PlayerStats::ResetGameInfo");
            Kills = 0;
            Deaths = 0;
            Health = _maxHealth;
        }

        // destroy scoreboard and unregister player from PlayerManager singleton
        private void OnDestroy()
        {
            Debug.LogFormat("PlayerStats::OnDestroy");
            if (_playerScoreboardData != null && !_playerScoreboardData.gameObject.IsDestroyed()) 
                Destroy(_playerScoreboardData.gameObject);
            PlayerManager.Instance?.UnregisterPlayer(this);
        }
    }
}