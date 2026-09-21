using UnityEngine;

public class PacStudentMovement : MonoBehaviour
{
    [Header("Path")]
    public Transform[] waypoints;

    [Header("Movement")]
    public float speed = 3f;

    [Header("References")]
    public Animator animator;
    public AudioSource moveAudioSource;

    private const string STATE_UP = "Cat_up";
    private const string STATE_DOWN = "Cat_Down";
    private const string STATE_LEFT = "Cat_Left";
    private const string STATE_RIGHT = "Cat_Right";

    private int currentIndex = 0;
    private Vector3 segmentStart;
    private Vector3 segmentEnd;
    private float segmentDuration;
    private float segmentTimer;

    private void Start()
    {
        if (waypoints == null || waypoints.Length != 4)
        {
            Debug.LogError("PacStudentMovement requires exactly 4 waypoints assigned in the Inspector.");
            enabled = false;
            return;
        }

        transform.position = waypoints[0].position;
        BeginSegment();

        if (moveAudioSource != null)
        {
            moveAudioSource.loop = true;
            moveAudioSource.Play();
        }
    }

    private void Update()
    {
        segmentTimer += Time.deltaTime;
        float t = Mathf.Clamp01(segmentTimer / segmentDuration);

        transform.position = Vector3.Lerp(segmentStart, segmentEnd, t);

        if (t >= 1f)
        {
            currentIndex = (currentIndex + 1) % waypoints.Length;
            BeginSegment();
        }
    }

    private void BeginSegment()
    {
        segmentStart = waypoints[currentIndex].position;
        int nextIndex = (currentIndex + 1) % waypoints.Length;
        segmentEnd = waypoints[nextIndex].position;

        float distance = Vector3.Distance(segmentStart, segmentEnd);
        segmentDuration = distance / speed;
        segmentTimer = 0f;

        Vector3 direction = (segmentEnd - segmentStart).normalized;
        PlayDirectionAnimation(direction);
    }

    private void PlayDirectionAnimation(Vector3 direction)
    {
        if (animator == null) return;

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            animator.Play(direction.x > 0 ? STATE_RIGHT : STATE_LEFT);
        }
        else
        {
            animator.Play(direction.y > 0 ? STATE_UP : STATE_DOWN);
        }
    }
}