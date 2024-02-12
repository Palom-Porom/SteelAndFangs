using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Unity.Collections;
using AnimCooker;
using System.Net.Security;
using System;
using Unity.Jobs;
using static AnimDb;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.UIElements;
using Unity.Burst.Intrinsics;
using Unity.Entities.UniversalDelegates;

[UpdateInGroup(typeof(UnitsSystemGroup))]
[BurstCompile]
public partial struct MovementSystem : ISystem, ISystemStartStop
{
    ComponentLookup<LocalTransform> transformLookup;
    
    ComponentLookup<AnimationCmdData> animCmdLookup;
    ComponentLookup<AnimationStateData> animStateLookup;
    NativeArray<AnimDbEntry> restClips;

    EntityQuery movementQuery;

    ComponentTypeHandle<LocalTransform> transformTypeHandle;
    ComponentTypeHandle<MovementComponent> movementTypeHandle;
    ComponentTypeHandle<RotationComponent> rotationTypeHandle;
    BufferTypeHandle<MovementCommandsBuffer> movementCommandsBuffTypeHandle;
    BufferTypeHandle<ModelsBuffer> modelsBuffTypeHandle;

    ComponentTypeHandle<VehicleMovementComponent> vehicleTypeHandle;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate<MovementComponent>();
        state.RequireForUpdate<AnimDbRefData>();

        transformLookup = state.GetComponentLookup<LocalTransform>();

        animCmdLookup = state.GetComponentLookup<AnimationCmdData>();
        animStateLookup = state.GetComponentLookup<AnimationStateData>();

        movementQuery = new EntityQueryBuilder(Allocator.Temp).
            WithAllRW<MovementComponent, LocalTransform>().
            WithAllRW<RotationComponent, MovementCommandsBuffer>().
            WithAny<ModelsBuffer, VehicleMovementComponent>().
            Build(ref state);

        transformTypeHandle = SystemAPI.GetComponentTypeHandle<LocalTransform>();
        movementTypeHandle = SystemAPI.GetComponentTypeHandle<MovementComponent>();
        rotationTypeHandle = SystemAPI.GetComponentTypeHandle<RotationComponent>();
        movementCommandsBuffTypeHandle = SystemAPI.GetBufferTypeHandle<MovementCommandsBuffer>();
        modelsBuffTypeHandle = SystemAPI.GetBufferTypeHandle<ModelsBuffer>(true);

        vehicleTypeHandle = SystemAPI.GetComponentTypeHandle<VehicleMovementComponent>();
    }

    public void OnStartRunning(ref SystemState state)
    {
        restClips = SystemAPI.GetSingleton<AnimDbRefData>().FindClips("Rest");        
    }

    public void OnStopRunning(ref SystemState state)
    {
        
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) 
    {
        transformLookup.Update(ref state);

        animCmdLookup.Update(ref state);
        animStateLookup.Update(ref state);

        transformTypeHandle.Update(ref state);
        movementTypeHandle.Update(ref state);
        rotationTypeHandle.Update(ref state);
        movementCommandsBuffTypeHandle.Update(ref state);
        modelsBuffTypeHandle.Update(ref state);
        vehicleTypeHandle.Update(ref state);


        #region MovementJob
        //JobHandle movementJobHandle = new MovementJob
        //{
        //    deltaTime = SystemAPI.Time.DeltaTime,
        //    collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,

        //    animCmdLookup = animCmdLookup,
        //    animStateLookup = animStateLookup,
        //    restClips = restClips
        //}.Schedule(state.Dependency);

        JobHandle vehicleRotationJobHandle = new VehicleRotationToTargetJob
        {
            deltaTime = SystemAPI.Time.DeltaTime
        }.Schedule(state.Dependency);

        JobHandle movementJobHandle = new MovementJob
        {
            deltaTime = SystemAPI.Time.DeltaTime,
            collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld,

            animCmdLookup = animCmdLookup,
            animStateLookup = animStateLookup,
            restClips = restClips,

            transformTypeHandle = transformTypeHandle,
            movementTypeHandle = movementTypeHandle,
            rotationTypeHandle = rotationTypeHandle,
            movementCommandsBuffTypeHandle = movementCommandsBuffTypeHandle,
            modelsBuffTypeHandle = modelsBuffTypeHandle,

            vehicleTypeHandle = vehicleTypeHandle,
        }.Schedule(movementQuery, vehicleRotationJobHandle);
        #endregion

        #region RotationJobs
        JobHandle rotationJobHandle = new RotationJob
        {
            deltaTime = SystemAPI.Time.DeltaTime
        }.Schedule(movementJobHandle); // Be careful changing these parallel jobs

        JobHandle attackRotationJobHandle = new AttackRotationJob 
        {
            deltaTime = SystemAPI.Time.DeltaTime,
            transformLookup = transformLookup
        }.Schedule(movementJobHandle); // Be careful changing these parallel jobs

        


        state.Dependency = JobHandle.CombineDependencies(attackRotationJobHandle, rotationJobHandle);
        #endregion
    }


}

/// <summary>
/// Moves all entities with MovementComponent to their target over the terrain
/// </summary>
//[BurstCompile]
//public partial struct MovementJob : IJobEntity
//{
//    public float deltaTime;
//    [ReadOnly] public CollisionWorld collisionWorld;

//    public ComponentLookup<AnimationCmdData> animCmdLookup;
//    [ReadOnly] public ComponentLookup<AnimationStateData> animStateLookup;
//    [ReadOnly] public NativeArray<AnimDbEntry> restClips;

//    public void Execute(ref LocalTransform transform, ref MovementComponent movementComponent, ref RotationToTargetComponent rotation,
//        DynamicBuffer<MovementCommandsBuffer> movementCommandsBuffer, in DynamicBuffer<ModelsBuffer> modelsBuf)
//    {
//        if (!movementComponent.isAbleToMove)
//            return;

//        if (!movementComponent.hasMoveTarget)
//            return;

//        if (math.distancesq(movementComponent.target, transform.Position) < (deltaTime * movementComponent.speed) / 2)
//        {
//            //Has next target to move?
//            if (movementCommandsBuffer.Length != 0)
//            {
//                movementComponent.target = movementCommandsBuffer[0].target;
//                movementCommandsBuffer.RemoveAt(0);
//            }
//            else
//            {
//                movementComponent.hasMoveTarget = false;
//                foreach (var modelBufElem in modelsBuf)
//                {
//                    RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
//                    animCmd.ValueRW.ClipIndex = restClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
//                    animCmd.ValueRW.Cmd = AnimationCmd.SetPlayForever;
//                }
//                return;
//            } 
//        }

//        float3 tempDir = movementComponent.target - transform.Position;
//        tempDir.y = 0;
//        tempDir = math.normalize(tempDir);
//        float speed = deltaTime * movementComponent.speed * (1 - movementComponent.curDebaff);
//        if (speed < 0)
//            Debug.Log("ERROR: Movement debuff is higher than 1!");

//        CollisionFilter filter = new CollisionFilter
//        {
//            BelongsTo = (uint)layers.Everything,
//            CollidesWith = (uint)layers.Terrain,
//            GroupIndex = 0
//        };
//        float3 pointDistancePos = transform.Position + tempDir * speed;
//        var pointDistanceInput = new PointDistanceInput { Filter = filter, Position = pointDistancePos, MaxDistance = 1.5f };
//        if (collisionWorld.CalculateDistance(pointDistanceInput, out DistanceHit closestHit))
//        {
//            if (closestHit.SurfaceNormal.y < 0f)
//                closestHit.SurfaceNormal = -closestHit.SurfaceNormal;

//            rotation.newRotTarget = quaternion.LookRotationSafe(closestHit.Position - transform.Position, closestHit.SurfaceNormal); // Update rotation target

//            transform.Position = closestHit.Position; // Update position
//        }
//        else
//        {
//            Debug.Log($"ERROR: Terrain was not found near unit!");
//        }
//    }
//}



[BurstCompile]
public partial struct MovementJob : IJobChunk
{
    public float deltaTime;
    [ReadOnly] public CollisionWorld collisionWorld;

    public ComponentLookup<AnimationCmdData> animCmdLookup;
    [ReadOnly] public ComponentLookup<AnimationStateData> animStateLookup;
    [ReadOnly] public NativeArray<AnimDbEntry> restClips;

    public ComponentTypeHandle<LocalTransform> transformTypeHandle;
    public ComponentTypeHandle<MovementComponent> movementTypeHandle;
    public ComponentTypeHandle<RotationComponent> rotationTypeHandle;
    public BufferTypeHandle<MovementCommandsBuffer> movementCommandsBuffTypeHandle;
    public BufferTypeHandle<ModelsBuffer> modelsBuffTypeHandle;
    
    public ComponentTypeHandle<VehicleMovementComponent> vehicleTypeHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
    {
        NativeArray<LocalTransform> transforms = chunk.GetNativeArray(ref transformTypeHandle);
        NativeArray<MovementComponent> movements = chunk.GetNativeArray(ref movementTypeHandle);
        NativeArray<RotationComponent> rotations = chunk.GetNativeArray(ref rotationTypeHandle);
        BufferAccessor<MovementCommandsBuffer> movementCommandsBuffs = chunk.GetBufferAccessor(ref movementCommandsBuffTypeHandle);
        BufferAccessor<ModelsBuffer> modelsBuffs = chunk.GetBufferAccessor(ref modelsBuffTypeHandle);

        unsafe
        {
            void* transformsPtr = transforms.GetUnsafePtr();
            void* movemetsPtr = movements.GetUnsafePtr();
            void* rotationsPtr = rotations.GetUnsafePtr();
            var a = chunk.GetRequiredComponentDataPtrRW(ref movementTypeHandle);
            
            NativeArray<VehicleMovementComponent> vehicles = new NativeArray<VehicleMovementComponent>();
            void* vehiclesPtr = null;
            bool isVehicle = chunk.Has(ref vehicleTypeHandle);
            if (isVehicle)
            {
                vehicles = chunk.GetNativeArray(ref vehicleTypeHandle);
                vehiclesPtr = vehicles.GetUnsafePtr();
            }

            ChunkEntityEnumerator chunkEnum = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

            while (chunkEnum.NextEntityIndex(out int i))
            {
                if (!movements[i].isAbleToMove)
                    continue;

                if (!movements[i].hasMoveTarget)
                    continue;

                if (math.distancesq(movements[i].target, transforms[i].Position) < math.max(deltaTime * movements[i].speed, 0.1f)) // has got to the current target?
                {
                    UnsafeUtility.ArrayElementAsRef<LocalTransform>(transformsPtr, i).Position = movements[i].target;
                    if (movementCommandsBuffs[i].Length != 0) //Has next target to move?
                    {
                        UnsafeUtility.ArrayElementAsRef<MovementComponent>(movemetsPtr, i).target = movementCommandsBuffs[i][0].target;
                        movementCommandsBuffs[i].RemoveAt(0);
                    }
                    else
                    {
                        UnsafeUtility.ArrayElementAsRef<MovementComponent>(movemetsPtr, i).hasMoveTarget = false;
                        foreach (var modelBufElem in modelsBuffs[i])
                        {
                            RefRW<AnimationCmdData> animCmd = animCmdLookup.GetRefRW(modelBufElem.model);
                            animCmd.ValueRW.ClipIndex = restClips[animStateLookup[modelBufElem.model].ModelIndex].ClipIndex;
                            animCmd.ValueRW.Cmd = AnimationCmd.SetPlayForever;
                        }
                        continue;
                    }
                }

                float speed = deltaTime * movements[i].speed * (1 - movements[i].curDebaff);
                float3 tempDir;
                if (isVehicle)
                {
                    tempDir = vehicles[i].temporaryTarget - transforms[i].Position;
                    //speed *= UnsafeUtility.ArrayElementAsRef<VehicleMovementComponent>(vehiclesPtr, i).curMovementSpeedMultiplier;
                }
                else
                {
                    tempDir = movements[i].target - transforms[i].Position;
                }
                tempDir.y = 0;
                tempDir = math.normalize(tempDir);
                //Debug.Log(tempDir);
                if (speed < 0)
                    Debug.Log("ERROR: Movement debuff is higher than 1!");

                CollisionFilter filter = new CollisionFilter
                {
                    BelongsTo = (uint)layers.Everything,
                    CollidesWith = (uint)layers.Terrain,
                    GroupIndex = 0
                };
                float3 pointDistancePos = transforms[i].Position + tempDir * speed;
                var pointDistanceInput = new PointDistanceInput { Filter = filter, Position = pointDistancePos, MaxDistance = 2.5f };
                if (collisionWorld.CalculateDistance(pointDistanceInput, out DistanceHit closestHit))
                {
                    if (closestHit.SurfaceNormal.y < 0f)
                        closestHit.SurfaceNormal = -closestHit.SurfaceNormal;

                    UnsafeUtility.ArrayElementAsRef<RotationComponent>(rotationsPtr, i).newRotTarget = 
                        quaternion.LookRotationSafe(closestHit.Position - transforms[i].Position, closestHit.SurfaceNormal); // Update rotation target
                    //Debug.Log($"{closestHit.Position - transforms[i].Position}; ||||| {rotations[i].newRotTarget} --- {rotations[i].curRotTarget}");
                    UnsafeUtility.ArrayElementAsRef<LocalTransform>(transformsPtr, i).Position = closestHit.Position; // Update position
                    if (isVehicle)
                    {
                        float3 dirToTarget = closestHit.Position - transforms[i].Position;
                        dirToTarget.y = 0;
                        dirToTarget = math.normalize(dirToTarget);
                        UnsafeUtility.ArrayElementAsRef<VehicleMovementComponent>(vehiclesPtr, i).curTargetDir = tempDir;
                    }
                    //if (isVehicle)
                    //{
                    //    float3 dirToTarget = movements[i].target - transforms[i].Position;
                    //    dirToTarget.y = 0;
                    //    dirToTarget = math.normalize(dirToTarget);
                    //    UnsafeUtility.ArrayElementAsRef<VehicleMovementComponent>(vehiclesPtr, i).curTargetDir = dirToTarget;
                    //    //UnsafeUtility.ArrayElementAsRef<VehicleMovementComponent>(vehiclesPtr, i).newTargetDir = dirToTarget;
                    //    UnsafeUtility.ArrayElementAsRef<VehicleMovementComponent>(vehiclesPtr, i).rotationDiff =
                    //        math.mul(quaternion.LookRotationSafe(dirToTarget, math.up()), math.inverse(transforms[i].Rotation));
                    //    //UnsafeUtility.ArrayElementAsRef<RotationToTargetComponent>(rotationsPtr, i).newRotTarget = math.mul(rotations[i].newRotTarget, vehicles[i].rotationDiff);
                    //}

                    //UnsafeUtility.ArrayElementAsRef<LocalTransform>(transformsPtr, i).Position = closestHit.Position; // Update position
                }
                else
                {
                    Debug.Log($"ERROR: Terrain was not found near unit!");
                }
            }
        }
     }
}






//[WithNone(typeof(VehicleMovementComponent))]
public partial struct RotationJob : IJobEntity
{
    public float deltaTime;

    public void Execute(ref RotationComponent rotation, ref LocalTransform localTransform)
    {
        if (UtilityFuncs.Nearly_Equals(rotation.newRotTarget, rotation.curRotTarget))
        //if (rotation.newRotTarget.Equals(rotation.curRotTarget))
        {
            if (rotation.rotTimeElapsed < rotation.rotTime)
            {
                rotation.rotTimeElapsed += deltaTime;
                localTransform.Rotation = math.nlerp(rotation.initialRotation, rotation.newRotTarget, rotation.rotTimeElapsed / rotation.rotTime);
            }
        }
        else
        {
            rotation.rotTimeElapsed = deltaTime;
            rotation.initialRotation = localTransform.Rotation;
            rotation.curRotTarget = rotation.newRotTarget;
            localTransform.Rotation = math.nlerp(localTransform.Rotation, rotation.newRotTarget, deltaTime / rotation.rotTime);
        }
    }
}



public partial struct VehicleRotationToTargetJob : IJobEntity
{
    public float deltaTime;

    public void Execute(/*ref RotationToTargetComponent rotation,*/ ref LocalTransform localTransform, ref VehicleMovementComponent vehicleMovement, in MovementComponent movement)
    {
        var curForwardDir = vehicleMovement.curTargetDir;
        //var curForwardDir = math.mul(localTransform.Rotation, new float3(0, 0, 1));
        //curForwardDir.y = 0;
        //curForwardDir = math.normalize(curForwardDir);
        var curForwardAngle = /*math.degrees*/(math.acos(math.dot(new float3(1, 0, 0), curForwardDir)));
        if (curForwardDir.z < 0)
            curForwardAngle = math.radians(360) - curForwardAngle;

        float3 targetForwardDir = movement.target - localTransform.Position;
        targetForwardDir.y = 0;
        targetForwardDir = math.normalize(targetForwardDir);
        var targetForwardAngle = /*math.degrees*/(math.acos(math.dot(new float3(1, 0, 0), targetForwardDir)));
        if (targetForwardDir.z < 0)
            targetForwardAngle = math.radians(360) - targetForwardAngle;

        var diffAngle = targetForwardAngle - curForwardAngle;
        if (math.abs(targetForwardAngle - (curForwardAngle + math.radians(360))) < math.abs(diffAngle))
            diffAngle = targetForwardAngle - (curForwardAngle + math.radians(360));
        if (math.abs(targetForwardAngle - (curForwardAngle - math.radians(360))) < math.abs(diffAngle))
            diffAngle = targetForwardAngle - (curForwardAngle - math.radians(360));


        var initialCurForwardAngle = curForwardAngle;
        if (math.abs(diffAngle) > vehicleMovement.degreesPerSecond)
            curForwardAngle += math.sign(diffAngle) * vehicleMovement.degreesPerSecond;
        else
            curForwardAngle += diffAngle;
        //curForwardAngle += math.min(math.sign(diffAngle) * vehicleMovement.degreesPerSecond, diffAngle);
        vehicleMovement.temporaryTarget = new float3(math.cos(/*math.radians*/(curForwardAngle)), 0, math.sin(/*math.radians*/(curForwardAngle))) + localTransform.Position;

        //Debug.Log($"curForwardDir = {curForwardDir}; curForwardAngle = {math.degrees(curForwardAngle)};" +
        //    $" initialCurForwardAngle = {math.degrees(initialCurForwardAngle)};" +
        //    $" targetForwardDir = {targetForwardDir}; targetForwardAngle = {math.degrees(targetForwardAngle)}; curTargetDir = {vehicleMovement.curTargetDir};" +
        //    $"\n cos({math.degrees(curForwardAngle)}) = {math.cos((curForwardAngle))}; sin({math.degrees(curForwardAngle)}) = {math.sin((curForwardAngle))}" +
        //    $"\n initialCurForwardDir = {math.mul(localTransform.Rotation, new float3(0, 0, 1))}" +
        //    $"\n initialinitialCurForwardAngle = {math.degrees(math.acos(math.dot(new float3(1, 0, 0), curForwardDir)))}");

        //var targetLookingVector = math.mul(vehicleMovement.rotationDiff, new float3(1, 0, 0));
        //targetLookingVector.y = 0;
        //math.normalize(targetLookingVector);
        //var cos_DifferenceAngle = math.dot(new float3(1, 0, 0), targetLookingVector);

        //vehicleMovement.rotationDeltaY = math.abs(math.degrees(math.acos(cos_DifferenceAngle)));
        //vehicleMovement.UpdateMovementMultiplier();
        ////Debug.Log(vehicleMovement.curMovementSpeedMultiplier);

        //if (UtilityFuncs.Nearly_Equals(vehicleMovement.curTargetDir, vehicleMovement.newTargetDir, 0.1f))
        //{
        //    if (vehicleMovement.rotTimeElapsed < vehicleMovement.rotTime)
        //    {
        //        vehicleMovement.rotTimeElapsed += deltaTime;
        //        localTransform.Rotation = math.nlerp(vehicleMovement.initialRotation, vehicleMovement.curRotTarget, vehicleMovement.rotTimeElapsed / vehicleMovement.rotTime);
        //        //if (vehicleMovement.rotationDeltaY >= vehicleMovement.minRotationDeltaForEffect)
        //        //{
        //        //    vehicleMovement.DecreaseMovementMultiplier();
        //        //    Debug.Log($"Decreased - {vehicleMovement.curMovementSpeedMultiplier}");
        //        //}
        //        Debug.Log($"Kopim ugol --- rotTimeElapsed = {vehicleMovement.rotTimeElapsed}; percentageOfComplete = {vehicleMovement.rotTimeElapsed / vehicleMovement.rotTime}");
        //    }
        //    else
        //        Debug.Log("Nothing was Done");
        //    //else
        //    //{
        //    //    //if (vehicleMovement.rotationDeltaY != 0)
        //    //    //{
        //    //    //    vehicleMovement.IncreaseMovementMultiplier();
        //    //    //    Debug.Log($"Increased - {vehicleMovement.curMovementSpeedMultiplier}");
        //    //    //}
        //    //    //Debug.Log("Nothing was done");
        //    //}
        //}
        //else
        //{
        //    vehicleMovement.rotTimeElapsed = deltaTime;
        //    vehicleMovement.initialRotation = localTransform.Rotation;
        //    vehicleMovement.curRotTarget = math.mul(localTransform.Rotation, vehicleMovement.rotationDiff);
        //    vehicleMovement.rotTime = vehicleMovement.rotationDeltaY / vehicleMovement.degreesPerSecond;
        //    vehicleMovement.curTargetDir = vehicleMovement.newTargetDir;

        //    #region Vehicle rotation info update

        //    //quaternion rotationDiff = math.mul(rotation.curRotTarget, math.inverse(localTransform.Rotation)); // Get the difference of rotations so current rotation state doesn't affect calculations 
        //    //var rotationDiffVector = math.mul(rotationDiff, new float3 (0, 0, 1));
            

        //    //var curLookingVector = math.mul(localTransform.Rotation, localTransform.Forward());
        //    ////var curLookingVector = new float3(1, 0, 0);
        //    //var targetLookingVector = math.mul(rotation.newRotTarget, new float3 (0, 0, 1));
        //    ////var targetLookingVector = math.mul(rotationDiff, new float3 (1, 0, 0));

        //    ////Not taking in mind any vertical rotations
        //    //curLookingVector.y = 0;
        //    //targetLookingVector.y = 0;
        //    //math.normalize(curLookingVector);
        //    //math.normalize(targetLookingVector);

        //    ////var a = math.degrees(math.atan(targetLookingVector.z / targetLookingVector.x));
        //    ////if (targetLookingVector.z < 0)
        //    ////    a += 180;
        //    ////if (a > 180)
        //    ////    a = 360 - a;
        //    ////vehicleMovement.rotationDeltaY = math.abs(a);

        //    //var cos_DifferenceAngle = math.dot(curLookingVector, targetLookingVector);

        //    //vehicleMovement.rotationDeltaY = math.abs(math.degrees(math.acos(cos_DifferenceAngle)));
        //    //Debug.Log($"targetVector = {targetLookingVector}; rotationDeltaY = {vehicleMovement.rotationDeltaY};");

        //    if (vehicleMovement.rotationDeltaY >= vehicleMovement.minRotationDeltaForEffect)
        //    {
        //        //Play anims of rotation
        //    }

        //    #endregion

        //    localTransform.Rotation = math.nlerp(vehicleMovement.initialRotation, vehicleMovement.curRotTarget, vehicleMovement.rotTimeElapsed / vehicleMovement.rotTime);
        //    Debug.Log($"New Target --- RotationDeltaY = {vehicleMovement.rotationDeltaY}; RotTime = {vehicleMovement.rotTime}");
        //}
    }
}



public partial struct AttackRotationJob : IJobEntity
{
    public float deltaTime;
    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<LocalTransform> transformLookup;

    public void Execute(ref AttackRotationComponent rotation, in DynamicBuffer<AttackModelsBuffer> attackModelsBuf, in AttackCharsComponent attackChars, in LocalToWorld localToWorld)
    {
        #region Update target and handle automatic return to default rotation
        if (attackChars.target != Entity.Null)
        {
            rotation.newRotTarget = quaternion.LookRotationSafe(transformLookup[attackChars.target].Position - localToWorld.Position, localToWorld.Up);
            rotation.isInDefaultState = false;
            rotation.isRotatingToDefault = false;
            rotation.noRotTimeElapsed = 0;
        }
        else
        {
            if (rotation.isInDefaultState)
                return;
            if (!rotation.isRotatingToDefault)
            {
                rotation.noRotTimeElapsed += deltaTime;
                if (rotation.noRotTimeElapsed >= rotation.timeToReturnRot)
                    rotation.isRotatingToDefault = true;
            }
        }
        #endregion

        #region Usual rotation logic, but for attackModels
        if (UtilityFuncs.Nearly_Equals(rotation.newRotTarget, rotation.curRotTarget))
        {
            if (rotation.rotTimeElapsed < rotation.rotTime)
            {
                rotation.rotTimeElapsed += deltaTime;
                quaternion resultRotation = math.nlerp(rotation.initialRotation, rotation.newRotTarget, deltaTime / rotation.rotTime);
                foreach (var model in attackModelsBuf)
                    transformLookup.GetRefRW(model).ValueRW.Rotation = resultRotation;
            }
            else if (rotation.isRotatingToDefault)
            {
                rotation.isRotatingToDefault = false;
                rotation.isInDefaultState = true;
            }
        }
        else
        {
            rotation.rotTimeElapsed = deltaTime;
            rotation.initialRotation = transformLookup[attackModelsBuf[0]].Rotation;
            rotation.curRotTarget = rotation.newRotTarget;
            quaternion resultRotation = math.nlerp(rotation.initialRotation, rotation.newRotTarget, deltaTime / rotation.rotTime);
            foreach (var model in attackModelsBuf)
                transformLookup.GetRefRW(model).ValueRW.Rotation = resultRotation;
        }
        #endregion
    }
}

