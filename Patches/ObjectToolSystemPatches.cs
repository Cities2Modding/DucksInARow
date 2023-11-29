using DucksInARow.Systems;
using Game.Tools;
using HarmonyLib;
using System.Reflection;
using Unity.Entities;
using Unity.Jobs;

namespace DucksInARow.Patches
{
    [HarmonyPatch( typeof( ObjectToolSystem ), "Apply" )]
    class ObjectToolSystem_ApplyPatch
    {
        enum CustomState
        {
            Default,
            MouseDownPrepare,
            MouseDown,
            Dragging
        }

        static FieldInfo m_State = typeof( ObjectToolSystem ).GetField( "m_State", BindingFlags.NonPublic | BindingFlags.Instance );
        static FieldInfo m_LastRaycastPoint = typeof( ObjectToolSystem ).GetField( "m_LastRaycastPoint", BindingFlags.NonPublic | BindingFlags.Instance );

        static void Prefix( ObjectToolSystem __instance, JobHandle inputDeps )
        {
            var state = ( CustomState ) ( int ) m_State.GetValue( __instance );

            if ( state == CustomState.Default )
            {
                var ducksInARow = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DucksInARowSystem>( );

                if ( ducksInARow.LineUpDucks )
                {
                    var lastRaycast = ( ControlPoint ) m_LastRaycastPoint.GetValue( __instance );
                    ducksInARow.CheckForLinePlacement( lastRaycast.m_HitPosition );
                }
            }
        }
    }
}
