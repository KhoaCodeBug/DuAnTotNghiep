using UnityEngine;

/// <summary>
/// Forwards Animation Events from child visual Animator (InterpolationTarget)
/// to PlayerMovement / PlayerCombat on the root NetworkObject.
/// </summary>
public class PlayerAnimationEventForwarder : MonoBehaviour
{
    private PlayerMovement playerMove;

    private void Awake()
    {
        CachePlayerMovement();
    }

    private void CachePlayerMovement()
    {
        if (playerMove == null)
        {
            playerMove = GetComponentInParent<PlayerMovement>();
        }
    }

    public void OnFootstep()
    {
        CachePlayerMovement();
        playerMove?.OnFootstep();
    }

    public void OnWalkFootstep()
    {
        CachePlayerMovement();
        playerMove?.OnWalkFootstep();
    }

    public void OnRunFootstep()
    {
        CachePlayerMovement();
        playerMove?.OnRunFootstep();
    }

    public void OnMeleeSwing()
    {
        CachePlayerMovement();
        playerMove?.OnMeleeSwing();
    }
}
