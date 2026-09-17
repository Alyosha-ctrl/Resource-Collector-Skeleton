using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/*
 * PlayerController is the owner's local input loop: movement, target
 * selection, and the client-to-server interaction request. The server
 * still owns every world mutation.
 */

public class PlayerController : NetworkBehaviour
{
    [Header("Components")]
    [SerializeField] CharacterController _characterController;
    [SerializeField] Animator _animator;
    [SerializeField] PlayerHeldItem _heldItem;

    [Header("Detection")]
    [SerializeField] float _detectionRadius = 3f;
    [SerializeField] float _detectionAngle = 60f;
    [SerializeField] LayerMask _pickupLayer;

    [Header("Movement")]
    [SerializeField] float _movementSpeed = 4f;
    [SerializeField] float _rotationSpeed = 200f;

    Interactable _closestTarget;
    UnityEngine.Vector2 _smoothedInput;

    void Update()
    {
        if (!IsOwner) return;

        // TODO Slice 2.2: read this owner's movement in Update.
        UnityEngine.Vector2 input = ReadMovementInput();
        // Debug.Log(input);
        
        // TODO Slice 2.5: smooth _smoothedInput toward the raw input so the walk cycle does not pop.
        _smoothedInput = Vector2.MoveTowards(_smoothedInput, input, Time.deltaTime * 10f);


        // TODO Slice 2.3: rotate and move forward/back.
        float rotation = input.x * _rotationSpeed *Time.deltaTime;
        transform.Rotate(0, rotation, 0);

        // TODO Slice 2.4: set the "Speed" animator float so walk speed matches input.
        UnityEngine.Vector3 direction = transform.forward;
        _characterController.Move(direction * _smoothedInput.y *_movementSpeed * Time.deltaTime);
        _animator.SetFloat("Speed", _characterController.velocity.magnitude); 
        
        // TODO Slice 6.2: detect a target and request interaction on E or left-click.
        UpdateInteractionTarget();

        if (Keyboard.current.eKey.wasPressedThisFrame || Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleInteractionPressed();   
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // TODO Slice 2.6: make the main camera follow only its local player. </> end of Slice 2
        if (IsOwner)
            Camera.main.GetComponent<FollowCamera>().Target = transform;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            // TODO Slice 5.2: turn off the current target's Highlightable,
            // then clear _closestTarget.
            ClearSelection();
        }

        base.OnNetworkDespawn();
    }

    void HandleInteractionPressed()
    {
        if (!IsOwner) return;

        // TODO Slice 6.1: if there is no target, return. Otherwise fire the
        // Animator's "Interact" trigger and send the target's NetworkObjectId
        // to the server.
        if(!_closestTarget) return;
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            _animator.SetTrigger("Interact");
        }
        RequestInteractRpc(_closestTarget.NetworkObjectId);
    }

    static UnityEngine.Vector2 ReadMovementInput()
    {
        // TODO Slice 2.1: return WASD input as a two-dimensional vector.
        UnityEngine.Vector2 input = UnityEngine.Vector2.zero;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        return input;
    }

    void UpdateInteractionTarget()
    {
        // TODO Slice 5.1: find the closest valid Interactable in front of the player.
        // When the target changes, clear the old highlight and select the new one.

        // 1. Detect nearby objects with Physics.OverlapSphere, using
        //    _detectionRadius and _pickupLayer.

        // 2. Check each hit and keep the closest Interactable within _detectionAngle.
        //    Ignore hits without an Interactable or whose
        //    CanInteract(_heldItem.ObjectType) returns false.

        // 3. If the closest candidate is still _closestTarget, nothing changed; return.
        Interactable interactable = FindClosestInteractable();
        if(interactable == _closestTarget) return;

        // 4. Otherwise, remove the old highlight, store the new candidate, and
        //    highlight it (if there is one).

        //Highlateble not properly turning off.
        ClearSelection();
        
        if(interactable != null)
        {
            _closestTarget = interactable;
            _closestTarget.GetComponent<Highlightable>().SetHighlighted(true);
        }
    }

    void ClearSelection()
    {
        if(_closestTarget != null)
        {
            _closestTarget.GetComponent<Highlightable>().SetHighlighted(false);
        }

        _closestTarget = null;
    }


    Interactable FindClosestInteractable()
    {
        Collider [] candidates = Physics.OverlapSphere(transform.position, _detectionRadius, _pickupLayer);
        Interactable closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider c in candidates){
            //Check if they're interactable
            // Debug.Log("0");
            if (!c.TryGetComponent(out Interactable interactable)) continue;
            // Debug.Log("1");
            //This is culling everything
            if(!interactable.CanInteract(_heldItem.ObjectType)) continue;
            // Debug.Log("2");
            //Take the two tranforms and compare them to see which is closer
            Vector3 interactDirection = c.transform.position - transform.position;
            float angle = Vector3.Angle(transform.forward, interactDirection.normalized);
            if(angle > _detectionAngle) continue;
            // Debug.Log("3");
            //Take the two tranforms and compare them to see which is closer
            float distance = interactDirection.magnitude;
            if(distance > closestDistance)
            {
                // Debug.Log("4");
                continue;
            }
            else
            {
                // Debug.Log("5");
                closest = interactable;
                closestDistance = distance;
            }
        }
        // Debug.Log(closest);
        return closest;
    }

    [Rpc(SendTo.Server)]
    void RequestInteractRpc(ulong networkObjectId)
    {
        // Debug.Log("Requesting Interact on server");
        // TODO Slice 6.3: resolve the NetworkObject id and invoke its server gateway.
        // The target may have despawned after the owner selected it.
        // Next: Slice 6.4 in Interactable.ServerInteract.
        Dictionary<ulong, NetworkObject> spawnedObjectMap = NetworkManager.SpawnManager.SpawnedObjects;
        if(!spawnedObjectMap.TryGetValue(networkObjectId, out NetworkObject spawnedObject))
        {
            Debug.Log($"Couldn't find id: {networkObjectId}");
            return;
        }

        if(!spawnedObject.TryGetComponent(out Interactable interactable)){
            Debug.Log($"Not Interactable id: {networkObjectId}");
            return;
        }

        if(interactable.CanInteract(_heldItem.ObjectType))
            interactable.ServerInteract(_heldItem);
    }
}
