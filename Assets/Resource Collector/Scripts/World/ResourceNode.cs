using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

/*
 * ResourceNode is a harvestable object like a tree or stone. Replicated health
 * counts down as players hit it with the right tool; at zero the server spawns
 * resource pickups and every client hides the depleted node.
 */

public class ResourceNode : Interactable
{
    [SerializeField] List<ObjectType> _toolTypeRequired = new();
    [SerializeField] NetworkObject _producedPrefab;
    [SerializeField] int _amountToSpawn = 3;
    [SerializeField] int _startingHealth = 1;
    [SerializeField] AudioClip _audioClip;

    readonly NetworkVariable<int> _health = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        

        if(IsServer)
        {
            // TODO Slice 8.1: on the server, set health to _startingHealth. 
            
            _health.Value = _startingHealth;
        }
        // Then subscribe to health changes and apply the current health.
        _health.OnValueChanged += HandleHealthChanged;
        HandleHealthChanged(0, _health.Value);
        ApplyHealth();
    }

    public override void OnNetworkDespawn()
    {
        // TODO Slice 8.4: unsubscribe from replicated health changes.
        _health.OnValueChanged -= HandleHealthChanged;
        base.OnNetworkDespawn();
    }

    public override bool CanInteract(ObjectType heldType)
    {
        // TODO Slice 8.5: require a living node and an accepted tool.
        if(_toolTypeRequired[0] == heldType && _health.Value > 0) return true;

        return false;
    }

    protected override void Interact(PlayerHeldItem heldItem)
    {
        // TODO Slice 8.7: reduce health and play feedback. At zero, spawn
        // </> end of Slice 8
        HitFeedbackRpc();
        HandleHealthChanged(_health.Value, _health.Value - 1);
        if(_health.Value <= 0) SpawnResources();
        ApplyHealth();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void HitFeedbackRpc()
    {
        // TODO Slice 8.6: play the authored hit sound on each observer.
        Debug.Log("Played Hit Sound");
        AudioSource.PlayClipAtPoint(_audioClip, transform.position);
    }

    void HandleHealthChanged(int previousValue, int newValue)
    {
        // TODO Slice 8.3: apply replicated health locally.
        _health.Value = newValue;
        ApplyHealth();
    }

    void ApplyHealth()
    {
        // TODO Slice 8.2: hide depleted nodes and disable their collider.
        if(_health.Value <= 0)
        {
            //Kill the node
            gameObject.SetActive(false);
            // NetworkObject.Despawn(true);
        }
    }

    void SpawnResources()
    {
        // _amountToSpawn copies of _producedPrefab with InstantiateAndSpawn.
        // Place each on the ground with a small random XZ offset and random yaw.
        for (int i = 0; i < _amountToSpawn; i++)
        {
            //Randomize the position
            Vector3 newPosition = transform.position;
            newPosition.x += Random.Range(-1,1);
            newPosition.z += Random.Range(-1,1);
            Quaternion newRotation = transform.rotation;
            newRotation.x += Random.Range(-30, 30);
            newRotation.y += Random.Range(-30, 30);
            newRotation.z += Random.Range(-30, 30);
            NetworkObject.InstantiateAndSpawn(_producedPrefab.gameObject, NetworkManager, position: newPosition, rotation: newRotation);
        }
    }
}
