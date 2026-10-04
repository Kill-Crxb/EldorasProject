using UnityEngine;

public class HeadLookSystem : AnimatorIKModule
{
    [Header("Facing Source")]
    [SerializeField] private Transform facingOverride;

    [Header("Look Direction")]
    [SerializeField] private float lookDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float verticalInfluence = 1f;
    [SerializeField, Range(0f, 1f)] private float horizontalInfluence = 0.35f;
    [SerializeField] private float maxPitchUp = 50f;
    [SerializeField] private float maxPitchDown = 45f;
    [SerializeField] private float maxYaw = 60f;

    [Header("IK Weights")]
    [SerializeField, Range(0f, 1f)] private float lookWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float bodyWeight = 0.15f;
    [SerializeField, Range(0f, 1f)] private float headWeight = 0.85f;
    [SerializeField, Range(0f, 1f)] private float eyesWeight = 0f;
    [SerializeField, Range(0f, 1f)] private float clampWeight = 0.6f;

    [Header("Smoothing")]
    [SerializeField] private float directionSmoothing = 12f;
    [SerializeField] private float weightFadeSpeed = 4f;

    [Header("Micro Motion")]
    [Tooltip("A degree or two of drift on the look direction. A perfectly still head reads as " +
             "a mannequin; this is the cheapest fix on a character with no face rig.")]
    [SerializeField] private float noiseAngle = 1.5f;
    [SerializeField] private float noiseSpeed = 0.35f;

    [Header("Suppression")]
    [IdRef(IdKind.Fact)] [SerializeField] private string suppressFactKey = "";

    private ICameraProvider cameraProvider;

    private Vector3 currentDirection;
    private Vector3 currentTarget;
    private float currentWeight;
    private bool directionPrimed;

    private int suppressHash;
    private string hashedFor;
    private float noiseSeed;

    public bool ExternalSuppress { get; set; }

    public Vector3 CurrentTarget => currentTarget;
    public Vector3 CurrentDirection => currentDirection;
    public float CurrentWeight => currentWeight;

    // Ordered after the feet so body-moving receivers write animator.bodyPosition first.
    // Bone transforms only reflect that on the following frame, so this is ordering hygiene
    // rather than a same-frame dependency.
    public override int IKOrder => 10;

    protected override void ResolveReferences()
    {
        cameraProvider = brain?.GetProvider<ICameraProvider>();
    }

    protected override void OnRebind(Animator animator)
    {
        directionPrimed = false;

        // Per-instance so a crowd does not drift in unison.
        noiseSeed = Random.value * 100f;
    }

    protected override void Solve(Animator animator)
    {
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        float desiredWeight = ShouldLook() ? lookWeight : 0f;
        currentWeight = Mathf.MoveTowards(currentWeight, desiredWeight, weightFadeSpeed * Time.deltaTime);

        if (currentWeight <= 0f)
        {
            animator.SetLookAtWeight(0f);
            directionPrimed = false;
            return;
        }

        Vector3 desiredDirection = ApplyMicroNoise(CalculateLookDirection());

        if (!directionPrimed)
        {
            currentDirection = desiredDirection;
            directionPrimed = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-directionSmoothing * Time.deltaTime);
            currentDirection = Vector3.Slerp(currentDirection, desiredDirection, t);
        }

        currentTarget = head.position + currentDirection * lookDistance;

        animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(currentTarget);
    }

    private bool ShouldLook()
    {
        if (!IsEnabled || ExternalSuppress) return false;
        if (cameraProvider == null || cameraProvider.CameraTransform == null) return false;
        return !IsSuppressedByFact();
    }

    private Vector3 CalculateLookDirection()
    {
        Transform cam = cameraProvider.CameraTransform;

        Vector3 bodyFlat = Flatten(FacingForward());
        Vector3 camFlat = Flatten(cam.forward);

        float yaw = Vector3.SignedAngle(bodyFlat, camFlat, Vector3.up) * horizontalInfluence;
        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);

        float pitch = Mathf.Asin(Mathf.Clamp(cam.forward.y, -1f, 1f)) * Mathf.Rad2Deg * verticalInfluence;
        pitch = Mathf.Clamp(pitch, -maxPitchDown, maxPitchUp);

        Vector3 flat = Quaternion.AngleAxis(yaw, Vector3.up) * bodyFlat;
        Vector3 right = Vector3.Cross(Vector3.up, flat);

        return Quaternion.AngleAxis(-pitch, right) * flat;
    }

    private Vector3 ApplyMicroNoise(Vector3 direction)
    {
        if (noiseAngle <= 0f) return direction;

        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude < 0.0001f) return direction;

        float t = Time.time * noiseSpeed;
        float yaw = (Mathf.PerlinNoise(noiseSeed + t, 0.37f) - 0.5f) * 2f * noiseAngle;
        float pitch = (Mathf.PerlinNoise(0.71f, noiseSeed + t) - 0.5f) * 2f * noiseAngle;

        return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, right.normalized) * direction;
    }

    private Vector3 FacingForward()
    {
        if (facingOverride != null) return facingOverride.forward;
        if (brain != null && brain.ModelRoot != null) return brain.ModelRoot.forward;
        if (brain != null && brain.EntityRoot != null) return brain.EntityRoot.forward;
        return transform.forward;
    }

    private static Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
    }

    private bool IsSuppressedByFact()
    {
        if (string.IsNullOrEmpty(suppressFactKey)) return false;

        Blackboard blackboard = brain?.Blackboard;
        if (blackboard == null) return false;

        if (hashedFor != suppressFactKey)
        {
            hashedFor = suppressFactKey;
            suppressHash = new BlackboardKey(suppressFactKey).hash;
        }

        return blackboard.GetBool(suppressHash);
    }
}
