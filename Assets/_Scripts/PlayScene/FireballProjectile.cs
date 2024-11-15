using Fusion;
using SpellFlinger.Enum;
using SpellSlinger.Networking;
using System.Linq;
using UnityEngine;

namespace SpellFlinger.PlayScene
{
    public class FireballProjectile : Projectile
    {
        [SerializeField] private float _range = 0f;
        [SerializeField] private float _explosionRange = 0f;
        [SerializeField] private float _explosionDuration = 0f;
        [SerializeField] private GameObject _projectileEffect = null;
        [SerializeField] private GameObject _explosionEffect = null;
        private bool _exploded = false;
        private PlayerStats _hitPlayer = null;

        public override void Throw(Vector3 direction, PlayerStats ownerPlayerStats)
        {
            Direction = direction.normalized * _movementSpeed;
            OwnerPlayerStats = ownerPlayerStats;
            transform.rotation = Quaternion.FromToRotation(transform.forward, Direction.normalized);
        }

        public override void FixedUpdateNetwork()
        {
            if (_exploded) 
                return;

            transform.position += Direction * Runner.DeltaTime;

            if (!HasStateAuthority) 
                return;

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, _range);

            foreach (Collider hitCollider in hitColliders)
            {
                if (!hitCollider.CompareTag("Player")) 
                    continue;

                PlayerStats player = hitCollider.GetComponent<PlayerStats>();

                // player can't hit himself
                if (player.Object.InputAuthority == OwnerPlayerStats.Object.InputAuthority) 
                    continue;
                // player can't hit teammates
                if (FusionConnection.GameModeType == GameModeType.TDM && player.Team == OwnerPlayerStats.Team)
                    continue;

                Debug.LogFormat("dealing {0} damage to player {1}", _damage, player.PlayerName);
                player.DealDamage(_damage, OwnerPlayerStats);
                _hitPlayer = player;
                Debug.LogFormat("_hitPlayer.PlayerName: {0}", _hitPlayer.PlayerName);
                Explode();

                break;
            }
            
            if (!_exploded && hitColliders.Any((hitCollider) => hitCollider.CompareTag("Ground"))) 
                Explode();
        }

        private void Explode()
        {
            Debug.Log("Exploded");
            _exploded = true;
            /*
             * U ovoj metodi je potrebno ugasiti vizualni efekt projektila i upaliti vizualni efekt eksplzije.
             * To je potrebno učiniti i na udaljenim klijentima putem metode ExplodeEffectRpc. 
             * Nakon toga je potrebno napraviti detekciju pogotka. Detekcija pogotka se radi na sličan način
             * kao u metodi FixedUpdateNetwork, no pazite da ne napravite štetu igraču kojega je projektil već
             * pogodio u toj metodi. 
             * Nakon toga je potrebno uništiti objekt, no tek nakon što završi trajanje eksplozije.
             */
            
            // visual effects change
            _projectileEffect.SetActive(false);
            _explosionEffect.SetActive(true);
            RPC_ExplodeEffect();
            
            // hit detection
            // wtf se opce tu trazi od mene => valjda explosion AoE damage
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, _explosionRange);
            foreach (Collider hitCollider in hitColliders)
            {
                if (!hitCollider.CompareTag("Player")) 
                    continue;

                PlayerStats player = hitCollider.GetComponent<PlayerStats>();

                // player can't hit himself => optional?
                // idk, nemam prijatelja da isprobam power scale testing :(
                if (player.Object.InputAuthority == OwnerPlayerStats.Object.InputAuthority) 
                    continue;
                // player can't hit teammates
                if (FusionConnection.GameModeType == GameModeType.TDM && player.Team == OwnerPlayerStats.Team)
                    continue;
                // can't hit the same player twice
                if (player == _hitPlayer)
                    continue;
                
                player.DealDamage(_damage, OwnerPlayerStats);
            }
            
            // destroy object after explosion (fireball explosion animation length = 0.6s)
            Invoke(nameof(DestroyProjectile), _explosionDuration);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
        public void RPC_ExplodeEffect()
        {
            /*
             * U ovoj metodi je potrebno ugasiti vizualni efekt projektila i upaliti vizualni efekt eksplozije za udaljene klijente.
             */
            _projectileEffect.SetActive(false);
            _explosionEffect.SetActive(true);
        }
        
        void DestroyProjectile()
        {
            Destroy(gameObject);
        }

        //This code can be used for testing hit range
        //private void Update()
        //{
        //    Collider[] hitColliders = Physics.OverlapSphere(transform.position, _explosionRange);
        //    if (hitColliders.Any((collider) => collider.tag == "Ground")) Debug.Log("In range");
        //}
    }

}
