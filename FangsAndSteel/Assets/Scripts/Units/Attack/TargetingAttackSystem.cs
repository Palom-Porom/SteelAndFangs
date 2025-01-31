using AnimCooker;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
//using UnityEngine.Apple.ReplayKit;
using UnityEngine.Assertions;
using UnityEngine.UIElements;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.InputSystem.LowLevel.InputStateHistory;

[UpdateInGroup(typeof(UnitsSystemGroup))]
[BurstCompile]
public partial struct TargetingAttackSystem : ISystem, ISystemStartStop
{
    ComponentLookup<HpComponent> hpLookup;
    ComponentLookup<Deployable> deployableLookup;
    ComponentLookup<LocalToWorld> localToWorldLookup;
    ComponentLookup<TeamComponent> teamLookup;
    ComponentLookup<VisibilityComponent> visibilityLookup;
    ComponentLookup<FillFloatOverride> fillBarLookup;
    ComponentLookup<VehicleMovementComponent> vehicleLookup;


    ///<value> All units which can reload </value>
    EntityQuery reloaders;
    ///<value> All units which can be a target for another unit </value>
    EntityQuery potentialTargetsQuery;
    ///<value> All units which are looking for target </value>
    EntityQuery targetSearchersQuery;
    ///<value> All units who can create attack request </value>
    ///<remarks> Without deployable </remarks>
    EntityQuery usualAttackers;
    ///<value> All units who can create attack request </value>
    ///<remarks> With deployable </remarks>
    EntityQuery deployableAttackers;
    ///<value> All units which are in a pursuing mode </value>
    EntityQuery pursuiers;

    ComponentTypeHandle<ReloadComponent> reloadTypeHandle;
    ComponentTypeHandle<ReloadComponent> reloadTypeHandleRO;
    ComponentTypeHandle<TeamComponent> teamTypeHandleRO;
    ComponentTypeHandle<UnitIconsComponent> unitIconsTypeHandleRO;
    ComponentTypeHandle<MovementComponent> movementTypeHandle;
    ComponentTypeHandle<RotationComponent> rotationTypeHandle;
    ComponentTypeHandle<AttackCharsComponent> attackCharsTypeHandleRO;
    ComponentTypeHandle<PursuingModeComponent> pursuingModeTypeHandle;
    ComponentTypeHandle<PursuingModeComponent> pursuingModeTypeHandleRO;
    ComponentTypeHandle<BattleModeComponent> battleModeTypeHandle;
    ComponentTypeHandle<BattleModeComponent> battleModeTypeHandleRO;
    ComponentTypeHandle<LocalTransform> transformTypeHandleRO;
    ComponentTypeHandle<Deployable> deployableTypeHandle;
    ComponentTypeHandle<Deployable> deployableTypeHandleRO;
    EntityTypeHandle entityTypeHandle;
    BufferTypeHandle<ModelsBuffer> modelsBuffTypeHandle;
    BufferTypeHandle<AttackModelsBuffer> attackModelsBuffTypeHandle;

    #region Animation vars
    ComponentLookup<AnimationCmdData> animCmdLookup;
    ComponentLookup<AnimationStateData> animStateLookup;
    NativeArray<AnimDbEntry> attackClips;
    NativeArray<AnimDbEntry> reloadClips;
    NativeArray<AnimDbEntry> reloadOnMoveClips;
    NativeArray<AnimDbEntry> moveClips;
    NativeArray<AnimDbEntry> deployClips;
    NativeArray<AnimDbEntry> undeployClips;
    NativeArray<AnimDbEntry> restClips;
    NativeArray<AnimDbEntry> rest_deployedClips;
    #endregion

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AttackCharsComponent>();
        state.RequireForUpdate<HpComponent>();

        hpLookup = state.GetComponentLookup<HpComponent>(true);
        deployableLookup = state.GetComponentLookup<Deployable>(true);
        localToWorldLookup = state.GetComponentLookup<LocalToWorld>(true);
        teamLookup = state.GetComponentLookup<TeamComponent>(true);
        visibilityLookup = state.GetComponentLookup<VisibilityComponent>(true);
        fillBarLookup = state.GetComponentLookup<FillFloatOverride>();
        vehicleLookup = state.GetComponentLookup<VehicleMovementComponent>(true);


        reloaders = new EntityQueryBuilder(Allocator.Temp).
            WithAllRW<ReloadComponent>().
            WithAll<UnitIconsComponent>().
            WithAny<ModelsBuffer, AttackModelsBuffer>().
            Build(ref state);
        potentialTargetsQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<HpComponent, LocalToWorld, TeamComponent>().Build(ref state);
        targetSearchersQuery = new EntityQueryBuilder(Allocator.Temp).
            WithAllRW<AttackCharsComponent, MovementComponent>().
            WithAllRW<ReloadComponent>().
            WithPresentRW<BattleModeComponent>().
            WithDisabledRW<PursuingModeComponent>().
            WithAll<LocalTransform, TeamComponent, UnitTypeComponent, ModelsBuffer>().
            WithAspect<AttackPrioritiesAspect>().
            Build(ref state);

        usualAttackers = new EntityQueryBuilder(Allocator.Temp).
            WithAllRW<MovementComponent, ReloadComponent>().
            WithAllRW<RotationComponent>().
            WithAll<AttackCharsComponent, PursuingModeComponent, BattleModeComponent, LocalTransform>().
            WithAny<ModelsBuffer, AttackModelsBuffer>().
            WithNone<Deployable>().
            WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).
            Build(ref state);
        deployableAttackers = new EntityQueryBuilder(Allocator.Temp).
            WithAllRW<MovementComponent, ReloadComponent>().
            WithAllRW<RotationComponent, Deployable>().
            WithAll<AttackCharsComponent, PursuingModeComponent, /*BattleModeComponent,*/ LocalTransform>().
            WithAny<ModelsBuffer, AttackModelsBuffer>().
            WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).
            Build(ref state);
        pursuiers = new EntityQueryBuilder(Allocator.Temp).
            WithAllRW<PursuingModeComponent, MovementComponent>().
            //WithAllRW<BattleModeComponent>(). 
            WithAll<LocalTransform, ReloadComponent, ModelsBuffer>().
            Build(ref state);

        reloadTypeHandle = SystemAPI.GetComponentTypeHandle<ReloadComponent>();
        reloadTypeHandleRO = SystemAPI.GetComponentTypeHandle<ReloadComponent>(true);
        teamTypeHandleRO = SystemAPI.GetComponentTypeHandle<TeamComponent>(true);
        unitIconsTypeHandleRO = SystemAPI.GetComponentTypeHandle<UnitIconsComponent>(true);
        movementTypeHandle = SystemAPI.GetComponentTypeHandle<MovementComponent>();
        rotationTypeHandle = SystemAPI.GetComponentTypeHandle<RotationComponent>();
        attackCharsTypeHandleRO = SystemAPI.GetComponentTypeHandle<AttackCharsComponent>(true);
        pursuingModeTypeHandle = SystemAPI.GetComponentTypeHandle<PursuingModeComponent>();
        pursuingModeTypeHandleRO = SystemAPI.GetComponentTypeHandle<PursuingModeComponent>(true);
        battleModeTypeHandle = SystemAPI.GetComponentTypeHandle<BattleModeComponent>();
        battleModeTypeHandleRO = SystemAPI.GetComponentTypeHandle<BattleModeComponent>(true);
        transformTypeHandleRO = SystemAPI.GetComponentTypeHandle<LocalTransform>(true);
        deployableTypeHandle = SystemAPI.GetComponentTypeHandle<Deployable>();
        deployableTypeHandleRO = SystemAPI.GetComponentTypeHandle<Deployable>(true);
        entityTypeHandle = SystemAPI.GetEntityTypeHandle();
        modelsBuffTypeHandle = SystemAPI.GetBufferTypeHandle<ModelsBuffer>(true);
        attackModelsBuffTypeHandle = SystemAPI.GetBufferTypeHandle<AttackModelsBuffer>(true);
        

        #region Get Animation Lookups
        animCmdLookup = state.GetComponentLookup<AnimationCmdData>();
        animStateLookup = state.GetComponentLookup<AnimationStateData>();
        #endregion
    }

    public void OnStartRunning(ref SystemState state)
    {
        #region Animation Clips Arrays
        attackClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Attack");
        reloadClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Recharge");
        reloadOnMoveClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Action_Recharge");
        moveClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Move");
        deployClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Deploy");
        undeployClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Undeploy");
        restClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Rest");
        rest_deployedClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Rest_Deployed");
        #endregion
    }

    public void OnStopRunning(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        #region Update data
        hpLookup.Update(ref state);
        deployableLookup.Update(ref state);
        localToWorldLookup.Update(ref state);
        teamLookup.Update(ref state);
        visibilityLookup.Update(ref state);
        fillBarLookup.Update(ref state);
        vehicleLookup.Update(ref state);

        animCmdLookup.Update(ref state);
        animStateLookup.Update(ref state);

        reloadTypeHandle.Update(ref state);
        reloadTypeHandleRO.Update(ref state);
        teamTypeHandleRO.Update(ref state);
        unitIconsTypeHandleRO.Update(ref state);
        movementTypeHandle.Update(ref state);
        rotationTypeHandle.Update(ref state);
        attackCharsTypeHandleRO.Update(ref state);
        pursuingModeTypeHandle.Update(ref state);
        pursuingModeTypeHandleRO.Update(ref state);
        battleModeTypeHandle.Update(ref state);
        battleModeTypeHandleRO.Update(ref state);
        transformTypeHandleRO.Update(ref state);
        deployableTypeHandle.Update(ref state);
        deployableTypeHandleRO.Update(ref state);
        entityTypeHandle.Update(ref state);
        modelsBuffTypeHandle.Update(ref state);
        attackModelsBuffTypeHandle.Update(ref state);

        NativeArray<Entity> potentialTargetsArr = potentialTargetsQuery.ToEntityArray(Allocator.TempJob);

        var ecb =  SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

        float deltaTime = SystemAPI.Time.DeltaTime;
        #endregion



        JobHandle reloadJobHandle = new _ReloadJob
        {
            deltaTime = deltaTime,
            fillBarLookup = fillBarLookup,

            animCmdLookup = animCmdLookup,
            animStateLookup = animStateLookup,
            reloadClips = reloadClips,
            reloadOnMoveClips = reloadOnMoveClips,

            reloadTypeHandle = reloadTypeHandle,
            unitsIconsTypeHandleRO = unitIconsTypeHandleRO,
            modelsBuffsTypeHandle = modelsBuffTypeHandle,
            attackModelsBuffsTypeHandle = attackModelsBuffTypeHandle,
        }.Schedule(reloaders, state.Dependency);

        JobHandle deployJobHandle = new UpdateDeployJob
        {
            deltaTime = deltaTime,

            animCmdLookup = animCmdLookup,
            animStateLookup = animStateLookup,
            moveClips = moveClips,
            restClips = restClips
        }.Schedule(reloadJobHandle);

        JobHandle targetSearchJobHandle = new AttackTargetSearchJob
        {
            hpLookup = hpLookup,
            localToWorldLookup = localToWorldLookup,
            potentialTargetsArr = potentialTargetsArr,
            teamLookup = teamLookup,
            visibilityLookup = visibilityLookup,
            vehicleLookup = vehicleLookup,
            deployLookup = deployableLookup,
            
            animCmdLookup = animCmdLookup,
            animStateLookup = animStateLookup,
            moveClips = moveClips,
        }.Schedule(targetSearchersQuery, deployJobHandle);

        JobHandle usualCreateAttackRequestsJobHandle = new _CreateUsualAttackRequestsJob
        {
            localToWorldLookup = localToWorldLookup,
            ecb = ecb,

            animCmdLookup = animCmdLookup,
            animStateLookup = animStateLookup,
            attackClips = attackClips,
            reloadClips = reloadClips,
            moveClips = moveClips,
            restClips = restClips,

            attackCharsTypeHandleRO = attackCharsTypeHandleRO,
            battleModeSetsTypeHandleRO = battleModeTypeHandleRO,
            pursuingModeSettsTypeHandleRO = pursuingModeTypeHandleRO,
            movementTypeHandle = movementTypeHandle,
            reloadTypeHandle = reloadTypeHandle,
            transformTypeHandleRO = transformTypeHandleRO,
            rotationTypeHandle = rotationTypeHandle,
            entityTypeHandle = entityTypeHandle,
            modelsBuffTypeHandle = modelsBuffTypeHandle,
            attackModelsBuffTypeHandle = attackModelsBuffTypeHandle

        }.Schedule(usualAttackers, targetSearchJobHandle);

        JobHandle deployableCreateAttackRequestsJobHandle = new _CreateDeployableAttackRequestsJob
        {
            localToWorldLookup = localToWorldLookup,
            ecb = ecb,
            deltaTime = deltaTime,

            animCmdLookup = animCmdLookup,
            animStateLookup = animStateLookup,
            attackClips = attackClips,
            reloadClips = reloadClips,
            deployClips = deployClips,
            undeployClips = undeployClips,
            rest_deployedClips = rest_deployedClips,

            attackCharsTypeHandleRO = attackCharsTypeHandleRO,
            battleModeSettsSettsTypeHandleRO = battleModeTypeHandleRO,
            pursuingModeSettsTypeHandleRO = pursuingModeTypeHandleRO,
            deployableTypeHandle = deployableTypeHandle,
            movementTypeHandle = movementTypeHandle,
            reloadTypeHandle = reloadTypeHandle,
            rotationTypeHandle = rotationTypeHandle,
            transformTypeHandleRO = transformTypeHandleRO,
            modelsBuffTypeHandle = modelsBuffTypeHandle,
            attackModelsBuffTypeHandle = attackModelsBuffTypeHandle
        }.Schedule(deployableAttackers, usualCreateAttackRequestsJobHandle);

        state.Dependency = new PursuingJob
        {
            deltaTime = deltaTime,
            localToWorldLookup = localToWorldLookup,
            visibilityLookup = visibilityLookup,

            pursuingModeSettsTypeHandle = pursuingModeTypeHandle,
            battleModeSetsTypeHandle = battleModeTypeHandle,
            reloadTypeHandleRO = reloadTypeHandleRO,
            teamTypeHandleRO = teamTypeHandleRO,
            movementTypeHandle = movementTypeHandle,
            transformTypeHandleRO = transformTypeHandleRO,
            deployableTypeHandleRO = deployableTypeHandleRO,
            attackModelsBuffTypeHandle = attackModelsBuffTypeHandle,
            modelsBuffTypeHandle = modelsBuffTypeHandle,

            animCmdLookup = animCmdLookup,
            animStateLookup = animStateLookup,
            moveClips = moveClips
        }.Schedule(pursuiers, deployableCreateAttackRequestsJobHandle);

        potentialTargetsArr.Dispose(targetSearchJobHandle);
    }
}




///<summary> Update all units' reloadTime values and reload bars </summary>
//[BurstCompile]
public partial struct _ReloadJob : IJobChunk
{
    public float deltaTime;

    public ComponentLookup<FillFloatOverride> fillBarLookup;

    public ComponentLookup<AnimationCmdData> animCmdLookup;
    [ReadOnly] public ComponentLookup<AnimationStateData> animStateLookup;
    [ReadOnly] public NativeArray<AnimDbEntry> reloadClips;
    [ReadOnly] public NativeArray<AnimDbEntry> reloadOnMoveClips;

    public ComponentTypeHandle<ReloadComponent> reloadTypeHandle;
    [ReadOnly] public ComponentTypeHandle<UnitIconsComponent> unitsIconsTypeHandleRO;
    //ComponentTypeHandle<Deployable> deployableTypeHandle;
    [ReadOnly] public BufferTypeHandle<ModelsBuffer> modelsBuffsTypeHandle;
    [ReadOnly] public BufferTypeHandle<AttackModelsBuffer> attackModelsBuffsTypeHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        unsafe
        {
            ReloadComponent* reloads = chunk.GetComponentDataPtrRW(ref reloadTypeHandle);
            UnitIconsComponent* unitIcons = chunk.GetComponentDataPtrRO(ref unitsIconsTypeHandleRO);
            BufferAccessor<ModelsBuffer> modelsBuffs = chunk.GetBufferAccessor(ref modelsBuffsTypeHandle);

            BufferAccessor<AttackModelsBuffer> attackModelsBuffs = new BufferAccessor<AttackModelsBuffer>();
            bool hasAttackModels = chunk.Has(ref attackModelsBuffsTypeHandle);
            if (hasAttackModels)
                attackModelsBuffs = chunk.GetBufferAccessor(ref attackModelsBuffsTypeHandle);

            Assert.IsFalse(useEnabledMask);

            for (int i = 0; i < chunk.Count; i++)
            {
                if (reloads[i].curBullets == 0) // if drum is empty -> drum reload
                {
                    if (reloads[i].shootAnimElapsed < reloads[i].shootAnimLen)
                    {
                        fillBarLookup.GetRefRW(unitIcons[i].reloadBarEntity).ValueRW.Value = 0f;
                        reloads[i].shootAnimElapsed += deltaTime;
                        continue;
                    }

                    if (reloads[i].drumReloadElapsed == 0f)//if just started reloading -> reload anim
                    {
                        if (!hasAttackModels)
                        {
                            if (!reloads[i].isShootingOnMoveAnim)
                            {
                                foreach (var modelBufElem in modelsBuffs[i])
                                {
                                    RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                                    animCmd.ValueRW.ClipIndex = reloadClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                                    animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                                }
                            }
                            else
                            {
                                Debug.Log("Started Action_Recharge Animation");
                                foreach (var modelBufElem in modelsBuffs[i])
                                {
                                    RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                                    animCmd.ValueRW.ClipIndex = reloadOnMoveClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                                    animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                                }
                            }
                        }
                        else
                        {
                            foreach (var modelBufElem in attackModelsBuffs[i])
                            {
                                RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                                animCmd.ValueRW.ClipIndex = reloadClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                                animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                            }
                        }
                    }

                    reloads[i].drumReloadElapsed += deltaTime;
                    if (reloads[i].drumReloadElapsed > reloads[i].drumReloadLen * (1 + reloads[i].curDebaff))
                    {
                        reloads[i].curBullets = reloads[i].maxBullets;
                        reloads[i].bulletReloadElapsed = reloads[i].bulletReloadLen;
                        reloads[i].drumReloadElapsed = 0f;
                        fillBarLookup.GetRefRW(unitIcons[i].reloadBarEntity).ValueRW.Value = 1f;
                        reloads[i].shootAnimElapsed = 0f;
                        continue;
                    }
                    fillBarLookup.GetRefRW(unitIcons[i].reloadBarEntity).ValueRW.Value = reloads[i].drumReloadElapsed / (reloads[i].drumReloadLen * (1 + reloads[i].curDebaff));

                }

                else if (reloads[i].bulletReloadElapsed <= reloads[i].bulletReloadLen) // reload of the bullet
                {
                    if (reloads[i].bulletReloadElapsed <= float.Epsilon) //If just shot -> update the ReloadBar
                        fillBarLookup.GetRefRW(unitIcons[i].reloadBarEntity).ValueRW.Value = (float)reloads[i].curBullets / reloads[i].maxBullets;
                    reloads[i].bulletReloadElapsed += deltaTime;
                }
            }
        }
    }
}


///<summary> Update deploying/undeploying values of units </summary>
//[BurstCompile]
public partial struct UpdateDeployJob : IJobEntity
{
    public float deltaTime;

    public ComponentLookup<AnimationCmdData> animCmdLookup;
    [ReadOnly] public ComponentLookup<AnimationStateData> animStateLookup;
    [ReadOnly] public NativeArray<AnimDbEntry> moveClips;
    [ReadOnly] public NativeArray<AnimDbEntry> restClips;

    public void Execute(ref Deployable deployable, ref MovementComponent movementComponent, in DynamicBuffer<ModelsBuffer> modelsBuf, in ReloadComponent reload)
    {
        //Undeploying
        if (!deployable.deployedState)
        {
            if (!movementComponent.isAbleToMove)
            {
                if (!reload.isReloaded()) //not undeploy until full reloaded
                    return;

                if (deployable.deployTimeElapsed <= 0)
                {
                    movementComponent.isAbleToMove = true;

                    if (movementComponent.hasMoveTarget) //Set move animation PlayForever
                    {
                        foreach (var modelBufElem in modelsBuf)
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            animCmd.ValueRW.ClipIndex = moveClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animCmd.ValueRW.Cmd = AnimationCmd.SetPlayForever;
                        }
                    }
                    else //Set rest animation PlayForever
                    {
                        foreach (var modelBufElem in modelsBuf)
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            animCmd.ValueRW.ClipIndex = restClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animCmd.ValueRW.Cmd = AnimationCmd.SetPlayForever;
                        }
                    }
                }
                else
                    deployable.deployTimeElapsed -= deltaTime;
            }
        }
        //Deploying
        else if (deployable.deployTimeElapsed < deployable.deployTime)
        {
            deployable.deployTimeElapsed += deltaTime;
        }
    }
}


///<summary> Searching for the most valuable target at the moment for ALL units </summary>
//[BurstCompile]
public partial struct AttackTargetSearchJob : IJobEntity
{
    /// <summary> In other words all units that can be attacked </summary>
    [ReadOnly] public NativeArray<Entity> potentialTargetsArr;

    [ReadOnly] public ComponentLookup<LocalToWorld> localToWorldLookup;
    [ReadOnly] public ComponentLookup<TeamComponent> teamLookup;
    [ReadOnly] public ComponentLookup<HpComponent> hpLookup;
    [ReadOnly] public ComponentLookup<VisibilityComponent> visibilityLookup;
    [ReadOnly] public ComponentLookup<VehicleMovementComponent> vehicleLookup;
    [ReadOnly] public ComponentLookup<Deployable> deployLookup;

    public ComponentLookup<AnimationCmdData> animCmdLookup;
    [ReadOnly] public ComponentLookup<AnimationStateData> animStateLookup;
    [ReadOnly] public NativeArray<AnimDbEntry> moveClips;

    public void Execute(ref AttackCharsComponent attackChars, ref BattleModeComponent modeSettings, ref PursuingModeComponent pursuingModeComponent, in LocalTransform localTransform, in TeamComponent team, in DynamicBuffer<ModelsBuffer> modelsBuf,
        EnabledRefRW<PursuingModeComponent> pursuingModeEnabledRefRW, EnabledRefRW<BattleModeComponent> battleModeEnabledRefRW, in UnitTypeComponent unitType, AttackPrioritiesAspect attackPriorities, ref MovementComponent movement, ref ReloadComponent reload, Entity entity)
    {
        double bestScore = double.MinValue;
        Entity bestScoreEntity = Entity.Null;

        bool hasPursueTarget = false;

        bool notHaveOnMoveReload = (vehicleLookup.HasComponent(entity) || deployLookup.HasComponent(entity));

        foreach (Entity potentialTarget in potentialTargetsArr)
        {
            //Check if they are in different teams
            if (teamLookup[potentialTarget].teamInd - team.teamInd == 0)
                continue;
            //Check if target is visible
            if ((team.teamInd & visibilityLookup[potentialTarget].visibleToTeams) == 0)
                continue;

            double curScore = 0;

            float distanceToPotTargetSq = math.distancesq(localTransform.Position, localToWorldLookup[potentialTarget].Position);
            float targetHpPercentage = (hpLookup[potentialTarget].curHp / hpLookup[potentialTarget].maxHp) * 100;
            if (modeSettings.isAutoTrigger)
                //Debug.Log($"maxHp = {targetHpPercentage} |||| unitTypes = {(uint)unitType.value & modeSettings.autoTriggerUnitTypes} |||| distToTar = {distanceToPotTargetSq} |||| maxDist = {modeSettings.autoTriggerRadiusSq}");

            #region auto-trigger and radius
            if ((modeSettings.isAutoTrigger /*|| (modeSettings.autoTriggerStatic && (!movement.hasMoveTarget || !movement.isAbleToMove))*/) // <-- suppose autoTriggerStatic is not so useful option for player
                &&
                distanceToPotTargetSq < modeSettings.autoTriggerRadiusSq
                &&
                targetHpPercentage <= modeSettings.autoTriggerMaxHpPercent
                &&
                (((uint)unitType.value & modeSettings.autoTriggerUnitTypes) != 0 || modeSettings.autoTriggerUnitTypes == 0))
            {
                curScore += 100000; // such targets has higher priority than other (auto-trigger has more priority than usual attack)
                hasPursueTarget = true;
                Debug.Log("Changed mode to PursueMode");
            }
            else if (distanceToPotTargetSq > attackChars.radiusSq)
                continue; // if pot target is not in any of the radiuses - going to next potential target
            #endregion

            #region Modifiers Calculations
            curScore -= distanceToPotTargetSq * attackPriorities.DistanceModifier;
            curScore += (100 - targetHpPercentage) * attackPriorities.MinHpModifier;
            foreach(var unitPrior in attackPriorities.unitsPriorities)
            {
                if ((unitPrior.types & (uint)unitType.value) != 0)
                {
                    curScore += unitPrior.modifier * 50; // 50 is a base for modifier multiplication (so it has some weight; not just +1 to curScore)
                    break;
                }
            }
            ///TODO: ZonePriorities
            #endregion

            if (curScore > bestScore)
            {
                bestScore = curScore;
                bestScoreEntity = potentialTarget;
            }
        }

        attackChars.target = bestScoreEntity;

        if (hasPursueTarget)
        {
            battleModeEnabledRefRW.ValueRW = false;
            pursuingModeEnabledRefRW.ValueRW = true;
            pursuingModeComponent.moveTargetBeforePursue = movement.target;

            if (!movement.hasMoveTarget)
            {
                //Start Movement Animations
                foreach (var modelBufElem in modelsBuf)
                {
                    RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                    animCmd.ValueRW.ClipIndex = moveClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                    animCmd.ValueRW.Cmd = AnimationCmd.SetPlayForever;
                }
                movement.hasMoveTarget = true;
            }

            if (!notHaveOnMoveReload)
            {
                movement.curDebaff = movement.movement_SoM_Debaff;
                reload.curDebaff = reload.reload_SoM_Debaff;
                reload.isShootingOnMoveAnim = true;
            }

            pursuingModeComponent.Target = attackChars.target;

        }
        //else <--- no need in this as such modeChange is done in the PursuingJob
        //{
        //    battleModeEnabledRefRW.ValueRW = true;
        //    pursuingModeEnabledRefRW.ValueRW = false;
        //}

    }
}


/// <summary> Creates attack requests if needed and do connected to this things (animation, etc.) </summary>
/// <remarks> That Job is for NON Deployable units! </remarks>
//[BurstCompile]
public partial struct _CreateUsualAttackRequestsJob : IJobChunk
{
    [ReadOnly] public ComponentLookup<LocalToWorld> localToWorldLookup;

    public EntityCommandBuffer.ParallelWriter ecb;

    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<AnimationCmdData> animCmdLookup;
    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<AnimationStateData> animStateLookup;
    [ReadOnly] public NativeArray<AnimDbEntry> attackClips;
    [ReadOnly] public NativeArray<AnimDbEntry> reloadClips;
    [ReadOnly] public NativeArray<AnimDbEntry> moveClips;
    [ReadOnly] public NativeArray<AnimDbEntry> restClips;

    [ReadOnly] public ComponentTypeHandle<AttackCharsComponent> attackCharsTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<PursuingModeComponent> pursuingModeSettsTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<BattleModeComponent> battleModeSetsTypeHandleRO;
    public ComponentTypeHandle<MovementComponent> movementTypeHandle;
    [ReadOnly] public ComponentTypeHandle<LocalTransform> transformTypeHandleRO;
    public ComponentTypeHandle<ReloadComponent> reloadTypeHandle;
    public ComponentTypeHandle<RotationComponent> rotationTypeHandle;
    public EntityTypeHandle entityTypeHandle;
    [ReadOnly] public BufferTypeHandle<ModelsBuffer> modelsBuffTypeHandle;
    [ReadOnly] public BufferTypeHandle<AttackModelsBuffer> attackModelsBuffTypeHandle;


    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        unsafe
        {
            AttackCharsComponent* attackChars = chunk.GetComponentDataPtrRO(ref attackCharsTypeHandleRO);
            PursuingModeComponent* pursuingModes = chunk.GetComponentDataPtrRO(ref pursuingModeSettsTypeHandleRO);
            BattleModeComponent* battleModeSetts = chunk.GetComponentDataPtrRO(ref battleModeSetsTypeHandleRO);
            MovementComponent* movements = chunk.GetComponentDataPtrRW(ref movementTypeHandle);
            LocalTransform* transforms = chunk.GetComponentDataPtrRO(ref transformTypeHandleRO);
            ReloadComponent* reloads = chunk.GetComponentDataPtrRW(ref reloadTypeHandle);
            RotationComponent* rotations = chunk.GetComponentDataPtrRW(ref rotationTypeHandle);
            BufferAccessor<ModelsBuffer> modelsBufs = chunk.GetBufferAccessor(ref modelsBuffTypeHandle);
            Entity* entities = chunk.GetEntityDataPtrRO(entityTypeHandle);

            BufferAccessor<AttackModelsBuffer> attackModelsBufs = new BufferAccessor<AttackModelsBuffer>();
            bool hasSeparateAttackModels = chunk.Has(ref attackModelsBuffTypeHandle);
            if (hasSeparateAttackModels)
                attackModelsBufs = chunk.GetBufferAccessor(ref attackModelsBuffTypeHandle);

            

            for (int i = 0; i < chunk.Count; i++)
            {
                //if has some target -> shoot
                if (attackChars[i].target != Entity.Null && !battleModeSetts[i].shootingDisabled)
                {
                    if (!reloads[i].isReloaded()) // if not reloaded -> return
                        continue;

                    bool isPursuingEnabled = chunk.IsComponentEnabled(ref pursuingModeSettsTypeHandleRO, i);
                    if (isPursuingEnabled &&
                        (pursuingModes[i].maxShootDistanceSq < math.distancesq(localToWorldLookup[pursuingModes[i].Target].Position, transforms[i].Position) ||
                        attackChars[i].radiusSq < math.distancesq(localToWorldLookup[pursuingModes[i].Target].Position, transforms[i].Position)))
                    {//if pursuing and not close to target enough -> not shoot
                        continue;
                    }

                    if (!isPursuingEnabled && attackChars[i].radiusSq < math.distancesq(localToWorldLookup[attackChars[i].target].Position, transforms[i].Position))
                        continue;

                    if (battleModeSetts[i].shootingOnMove || hasSeparateAttackModels || isPursuingEnabled) //if can move while reload -> temp component added
                        ecb.AddComponent(unfilteredChunkIndex, entities[i], new NotAbleToMoveForTimeRqstComponent 
                        { 
                            passedTime = 0, 
                            targetTime = reloads[i].shootAnimLen
                        });

                    if (!hasSeparateAttackModels)//if no turret -> stop and turn to the enemy
                    {
                        movements[i].isAbleToMove = false;
                        //Rotate to target
                        rotations[i].newRotTarget =
                            quaternion.LookRotationSafe(localToWorldLookup[attackChars[i].target].Position - transforms[i].Position, transforms[i].Up());
                    }

                    //Create Attack Request
                    Entity attackRequest = ecb.CreateEntity(unfilteredChunkIndex);
                    ecb.AddComponent(unfilteredChunkIndex, attackRequest, new AttackRequestComponent
                    {
                        target = attackChars[i].target,
                        damage = attackChars[i].damage,
                        attackerPos = transforms[i].Position
                    });

                    //Play Attack Anim
                    if (!hasSeparateAttackModels) //if has no turret -> anim whole body
                    {
                        foreach (var modelBufElem in modelsBufs[i])
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            byte restIdx = restClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animStateLookup.GetRefRW(modelBufElem.model).ValueRW.ForeverClipIndex = restIdx;
                            animCmd.ValueRW.ClipIndex = attackClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                        }
                    }
                    else //if has a turret -> anim only turret
                    {
                        foreach (var modelBufElem in attackModelsBufs[i])
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            //byte reloadIdx = reloadClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            //animStateLookup.GetRefRW(modelBufElem.model).ValueRW.ForeverClipIndex = reloadIdx;
                            animCmd.ValueRW.ClipIndex = attackClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                        }
                    }

                    reloads[i].curBullets--;
                    reloads[i].bulletReloadElapsed = 0;

                    if (isPursuingEnabled)
                        pursuingModes[i].dropTimeElapsed = 0;
                }
                else // this means unit doesn't have a target to shoot
                {
                    if (!movements[i].isAbleToMove && // if not able to move
                        reloads[i].isReloaded()) // and reloaded
                    {// enable to move and adjust the anims
                        if (movements[i].hasMoveTarget)
                            foreach (var modelBufElem in modelsBufs[i])
                            {
                                RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                                animCmd.ValueRW.ClipIndex = moveClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                                animCmd.ValueRW.Cmd = AnimationCmd.SetPlayForever;
                            }
                        else
                            foreach (var modelBufElem in modelsBufs[i])
                            {
                                RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                                animCmd.ValueRW.ClipIndex = restClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                                animCmd.ValueRW.Cmd = AnimationCmd.SetPlayForever;
                            }
                        movements[i].isAbleToMove = true;
                    }
                }
            }
        }
    }
}


///<summary> Creates attack requests if needed and do connected to this things (animation, etc.) </summary>
/// <remarks> That Job is for Deployable units! </remarks>
[BurstCompile]
public partial struct _CreateDeployableAttackRequestsJob : IJobChunk
{
    [ReadOnly] public ComponentLookup<LocalToWorld> localToWorldLookup;

    public EntityCommandBuffer.ParallelWriter ecb;

    public float deltaTime;

    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<AnimationCmdData> animCmdLookup;
    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<AnimationStateData> animStateLookup;
    [ReadOnly] public NativeArray<AnimDbEntry> attackClips;
    [ReadOnly] public NativeArray<AnimDbEntry> reloadClips;
    [ReadOnly] public NativeArray<AnimDbEntry> deployClips;
    [ReadOnly] public NativeArray<AnimDbEntry> undeployClips;
    [ReadOnly] public NativeArray<AnimDbEntry> rest_deployedClips;

    public ComponentTypeHandle<Deployable> deployableTypeHandle;
    [ReadOnly] public ComponentTypeHandle<AttackCharsComponent> attackCharsTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<BattleModeComponent> battleModeSettsSettsTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<PursuingModeComponent> pursuingModeSettsTypeHandleRO;
    public ComponentTypeHandle<MovementComponent> movementTypeHandle;
    [ReadOnly] public ComponentTypeHandle<LocalTransform> transformTypeHandleRO;
    public ComponentTypeHandle<ReloadComponent> reloadTypeHandle;
    public ComponentTypeHandle<RotationComponent> rotationTypeHandle;
    [ReadOnly] public BufferTypeHandle<ModelsBuffer> modelsBuffTypeHandle;
    [ReadOnly] public BufferTypeHandle<AttackModelsBuffer> attackModelsBuffTypeHandle;


    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        unsafe
        {
            Deployable* deployables = chunk.GetComponentDataPtrRW(ref deployableTypeHandle);
            AttackCharsComponent* attackChars = chunk.GetComponentDataPtrRO(ref attackCharsTypeHandleRO);
            BattleModeComponent* battleModeSetts = chunk.GetComponentDataPtrRO(ref battleModeSettsSettsTypeHandleRO);
            PursuingModeComponent* pursuingModes = chunk.GetComponentDataPtrRO(ref pursuingModeSettsTypeHandleRO);
            MovementComponent* movements = chunk.GetComponentDataPtrRW(ref movementTypeHandle);
            LocalTransform* transforms = chunk.GetComponentDataPtrRO(ref transformTypeHandleRO);
            ReloadComponent* reloads = chunk.GetComponentDataPtrRW(ref reloadTypeHandle);
            RotationComponent* rotations = chunk.GetComponentDataPtrRW(ref rotationTypeHandle);
            BufferAccessor<ModelsBuffer> modelsBufs = chunk.GetBufferAccessor(ref modelsBuffTypeHandle);

            BufferAccessor<AttackModelsBuffer> attackModelsBufs = new BufferAccessor<AttackModelsBuffer>();
            bool hasSeparateAttackModels = chunk.Has(ref attackModelsBuffTypeHandle);
            if (hasSeparateAttackModels)
                attackModelsBufs = chunk.GetBufferAccessor(ref attackModelsBuffTypeHandle);



            for (int i = 0; i < chunk.Count; i++)
            {
                if (attackChars[i].target != Entity.Null && !battleModeSetts[i].shootingDisabled)
                {
                    deployables[i].waitingTimeCur = 0; // remove to other place (is it possible to null this value in some if - not every frame when unit has a target?)

                    bool isPursuingEnabled = chunk.IsComponentEnabled(ref pursuingModeSettsTypeHandleRO, i);
                    if (isPursuingEnabled &&
                        pursuingModes[i].maxShootDistanceSq < math.distancesq(localToWorldLookup[pursuingModes[i].Target].Position, transforms[i].Position))
                    {//if pursuing and not close to target enough -> not shoot
                        continue;
                    }

                    //if Undeployed, than start Deploying
                    if (!deployables[i].deployedState)
                    {
                        deployables[i].deployedState = true;
                        movements[i].isAbleToMove = false;

                        //Set Deploy anim PlayOnce
                        foreach (var modelBufElem in modelsBufs[i])
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            byte rest_deployed_idx = rest_deployedClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animStateLookup.GetRefRW(modelBufElem.model).ValueRW.ForeverClipIndex = rest_deployed_idx;
                            animCmd.ValueRW.ClipIndex = deployClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                        }
                    }
                    //If fully Deployed and Reloaded -> create Attack Rqst
                    else if (deployables[i].deployTimeElapsed >= deployables[i].deployTime && reloads[i].isReloaded())
                    {
                        if (!hasSeparateAttackModels)
                            //Rotate to target
                            rotations[i].newRotTarget = quaternion.LookRotationSafe(localToWorldLookup[attackChars[i].target].Position - transforms[i].Position, transforms[i].Up());

                        //Create Attack Request
                        Entity attackRequest = ecb.CreateEntity(unfilteredChunkIndex);
                        ecb.AddComponent(unfilteredChunkIndex, attackRequest, new AttackRequestComponent
                        {
                            target = attackChars[i].target,
                            damage = attackChars[i].damage,
                            attackerPos = transforms[i].Position
                        });

                        //Debug.Log("1");
                        //Play Attack Anim
                        if (!hasSeparateAttackModels)
                        {
                            foreach (var modelBufElem in modelsBufs[i])
                            {
                                //Debug.Log("2");
                                
                                RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                                animCmd.ValueRW.ClipIndex = attackClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                                //Debug.Log(animStateLookup[modelBufElem.model].ModelIndex);
                                //Debug.Log(attackClips[animStateLookup[modelBufElem.model].ModelIndex].ClipName);
                                //Debug.Log(attackClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex);
                                animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                            }
                        }
                        else
                        {
                            foreach (var modelBufElem in attackModelsBufs[i])
                            {
                                RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                                animCmd.ValueRW.ClipIndex = attackClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                                animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                            }
                        }

                        reloads[i].curBullets--;
                        reloads[i].bulletReloadElapsed = 0;

                        if (isPursuingEnabled)
                            pursuingModes[i].dropTimeElapsed = 0;
                    }
                }
                //If has no target and another point to Move -> undeploy and move after waitingTime elapsed
                else if (movements[i].hasMoveTarget)
                {
                    //Update waiting time
                    if (deployables[i].waitingTimeCur < deployables[i].waitingTimeMax)
                        deployables[i].waitingTimeCur += deltaTime;
                    //if waiting time elapsed -> undeploy and move
                    if (deployables[i].deployedState && deployables[i].waitingTimeCur >= deployables[i].waitingTimeMax && // if deployed and waited enough time
                        reloads[i].isReloaded()) // and reloaded
                    {// then undelpoy
                        deployables[i].deployedState = false;

                        //Set Undeploy anim PlayOnce
                        foreach (var modelBufElem in modelsBufs[i])
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            animCmd.ValueRW.ClipIndex = undeployClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animCmd.ValueRW.Cmd = AnimationCmd.PlayOnce;
                        }
                    }
                }
            }
        }
    }
}




///<summary> Updates some pursuing info for all units with such mode enabled </summary>
[BurstCompile]
public partial struct PursuingJob : IJobChunk
{
    const float MIN_DIST_TO_UNIT = 4;
    public float deltaTime;
    
    [ReadOnly] public ComponentLookup<LocalToWorld> localToWorldLookup;
    [ReadOnly] public ComponentLookup<VisibilityComponent> visibilityLookup;

    public ComponentTypeHandle<PursuingModeComponent> pursuingModeSettsTypeHandle;
    public ComponentTypeHandle<BattleModeComponent> battleModeSetsTypeHandle;
    public ComponentTypeHandle<MovementComponent> movementTypeHandle;
    [ReadOnly] public ComponentTypeHandle<LocalTransform> transformTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<ReloadComponent> reloadTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<TeamComponent> teamTypeHandleRO;
    [ReadOnly] public ComponentTypeHandle<Deployable> deployableTypeHandleRO;
    [ReadOnly] public BufferTypeHandle<AttackModelsBuffer> attackModelsBuffTypeHandle;
    [ReadOnly] public BufferTypeHandle<ModelsBuffer> modelsBuffTypeHandle;

    public ComponentLookup<AnimationCmdData> animCmdLookup;
    [ReadOnly] public ComponentLookup<AnimationStateData> animStateLookup;
    [ReadOnly] public NativeArray<AnimDbEntry> moveClips;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        unsafe
        {
            PursuingModeComponent* pursuingSetts = chunk.GetComponentDataPtrRW(ref pursuingModeSettsTypeHandle);
            BattleModeComponent* battleModeSetts = chunk.GetComponentDataPtrRW(ref battleModeSetsTypeHandle);
            MovementComponent* movements = chunk.GetComponentDataPtrRW(ref movementTypeHandle);
            LocalTransform* transforms = chunk.GetComponentDataPtrRO(ref transformTypeHandleRO);
            ReloadComponent* reloads = chunk.GetComponentDataPtrRO(ref reloadTypeHandleRO);
            TeamComponent* teams = chunk.GetComponentDataPtrRO(ref teamTypeHandleRO);
            BufferAccessor<ModelsBuffer> modelsBufs = chunk.GetBufferAccessor(ref modelsBuffTypeHandle);
            bool notHaveOnMoveReload = (chunk.Has(ref attackModelsBuffTypeHandle) || chunk.Has(ref deployableTypeHandleRO));

            ChunkEntityEnumerator chunkEnum = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (chunkEnum.NextEntityIndex(out int i))
            {
                float3 targetPos = localToWorldLookup[pursuingSetts[i].Target].Position;
                float distToTargetSq = math.distancesq(transforms[i].Position, targetPos);
                //if (reloads[i].isReloaded())
                //    pursuingSetts[i].dropTimeElapsed += deltaTime;
                //Check if the distance to target is too big OR too long time pusrueing OR target is not visible anymore 
                if (distToTargetSq > pursuingSetts[i].dropDistanceSq ||
                    //pursuingSetts[i].dropTimeElapsed > pursuingSetts[i].dropTime ||
                    (visibilityLookup[pursuingSetts[i].Target].visibleToTeams & teams[i].teamInd) == 0)
                {// Turn off pursuing mode and return to BattleMode
                    Debug.Log($"Stopped pursuing: distToTargetSq = {distToTargetSq} || dropTimeElapsed = {pursuingSetts[i].dropTimeElapsed} || visibleToTeams = {visibilityLookup[pursuingSetts[i].Target].visibleToTeams}");

                    chunk.SetComponentEnabled(ref battleModeSetsTypeHandle, i, true);
                    chunk.SetComponentEnabled(ref pursuingModeSettsTypeHandle, i, false);
                    movements[i].target = pursuingSetts[i].moveTargetBeforePursue;
                    if (!battleModeSetts[i].shootingOnMove)
                    {
                        movements[i].curDebaff = 0;
                        reloads[i].curDebaff = 0;
                        reloads[i].isShootingOnMoveAnim = false;
                    }
                    continue;
                }

                //Update target of moving
                if (distToTargetSq >= MIN_DIST_TO_UNIT * MIN_DIST_TO_UNIT)
                {
                    if (!movements[i].hasMoveTarget)
                    {
                        movements[i].hasMoveTarget = true;
                        if (!notHaveOnMoveReload)
                        {
                            reloads[i].isShootingOnMoveAnim = true;
                            movements[i].curDebaff = movements[i].movement_SoM_Debaff;
                            reloads[i].curDebaff = reloads[i].reload_SoM_Debaff;
                        }
                        //Move animation
                        foreach (var modelBufElem in modelsBufs[i])
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            byte restIdx = moveClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animStateLookup.GetRefRW(modelBufElem.model).ValueRW.ForeverClipIndex = restIdx;
                        }
                    }
                    movements[i].target = targetPos;
                }
                else if (movements[i].hasMoveTarget)
                {
                    movements[i].hasMoveTarget = false;
                    movements[i].curDebaff = 0;
                    reloads[i].curDebaff = 0;
                    reloads[i].isShootingOnMoveAnim = false;
                    ////Rest animation  <---- should be turned on in MovementSystem
                    //foreach (var modelBufElem in modelsBufs[i])
                    //{
                    //    RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                    //    byte restIdx = restClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                    //    animStateLookup.GetRefRW(modelBufElem.model).ValueRW.ForeverClipIndex = restIdx;
                    //}
                }
            }
        }
    }
}



///<summary> Used in temporary "event"-entity for creating an attack request </summary>
public struct AttackRequestComponent : IComponentData
{
    public Entity target;
    public float damage;
    public float3 attackerPos;
}
