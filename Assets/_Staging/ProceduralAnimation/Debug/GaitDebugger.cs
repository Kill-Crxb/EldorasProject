using UnityEngine;

// GaitDebugger (staging, test only; remove once gait is signed off)
//
// Attach to: anything in the scene. Point it at the character's ProceduralPoseModule and Animator.
//
// Flips gait weight between 0 and 1 on a timer so the clip locomotion and the gait can be compared while walking,
// without touching the Inspector. Optional slow motion. Restores weight and time scale on disable.
// Measures foot slide: while a contact (Heel / Ball bone, or the foot bone without them) is within contactHeight of
// the ground, its sideways movement counts as slide. Each flip logs the mode that just ran: average slide speed while
// planted and the worst single plant.

public class GaitDebugger : MonoBehaviour
{
    [SerializeField] private ProceduralPoseModule module;

    [Tooltip("The character's Animator, for the foot contacts.")]
    [SerializeField] private Animator animator;

    [Tooltip("Seconds between flips.")]
    [Min(0.25f)] [SerializeField] private float flipSeconds = 3f;

    [Tooltip("Game time scale while this is on. 1 = normal.")]
    [Range(0.05f, 1f)] [SerializeField] private float timeScale = 1f;

    [Tooltip("A contact closer than this to the ground counts as planted, metres.")]
    [Min(0.001f)] [SerializeField] private float contactHeight = 0.02f;

    [SerializeField] private LayerMask ground = ~0;

    private class Contact
    {
        public Transform Point;
        public Transform Ankle;
        public bool Planted;
        public Vector3 Last;
        public Vector3 LastAnkle;
        public float PlantSlide;
        public float PlantAnkleSlide;
        public float MinHeight;
        public float MaxHeight;
        public float PlantedTime;
    }

    private Contact[] contacts;
    private readonly RaycastHit[] hits = new RaycastHit[8];
    private Transform character;
    private float startWeight;
    private float timer;
    private bool gaitOn;
    private float slide;
    private float plantedTime;
    private float worstPlant;
    private float ankleSlide;
    private float worstAnklePlant;
    private int plants;
    private float windowTime;

    private void OnEnable()
    {
        if (module == null || animator == null)
        {
            Debug.LogError("[GaitDebugger] Assign the ProceduralPoseModule and the Animator.", this);
            enabled = false;
            return;
        }

        startWeight = module.GaitWeight;
        timer = 0f;
        gaitOn = false;
        module.GaitWeight = 0f;
        character = animator.transform.root;
        contacts = new[]
        {
            NewContact(HumanBodyBones.LeftFoot, "Heel.L"), NewContact(HumanBodyBones.LeftFoot, "Ball.L"),
            NewContact(HumanBodyBones.RightFoot, "Heel.R"), NewContact(HumanBodyBones.RightFoot, "Ball.R")
        };
        ResetStats();
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
        if (module != null) module.GaitWeight = startWeight;
    }

    private void Update()
    {
        Time.timeScale = timeScale;
        timer += Time.unscaledDeltaTime;
        if (timer < flipSeconds) return;

        Report();
        timer = 0f;
        gaitOn = !gaitOn;
        module.GaitWeight = gaitOn ? 1f : 0f;
        ResetStats();
    }

    private void LateUpdate()
    {
        windowTime += Time.deltaTime;
        foreach (Contact c in contacts) Track(c);
    }

    private void Track(Contact c)
    {
        Vector3 p = c.Point.position;
        Vector3 ankle = c.Ankle.position;
        float height = Height(p);
        bool planted = height < contactHeight;
        c.MinHeight = Mathf.Min(c.MinHeight, height);
        c.MaxHeight = Mathf.Max(c.MaxHeight, height);
        if (planted) c.PlantedTime += Time.deltaTime;

        if (planted && c.Planted)
        {
            Vector3 step = p - c.Last;
            step.y = 0f;
            slide += step.magnitude;
            c.PlantSlide += step.magnitude;
            plantedTime += Time.deltaTime;
            Vector3 ankleStep = ankle - c.LastAnkle;
            ankleStep.y = 0f;
            ankleSlide += ankleStep.magnitude;
            c.PlantAnkleSlide += ankleStep.magnitude;
        }

        if (!planted && c.Planted)
        {
            worstPlant = Mathf.Max(worstPlant, c.PlantSlide);
            worstAnklePlant = Mathf.Max(worstAnklePlant, c.PlantAnkleSlide);
            plants++;
        }

        if (planted && !c.Planted)
        {
            c.PlantSlide = 0f;
            c.PlantAnkleSlide = 0f;
        }

        c.Planted = planted;
        c.Last = p;
        c.LastAnkle = ankle;
    }

    // Height of the point above the ground under it (negative = below the surface), ignoring the character's own
    // colliders (its capsule surrounds the feet). Infinity when there is no ground within 2 m.
    private float Height(Vector3 p)
    {
        int count = Physics.RaycastNonAlloc(p + Vector3.up * 0.3f, Vector3.down, hits, 2.3f, ground,
                                            QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (!hits[i].collider.transform.IsChildOf(character)) nearest = Mathf.Min(nearest, hits[i].distance);

        return nearest - 0.3f;
    }

    private void Report()
    {
        float speed = plantedTime > 0f ? slide / plantedTime : 0f;
        Debug.Log($"[GaitDebugger] {(gaitOn ? "GAIT" : "CLIP")}: planted slide {speed * 100f:0.0} cm/s average, " +
                  $"worst plant {worstPlant * 100f:0.0} cm over {plants} plants. Ankle meanwhile: " +
                  $"{(plantedTime > 0f ? ankleSlide / plantedTime : 0f) * 100f:0.0} cm/s, worst {worstAnklePlant * 100f:0.0} cm.", this);
        foreach (Contact c in contacts)
            Debug.Log($"[GaitDebugger]   {c.Point.name}: height {c.MinHeight * 100f:0.0} to {c.MaxHeight * 100f:0.0} cm, " +
                      $"planted {c.PlantedTime / Mathf.Max(windowTime, 0.001f) * 100f:0}% of the time.", this);
    }

    private void ResetStats()
    {
        slide = 0f;
        plantedTime = 0f;
        worstPlant = 0f;
        ankleSlide = 0f;
        worstAnklePlant = 0f;
        plants = 0;
        windowTime = 0f;
        foreach (Contact c in contacts)
        {
            c.Planted = false;
            c.PlantedTime = 0f;
            c.MinHeight = float.PositiveInfinity;
            c.MaxHeight = float.NegativeInfinity;
        }
    }

    private Contact NewContact(HumanBodyBones foot, string name)
    {
        Transform bone = animator.GetBoneTransform(foot);
        Transform point = bone != null ? bone.Find(name) : null;
        return new Contact { Point = point != null ? point : bone, Ankle = bone };
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10f, 10f, 300f, 24f), gaitOn ? "GAIT" : "CLIP");
    }
}
