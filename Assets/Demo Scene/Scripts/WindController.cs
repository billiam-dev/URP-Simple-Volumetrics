using UnityEditor;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class WindController : MonoBehaviour
{
    [SerializeField, Range(0.0f, 2.0f)] float m_WindSpeed = 0.15f;

    void Update()
    {
        Shader.SetGlobalVector("_WindForce", transform.forward * m_WindSpeed);
    }

    void OnDisable()
    {
        Shader.SetGlobalVector("_WindForce", Vector3.zero);
    }

#if UNITY_EDITOR
    static readonly Vector3[] handlesRayPositions = new Vector3[]
    {
        new Vector3(1, 0, 0),
        new Vector3(-1, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, -1, 0),
        new Vector3(1, 1, 0).normalized,
        new Vector3(1, -1, 0).normalized,
        new Vector3(-1, 1, 0).normalized,
        new Vector3(-1, -1, 0).normalized
    };

    void OnDrawGizmosSelected()
    {
        // Yoink https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/LightEditor.cs/
        Handles.color = new Color(0.5f, 0.8f, 1f, 1f);

        float size = HandleUtility.GetHandleSize(transform.position) * 2f;
        float radius = size * 0.2f;

        using (new Handles.DrawingScope(Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one)))
        {
            Handles.DrawWireDisc(Vector3.zero, Vector3.forward, radius);
            foreach (Vector3 normalizedPos in handlesRayPositions)
            {
                Vector3 pos = normalizedPos * radius;
                Handles.DrawLine(pos, pos + new Vector3(0, 0, size + (m_WindSpeed * 2) - 0.3f));
            }
        }
    }
#endif
}
