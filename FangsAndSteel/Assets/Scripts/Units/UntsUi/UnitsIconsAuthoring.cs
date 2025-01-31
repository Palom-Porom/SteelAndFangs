using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class UnitsIconsAuthoring : MonoBehaviour
{
    public GameObject infoQuadsGO;
    public GameObject healthBarGO;
    public GameObject reloadBarGO;

    public GameObject VisualizationGO;
    public class Baker : Baker<UnitsIconsAuthoring>
    {
        public override void Bake(UnitsIconsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitIconsComponent
            {
            infoQuadsEntity = GetEntity(authoring.infoQuadsGO, TransformUsageFlags.Dynamic), 
            healthBarEntity = GetEntity(authoring.healthBarGO, TransformUsageFlags.Dynamic), 
            reloadBarEntity = GetEntity(authoring.reloadBarGO, TransformUsageFlags.Dynamic),

            VisualizationEntity = GetEntity(authoring.VisualizationGO, TransformUsageFlags.Dynamic)
            });
            
        }
    }
}
            
            
        
public struct UnitIconsComponent: IComponentData
{
    public Entity infoQuadsEntity;
    public Entity healthBarEntity;
    public Entity reloadBarEntity;

    public Entity VisualizationEntity;
}
