using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class AttackAuthoring : MonoBehaviour
{
    public int damage = 0;
    public float realodLen = 1;
    public int attackRadius = 0;
    public class Baker : Baker<AttackAuthoring>
    {
        public override void Bake(AttackAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new AttackComponent { 
                damage = authoring.damage,
                reloadLen = authoring.realodLen,
                curReload = 0,
                radiusSq = authoring.attackRadius * authoring.attackRadius,
                target = Entity.Null });

            AddComponent(entity, new AttackSettingsComponent {targettingMinHP = false, shootingOnMoveMode = false});
        }
    }
}
public struct AttackComponent : IComponentData
{
    public int damage;
    public float reloadLen;
    public float curReload;
    public int radiusSq;
    public Entity target;
}

public struct AttackSettingsComponent : IComponentData
{
    public bool targettingMinHP;
    public bool shootingOnMoveMode;
}