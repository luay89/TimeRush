using UnityEngine;

public static class CombatPrimitiveCache
{
    private static Mesh sphere;
    private static Mesh capsule;
    private static Mesh cylinder;

    public static Mesh Sphere => sphere ? sphere : sphere = Resolve(PrimitiveType.Sphere);
    public static Mesh Capsule => capsule ? capsule : capsule = Resolve(PrimitiveType.Capsule);
    public static Mesh Cylinder => cylinder ? cylinder : cylinder = Resolve(PrimitiveType.Cylinder);

    private static Mesh Resolve(PrimitiveType type)
    {
        GameObject probe = GameObject.CreatePrimitive(type);
        Mesh mesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(probe);
        return mesh;
    }
}

