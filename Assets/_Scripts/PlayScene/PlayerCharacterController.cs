using Fusion;
using SpellFlinger.Enum;
using SpellFlinger.Scriptables;
using SpellSlinger.Networking;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace SpellFlinger.PlayScene
{
    public class PlayerCharacterController : NetworkBehaviour
    {
        [SerializeField] private Transform _cameraEndTarget = null;
        [SerializeField] private Transform _cameraStartTarget = null;
        [SerializeField] private Transform _cameraAimTarget = null;
        [SerializeField] private NetworkCharacterController _networkController;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private int _respawnTime = 0;
        [SerializeField] private Transform _shootOrigin;
        [SerializeField] private PlayerStats _playerStats = null;
        [SerializeField] private GameObject _playerModel = null;
        [SerializeField] private Transform _modelLeftHand = null;
        [SerializeField] private Transform _modelRightHand = null;
        [SerializeField] private Animator _playerAnimator = null;
        [SerializeField] private float _doubleJumpDelay = 0f;
        private float _fireRate = 0;
        private PlayerAnimationController _playerAnimationController = null;
        private CameraController _cameraController = null;
        private Projectile _projectilePrefab = null;
        private PlayerAnimationState _playerAnimationState = PlayerAnimationState.Idle;
        private float _fireCooldown = 0;
        private IEnumerator _respawnCoroutine = null;
        private bool _resetPosition = false;
        private float _jumpTime = 0f;
        private bool _doubleJumpAvailable = false;
        private int _updatesSinceLastGrounded = 0;

        [Networked, OnChangedRender(nameof(RespawnTimeChanged))] public int RemainingRespawnTime { get; private set; }
        public PlayerStats PlayerStats => _playerStats;

        // initialize player character controller
        // also if host, initialize server
        public override void Spawned()
        {
            _playerAnimationController = new();
            if (HasInputAuthority) 
                InitializeClient();
            if (Runner.IsServer) 
                InitializeServer();
        }

        // setup camera and player controller
        private void InitializeClient()
        {
            FusionConnection.Instance.LocalCharacterController = this;
            _cameraController = CameraController.Instance;
            _cameraController.transform.parent = _cameraEndTarget;
            _cameraController.Init(_cameraStartTarget, _cameraEndTarget);
            _playerAnimationController.Init(ref _playerAnimationState, _playerAnimator);
        }

        // setup network controller
        private void InitializeServer()
        {
            _networkController.Teleport(SpawnLocationManager.Instance.GetRandomSpawnLocation());
            _playerAnimationController.Init(ref _playerAnimationState, _playerAnimator);
        }

        public void SetWeapon(WeaponDataScriptable.WeaponData data)
        {
            _projectilePrefab = data.WeaponPrefab;
            Instantiate(data.GlovePrefab, _modelLeftHand).transform.localPosition = data.GloveLocation;
            Instantiate(data.GlovePrefab, _modelRightHand).transform.localPosition = data.GloveLocation;
            _fireRate = data.FireRate;
        }

        public Vector3 GetShootDirection()
        {
            RaycastHit[] hits = Physics.RaycastAll(_cameraController.transform.position, _cameraController.transform.forward);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.CompareTag("Projectile")) 
                    continue;

                Vector3 shootDirection;
                if (Vector3.Dot(hit.point - _shootOrigin.position, transform.forward) >= 0)
                {
                    shootDirection = hit.point - _shootOrigin.position;
                    //Debug.Log("Proper shoot");
                }
                else
                {
                    shootDirection = _cameraAimTarget.position - _shootOrigin.position;
                    //Debug.Log("Bad shoot");
                }

                return shootDirection;
            }

            return _cameraAimTarget.position - _shootOrigin.position; ;
        }

        // spawn and fire the weapon projectile
        private void Shoot(Vector3 shootDirection)
        {
            if (Time.time < _fireCooldown) return;

            _fireCooldown = Time.time + _fireRate;
            _playerAnimationController.PlayShootAnimation(_playerAnimator);

            if (HasStateAuthority)
            {
                Projectile projectile = Runner.Spawn(_projectilePrefab, _shootOrigin.position, inputAuthority: Runner.LocalPlayer);
                projectile.Throw(shootDirection, _playerStats);
            }
        }

        // if spawning a player, reset their position
        // update player movement and shooting
        public override void FixedUpdateNetwork()
        {
            if (_resetPosition)
            {
                _networkController.enabled = true;
                _networkController.Teleport(SpawnLocationManager.Instance.GetRandomSpawnLocation());
                _resetPosition = false;
            }

            if (_playerStats.Health <= 0 || _characterController.enabled == false) 
                return;

            if (GetInput(out NetworkInputData data))
            {
                bool isGrounded = _characterController.isGrounded;
                if (isGrounded)
                {
                    _updatesSinceLastGrounded = 0;
                    _doubleJumpAvailable = true;
                }
                //This is done to avoid jittering with collider switching between grounded and not grounded between calls.
                else if (_updatesSinceLastGrounded < 3)
                {
                    isGrounded = true;
                    _updatesSinceLastGrounded++;
                }

                if (data.Buttons.IsSet(NetworkInputData.SHOOT)) 
                    Shoot(data.ShootTarget);

                /*
                 * U ovoj metodi potrebno je nadopuniti kod za kretanje. Komande smjera kretanja dobivaju se iz poslanog NetworkInputData podatka. 
                 * Sve naredbe kretanja odvijaju se preko poziva metoda pripadne instance klase NetworkCharacterController-a.
                 * Potrebno je pozvati metodu Move za pomicanje lika klase NetworkCharacterController.
                 * 
                 * Također je potrebno provjeriti je li dana naredba za skok. 
                 * Ako je, i ako je igrač prizemljen, potrebno je ažurirati stanje animacije PlayerAnimationController-a. 
                 * Također, ako je igrač prizemljen i ako je prošlo dovoljno vremena od prošlog skoka, potrebno je pozvati metodu za skok klase NetworkCharacterController. 
                 * Ako lik nije prizemljen, a postavljena je zastavica duplog skoka potrebno je maknuti zastavicu duplog skoka i obaviti poziv metode za skok. 
                 * Zastavicu duplog skoka je potrebno postaviti kada je lik prizemljen. Pri skoku je potrebno zabilježiti posljednje vrijeme kada je skok aktiviran. 
                 * 
                 * Kod metode Move klase NetworkCharacterController je izmjenjen za potrebe ovog projekta kako bi kretanje bolje odgovaralo korisničkom iskustvu. 
                 * Tu metodu kao i metodu AdjustVelocityToSlope možete proučiti i po želji izmijeniti. 
                 */
                
                // WASD movement
                _networkController.Move(data.Direction, _playerStats.IsSlowed, data.YRotation, isGrounded);
                
                // jumping
                if (data.Buttons.IsSet(NetworkInputData.JUMP))
                {
                    if (isGrounded && _updatesSinceLastGrounded < 3)
                    {
                        _networkController.Jump(true, false);
                        _jumpTime = Time.time;
                    }
                    else if (_doubleJumpAvailable)
                    {
                        _doubleJumpAvailable = false;
                        _networkController.Jump(true, true);
                    }
                }
                
                // data.Direction.x = left/right, data.Direction.y = forward/backward
                _playerAnimationController.AnimationUpdate(isGrounded, data.Direction.x, data.Direction.y,
                    ref _playerAnimationState, _playerAnimator, _playerModel.transform, transform);
            }
        }

        //This is called on server host, so HasStateAuthority is set to true
        public void PlayerKilled()
        {
            RemainingRespawnTime = _respawnTime;
            _respawnCoroutine = RespawnCoroutine();
            StartCoroutine(_respawnCoroutine);
            RPC_PlayerKilled();
        }

        private IEnumerator RespawnCoroutine()
        {
            /*
             * U ovoj metodi potrebno je zamijeniti liniju yield return null potrebnim kodom.
             * Prvo je potrebno pozvati metodu udaljene procedure za gašenje 
             * controllera lika na klijentu, jer se ova metoda izvodi na računalu domaćina.
             * 
             * Potom je potrebno postaviti stanje animacije smrti PlayerAnimationController-a
             * i započeti odbrojavanje. Svake sekunde je potrebno smanjiti vrijednost umrežene 
             * varijable RemainingRespawnTime za jedan, dok ne dođe do 0.
             * Kada postigne vrijednost 0 potrebno je pozvati metodu pripadne instance klase PlayerStats za resetiranje životnih
             * bodova, pozvati metodu udaljenog poziva za ponovnu aktivaciju conrollera na klijentu, 
             * postaviti zastavicu za ponovno postavljanje nasumične početne pozicije i postaviti 
             * stanje animacije življenja PlayerAnimationController-a.
             */

            // remote procedure call for shutting down client player controller
            RPC_DisableController();
            // death animation state
            _playerAnimationController.SetDeadState(ref _playerAnimationState, _playerAnimator);
            // respawn countdown
            while (RemainingRespawnTime > 0)
            {
                yield return new WaitForSeconds(1);
                RemainingRespawnTime--;
            }
            
            // when countdown reaches zero:
            // reset lifepoints
            _playerStats.ResetHealth();
            // reset player controller on client
            RPC_EnableController();
            // set random spawn location flag
            _resetPosition = true;
            // set revive animation in PlayerAnimationController 
            _playerAnimationController.SetAliveState(ref _playerAnimationState, _playerAnimator);
        }

        // start death animation, show deathscreen and lock the camera
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, HostMode = RpcHostMode.SourceIsServer)]
        private void RPC_PlayerKilled()
        {
            /*
             * U ovoj metodi potrebno je postaviti stanje animacije smrti PlayerAnimationControllera,
             * prikazati ekran smrti preko singleton instance UiManager skripte, te onesposobiti i zaključati kameru.
             */
            
            // death animation
            _playerAnimationController.SetDeadState(ref _playerAnimationState, _playerAnimator);
            // show death screen
            UiManager.Instance.ShowPlayerDeathScreen(_respawnTime);
            // disable and lock camera
            _cameraController.CameraEnabled = false;
            _cameraController.CameraLock = true;
        }

        // countdown from death until player respawns
        private void RespawnTimeChanged()
        {
            if (!HasInputAuthority) 
                return;

            UiManager.Instance.UpdateDeathTimer(RemainingRespawnTime);

            if (RemainingRespawnTime > 0) 
                return;

            UiManager.Instance.HideDeathTimer();
            _playerAnimationController.SetAliveState(ref _playerAnimationState, _playerAnimator);
            if (GameManager.Instance.RemainingGameEndTime > 0) 
                return;
            _cameraController.CameraLock = false;
            _cameraController.CameraEnabled = true;
        }

        // disable player character controller and network controller
        [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_DisableController()
        {
            _networkController.enabled = false;
            _characterController.enabled = false;
        }

        // enable player character controller and network controller
        [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_EnableController()
        {
            _networkController.enabled = true;
            _characterController.enabled = true;
        }

        // notify all players the game has ended
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_GameEnd() => GameEnd();

        public void GameEnd()
        {
            UiManager.Instance.HideDeathTimer();
            UiManager.Instance.ShowEndGameScreen();
            _cameraController.CameraLock = true;
            _cameraController.CameraEnabled = false;
        }

        public void StopRespawnCoroutine()
        {
            if (_respawnCoroutine == null) 
                return;

            StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = null;
            RemainingRespawnTime = 0;
        }

        // enable player animation and camera controllers
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_GameStart()
        {
            UiManager.Instance.HideEndGameScreen();
            _playerAnimationController.SetAliveState(ref _playerAnimationState, _playerAnimator);
            _cameraController.CameraLock = false;
            _cameraController.CameraEnabled = true;
        }

        // spawn the player
        public void SetGameStartPosition()
        {
            _resetPosition = true;
            _playerAnimationController.SetAliveState(ref _playerAnimationState, _playerAnimator);
        }
    }
}