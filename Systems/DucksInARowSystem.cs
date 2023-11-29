using Game;
using Game.Audio;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using System;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace DucksInARow.Systems
{
    public class DucksInARowSystem : GameSystemBase
    {
        private EntityQuery _soundQuery;
        private ObjectToolSystem _objectToolSystem;
        private TerrainSystem _terrainSystem;
        private PrefabSystem _prefabSystem;
        private ToolSystem _toolSystem;

        static FieldInfo m_Prefab = typeof( ObjectToolSystem ).GetField( "m_Prefab", BindingFlags.NonPublic | BindingFlags.Instance );

        public int Spacing
        {
            get;
            set;
        } = 8;

        public bool LineUpDucks
        {
            get;
            private set;
        }

        public bool IsWaitingSecondPlacement
        {
            get;
            set;
        } = false;

        public float3 StartPosition
        {
            get;
            set;
        }

        public float3 EndPosition
        {
            get;
            set;
        }

        protected override void OnCreate( )
        {
            base.OnCreate( );

            _objectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>( );
            _terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>( );
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>( );
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>( );
            _soundQuery = GetEntityQuery( ComponentType.ReadOnly<ToolUXSoundSettingsData>( ) );

            var action = new InputAction( "DucksInARow_Toggle" );
            action.AddBinding( "<Keyboard>/leftShift" );
            action.performed += ctx => 
            {
                if ( _toolSystem.activeTool is not ObjectToolSystem )
                    return;

                LineUpDucks = !LineUpDucks;

                if ( LineUpDucks )
                    AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_PlaceUpgradeSound );
                else
                    AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_AreaMarqueeClearEndSound );

                UnityEngine.Debug.Log( "DucksInARow: Toggled on: " + LineUpDucks );
            };
            action.Enable( );

            action = new InputAction( "DucksInARow_SpacingUp" );
            action.AddBinding( "<Keyboard>/upArrow" );
            action.performed += ctx =>
            {
                if ( !LineUpDucks )
                    return;

                Spacing++;

                UnityEngine.Debug.Log( "DucksInARow: Spacing set to " + Spacing + " meters." );

                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_ZoningMarqueeStartSound );
            };
            action.Enable( );

            action = new InputAction( "DucksInARow_SpacingDown" );
            action.AddBinding( "<Keyboard>/downArrow" );
            action.performed += ctx =>
            {
                if ( !LineUpDucks )
                    return;

                if ( Spacing <= 1 )
                    return;

                Spacing--;

                UnityEngine.Debug.Log( "DucksInARow: Spacing set to " + Spacing + " meters." );

                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_ZoningMarqueeStartSound );
            };
            action.Enable( );

            UnityEngine.Debug.Log( "DucksInARowSystem OnCreate" );
        }

        protected override void OnUpdate( )
        {
        }

        public void CheckForLinePlacement( float3 hitPosition )
        {
            if ( !IsWaitingSecondPlacement )
            {
                StartPosition = hitPosition;
                IsWaitingSecondPlacement = true;
                UnityEngine.Debug.Log( "DucksInARow: Start click!" );
            }
            else
            {
                EndPosition = hitPosition;
                IsWaitingSecondPlacement = false;

                UnityEngine.Debug.Log( "DucksInARow: End click!" );

                var length = math.length( EndPosition - StartPosition );
                var qty = ( int ) ( length / Spacing );
                var direction = math.normalize( EndPosition - StartPosition );

                if ( qty > 1 )
                {
                    for ( var i = 0; i < qty - 1; i++ )
                    {
                        CreatePrefab( StartPosition + ( direction * Spacing * i ) );
                    }
                }
            }
        }

        private void CreatePrefab( float3 position )
        {
            var prefabEntity = _prefabSystem.GetEntity( ( PrefabBase ) m_Prefab.GetValue( _objectToolSystem ) );

            if ( !EntityManager.HasComponent<PlantData>( prefabEntity ) )
                return;

            var random = new Unity.Mathematics.Random( ( uint ) DateTime.Now.Ticks );

            var objectData = EntityManager.GetComponentData<ObjectData>( prefabEntity );

            TerrainHeightData heightData = _terrainSystem.GetHeightData( true );
            Transform transformData;
            transformData.m_Rotation = quaternion.RotateY( random.NextFloat( 6.28318548f ) );
            transformData.m_Position = position;
            transformData.m_Position.y = TerrainUtils.SampleHeight( ref heightData, transformData.m_Position );

            Entity newEntity = EntityManager.CreateEntity( objectData.m_Archetype );
            EntityManager.SetComponentData( newEntity, new PrefabRef( prefabEntity ) );
            EntityManager.SetComponentData( newEntity, transformData );            
        }
    }
}
