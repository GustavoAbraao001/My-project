using UnityEngine;
public class ClickMarker : MonoBehaviour
{
    [Header("Cores")]
    [SerializeField] private Color validColor = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Forma")]
    [SerializeField] private float startRadius = 0.7f;
    [SerializeField] private float endRadius = 0.2f;
    [SerializeField] private float lineWidth = 0.07f;
    [SerializeField] private int segments = 48;
    [Tooltip("Pequeno anel que fica no centro e some mais devagar.")]
    [SerializeField] private bool showInnerRing = true;

    [Header("Animação")]
    [SerializeField] private float duration = 0.45f;
    [Tooltip("Sobe um pouco o anel para não brigar (z-fighting) com o chão.")]
    [SerializeField] private float heightOffset = 0.06f;

    [Header("Material (opcional)")]
    [Tooltip("Deixe vazio para usar o shader Sprites/Default. Se aparecer rosa/invisível, crie um Material Unlit e arraste aqui.")]
    [SerializeField] private Material lineMaterial;

    private LineRenderer outerRing;
    private LineRenderer innerRing;
    private Vector3[] circle;

    private float timer;
    private bool playing;
    private Color currentColor;

    private void Awake()
    {
        circle = new Vector3[Mathf.Max(8, segments)];

        Material material = lineMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("ClickMarker: shader Sprites/Default não encontrado. Arraste um Material em Line Material.", this);
                enabled = false;
                return;
            }

            material = new Material(shader);
        }

        outerRing = CreateRing("OuterRing", material);
        innerRing = showInnerRing ? CreateRing("InnerRing", material) : null;

        SetVisible(false);
    }
    public void Show(Vector3 worldPosition, bool valid = true)
    {
        if (!enabled)
        {
            return;
        }

        transform.position = worldPosition + Vector3.up * heightOffset;
        currentColor = valid ? validColor : invalidColor;
        timer = 0f;
        playing = true;

        SetVisible(true);
        Animate(0f);
    }

    private void Update()
    {
        if (!playing)
        {
            return;
        }

        timer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));

        Animate(t);

        if (t >= 1f)
        {
            playing = false;
            SetVisible(false);
        }
    }

    private void Animate(float t)
    {
        float ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out: começa rápido, termina suave
        float outerRadius = Mathf.Lerp(startRadius, endRadius, ease);
        DrawCircle(outerRing, outerRadius, WithAlpha(currentColor, 1f - t));

        if (innerRing != null)
        {
            float innerRadius = endRadius * 0.45f;
            float innerAlpha = Mathf.Clamp01(1f - t * 0.8f);
            DrawCircle(innerRing, innerRadius, WithAlpha(currentColor, innerAlpha));
        }
    }

    private LineRenderer CreateRing(string ringName, Material material)
    {
        GameObject go = new GameObject(ringName);
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = circle.Length;
        lr.alignment = LineAlignment.TransformZ;
        lr.sharedMaterial = material;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.widthMultiplier = lineWidth;
        return lr;
    }

    private void DrawCircle(LineRenderer lr, float radius, Color color)
    {
        for (int i = 0; i < circle.Length; i++)
        {
            float angle = (float)i / circle.Length * Mathf.PI * 2f;
            circle[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        lr.SetPositions(circle);
        lr.startColor = color;
        lr.endColor = color;
    }

    private void SetVisible(bool visible)
    {
        if (outerRing != null) outerRing.enabled = visible;
        if (innerRing != null) innerRing.enabled = visible;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}