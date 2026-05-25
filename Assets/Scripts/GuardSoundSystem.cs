using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Reacts to <see cref="GuardAI"/> state transitions and drives guard audio.
/// </summary>
[RequireComponent(typeof(GuardAI))]
public class GuardSoundSystem : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private GuardAI guardAI;

    [SerializeField]
    private NavMeshAgent navMeshAgent;

    [SerializeField]
    private AudioSource oneShotSource;

    [SerializeField]
    private AudioSource loopSource;

    [Header("One-Shot Clips")]
    [SerializeField]
    private AudioClip suspiciousEnterClip;

    [SerializeField]
    private AudioClip chaseEnterClip;

    [SerializeField]
    private AudioClip investigatingEnterClip;

    [SerializeField]
    private AudioClip returnToPatrolClip;

    [Header("Loop Clips")]
    [SerializeField]
    private AudioClip patrolFootstepsLoopClip;

    [SerializeField]
    private AudioClip chasingFootstepsLoopClip;

    [SerializeField]
    private AudioClip searchingLoopClip;

    [Header("Loop Mix")]
    [SerializeField]
    private float patrolLoopVolume = 0.2f;

    [SerializeField]
    private float patrolLoopPitch = 0.95f;

    [SerializeField]
    private float chaseLoopVolume = 0.65f;

    [SerializeField]
    private float chaseLoopPitch = 1.25f;

    [SerializeField]
    private float searchingLoopVolume = 0.35f;

    [SerializeField]
    private float searchingLoopPitch = 1f;

    [Header("Movement Detection")]
    [SerializeField]
    private float movingSpeedThreshold = 0.05f;

    private GuardAI.GuardState previousState;
    private bool initializedPreviousState;

    private void Awake()
    {
        if (guardAI == null)
        {
            guardAI = GetComponent<GuardAI>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (oneShotSource == null)
        {
            oneShotSource = GetComponent<AudioSource>();
            if (oneShotSource == null)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (loopSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 1)
            {
                loopSource = sources[1];
            }
            else
            {
                loopSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (loopSource == oneShotSource)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSources();
    }

    private void Update()
    {
        if (guardAI == null)
        {
            return;
        }

        GuardAI.GuardState currentState = guardAI.CurrentGuardState;

        if (!initializedPreviousState)
        {
            previousState = currentState;
            initializedPreviousState = true;
        }

        if (currentState != previousState)
        {
            HandleStateTransition(previousState, currentState);
        }

        HandleLoopingAudio(currentState);
        previousState = currentState;
    }

    private void ConfigureAudioSources()
    {
        if (oneShotSource != null)
        {
            oneShotSource.loop = false;
            oneShotSource.playOnAwake = false;
        }

        if (loopSource != null)
        {
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            loopSource.spatialBlend = 1f;
        }
    }

    private void HandleStateTransition(GuardAI.GuardState fromState, GuardAI.GuardState toState)
    {
        switch (toState)
        {
            case GuardAI.GuardState.Suspicious:
                PlayOneShot(suspiciousEnterClip);
                break;

            case GuardAI.GuardState.Chasing:
                PlayOneShot(chaseEnterClip);
                break;

            case GuardAI.GuardState.Investigating:
                PlayOneShot(investigatingEnterClip);
                break;

            case GuardAI.GuardState.Patrolling:
                if (IsAlertState(fromState))
                {
                    PlayOneShot(returnToPatrolClip);
                }

                break;
        }
    }

    private void HandleLoopingAudio(GuardAI.GuardState state)
    {
        switch (state)
        {
            case GuardAI.GuardState.Patrolling:
                if (IsGuardMoving())
                {
                    PlayLoop(patrolFootstepsLoopClip, patrolLoopVolume, patrolLoopPitch);
                }
                else
                {
                    StopLoop();
                }

                break;

            case GuardAI.GuardState.Chasing:
                PlayLoop(chasingFootstepsLoopClip, chaseLoopVolume, chaseLoopPitch);
                break;

            case GuardAI.GuardState.Searching:
                PlayLoop(searchingLoopClip, searchingLoopVolume, searchingLoopPitch);
                break;

            default:
                StopLoop();
                break;
        }
    }

    private bool IsGuardMoving()
    {
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            if (navMeshAgent.isStopped)
            {
                return false;
            }

            return navMeshAgent.velocity.sqrMagnitude > movingSpeedThreshold * movingSpeedThreshold;
        }

        return false;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || oneShotSource == null)
        {
            return;
        }

        oneShotSource.PlayOneShot(clip);
    }

    private void PlayLoop(AudioClip clip, float volume, float pitch)
    {
        if (loopSource == null || clip == null)
        {
            StopLoop();
            return;
        }

        loopSource.volume = Mathf.Clamp01(volume);
        loopSource.pitch = Mathf.Max(0.01f, pitch);

        if (loopSource.clip != clip)
        {
            loopSource.clip = clip;
            loopSource.Play();
            return;
        }

        if (!loopSource.isPlaying)
        {
            loopSource.Play();
        }
    }

    private void StopLoop()
    {
        if (loopSource == null)
        {
            return;
        }

        if (loopSource.isPlaying)
        {
            loopSource.Stop();
        }

        loopSource.clip = null;
    }

    private static bool IsAlertState(GuardAI.GuardState state)
    {
        return state == GuardAI.GuardState.Suspicious
            || state == GuardAI.GuardState.Chasing
            || state == GuardAI.GuardState.Investigating
            || state == GuardAI.GuardState.Searching;
    }
}
