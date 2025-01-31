using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class VisionAuthoring : MonoBehaviour
{
    public int VisionRadius;
    public class Baker : Baker<VisionAuthoring>
    {
        public override void Bake(VisionAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new VisionCharsComponent { radius = authoring.VisionRadius });
            AddComponent(entity, new VisibilityComponent { visibleToTeams = 0 });
        }
    }
}

public struct VisionCharsComponent : IComponentData
{
    public int radius;
}
public struct VisibilityComponent : IComponentData
{
    public int visibleToTeams;
}
