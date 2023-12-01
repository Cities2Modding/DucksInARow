using DucksInARow.Systems;
using Game.Tools;
using HarmonyLib;
using System.Reflection;
using Unity.Entities;
using Unity.Jobs;

namespace DucksInARow.Patches
{
    public enum CustomState
    {
        Default,
        MouseDownPrepare,
        MouseDown,
        Dragging
    }

    class ObjectToolSystemPatches
    {
        static FieldInfo m_State = typeof( ObjectToolSystem ).GetField( "m_State", BindingFlags.NonPublic | BindingFlags.Instance );
        static FieldInfo m_LastRaycastPoint = typeof( ObjectToolSystem ).GetField( "m_LastRaycastPoint", BindingFlags.NonPublic | BindingFlags.Instance );

        [HarmonyPatch( typeof( ObjectToolSystem ), "Apply" )]
        class ObjectToolSystem_ApplyPatch
        {            
            static bool Prefix( ObjectToolSystem __instance, JobHandle inputDeps )
            {
                var state = ( CustomState ) ( int ) m_State.GetValue( __instance );

                if ( state == CustomState.Default )
                {
                    var ducksInARow = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DucksInARowSystem>( );

                    if ( ducksInARow.Model.LineUpDucks )
                    {
                        var lastRaycast = ( ControlPoint ) m_LastRaycastPoint.GetValue( __instance );

                        if ( !ducksInARow.CheckForLinePlacement( lastRaycast.m_HitPosition ) )
                        {
                            m_State.SetValue( __instance, ( int ) CustomState.Default );
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        [HarmonyPatch( typeof( ObjectToolSystem ), "Cancel" )]
        class ObjectToolSystem_CancelPatch
        {
            static bool Prefix( ObjectToolSystem __instance, JobHandle inputDeps )
            {
                var state = ( CustomState ) ( int ) m_State.GetValue( __instance );

                if ( state == CustomState.Default )
                {
                    var ducksInARow = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<DucksInARowSystem>( );

                    if ( ducksInARow.Model.LineUpDucks )
                        return !ducksInARow.Cancel( );
                }

                return true;
            }
        }
    }
}
