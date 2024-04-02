using Colossal.Entities;
using Colossal.Mathematics;
using DucksInARow.Models;
using DucksInARow.PlacementSolvers;
using Game;
using Game.Audio;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace DucksInARow.Systems
{
    public partial class DucksInARowSystem : GameSystemBase
    {
        private EntityQuery _soundQuery;
        private ObjectToolSystem _objectToolSystem;
        private TerrainSystem _terrainSystem;
        private PrefabSystem _prefabSystem;
        private ToolSystem _toolSystem;
        private OverlayRenderSystem _overlayRenderSystem;

        static FieldInfo m_Prefab = typeof( ObjectToolSystem ).GetField( "m_Prefab", BindingFlags.NonPublic | BindingFlags.Instance );
        static FieldInfo m_LastRaycastPoint = typeof( ObjectToolSystem ).GetField( "m_LastRaycastPoint", BindingFlags.NonPublic | BindingFlags.Instance );

        private readonly DucksInARowModel _model = new DucksInARowModel();

        private List<Entity> Ducks
        {
            get;
            set;
        } = new List<Entity>( 100 );

        private OverlayRenderSystem.Buffer OverlayBuffer
        {
            get;
            set;
        }

        private float3 LastRaycastPosition
        {
            get;
            set;
        }

        public DucksInARowModel Model
        {
            get
            {
                return _model;
            }
        }

        private PrefabBase SelectedPrefab
        {
            get
            {
                return ( PrefabBase ) m_Prefab.GetValue( _objectToolSystem );
            }
        }

        private List<IPlacementSolver> PlacementSolvers
        {
            get;
            set;
        } = new List<IPlacementSolver>( );

        private IPlacementSolver CurrentSolver
        {
            get;
            set;
        }

        private static UnityEngine.Color LINE_COLOUR = new UnityEngine.Color( 1.0f, 1.0f, 1.0f, 0.5f );
        private static UnityEngine.Color BLIP_COLOUR = new UnityEngine.Color( 1.0f, 1.0f, 1.0f, 0.8f );

        private readonly Queue<float3> _addQueue = new Queue<float3>( );
        private readonly Queue<Entity> _removalQueue = new Queue<Entity> ();

        private float _nextUpdateTime;
        private string _lastPrefab;

        protected override void OnCreate( )
        {
            base.OnCreate( );

            _objectToolSystem = World.GetOrCreateSystemManaged<ObjectToolSystem>( );
            _terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>( );
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>( );
            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>( );
            _overlayRenderSystem = World.GetExistingSystemManaged<OverlayRenderSystem>( );
            OverlayBuffer =_overlayRenderSystem.GetBuffer( out _ );

            _soundQuery = GetEntityQuery( ComponentType.ReadOnly<ToolUXSoundSettingsData>( ) );

            _model.Random = new System.Random( ( int ) DateTime.Now.Ticks );

            AddSolvers( );
            SetupKeybinds( );

            UnityEngine.Debug.Log( "DucksInARowSystem OnCreate" );
        }

        private void AddSolvers( )
        {
            PlacementSolvers.Add( new StraightPlacementSolver( _model ) );
            PlacementSolvers.Add( new CurvePlacementSolver( _model ) );
            PlacementSolvers.Add( new CirclePlacementSolver( _model ) );
            CurrentSolver = PlacementSolvers.First( );
        }

        private void UpdateCurrentSolver( )
        {
            CurrentSolver = PlacementSolvers.FirstOrDefault( s => s.Mode == _model.Mode );
        }

        private void SetupKeybinds( )
        {
            var action = new InputAction( "DucksInARow_Toggle" );
            action.AddBinding( "<Keyboard>/leftShift" );
            action.performed += ctx =>
            {
                if ( _toolSystem.activeTool is not ObjectToolSystem || !IsValidPrefab( ) )
                    return;

                _model.LineUpDucks = !_model.LineUpDucks;

                if ( _model.LineUpDucks )
                    AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_PlaceUpgradeSound );
                else
                    AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_AreaMarqueeClearEndSound );

                if ( !_model.LineUpDucks )
                {
                    _model.IsWaitingThirdPlacement = false;
                    _model.IsWaitingSecondPlacement = false;
                    Clear( true );
                }

                UnityEngine.Debug.Log( "DucksInARow: Toggled on: " + _model.LineUpDucks );
            };
            action.Enable( );

            action = new InputAction( "DucksInARow_SpacingUp" );
            action.AddBinding( "<Keyboard>/upArrow" );
            action.performed += ctx =>
            {
                if ( !_model.LineUpDucks || _toolSystem.activeTool is not ObjectToolSystem || !IsValidPrefab( ) )
                    return;

                _model.Spacing += 0.25m;

                UnityEngine.Debug.Log( "DucksInARow: Spacing set to " + _model.Spacing + " meters." );

                if ( _model.IsWaitingSecondPlacement || _model.IsWaitingThirdPlacement )
                    ForceRecalculate( );

                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_ZoningMarqueeStartSound );
            };
            action.Enable( );

            action = new InputAction( "DucksInARow_SpacingDown" );
            action.AddBinding( "<Keyboard>/downArrow" );
            action.performed += ctx =>
            {
                if ( !_model.LineUpDucks || _toolSystem.activeTool is not ObjectToolSystem || !IsValidPrefab( ) )
                    return;

                if ( _model.Spacing <= 1 )
                {
                    _model.Spacing = 1;
                    return;
                }
                _model.Spacing -= 0.25m;

                UnityEngine.Debug.Log( "DucksInARow: Spacing set to " + _model.Spacing + " meters." );

                if ( _model.IsWaitingSecondPlacement || _model.IsWaitingThirdPlacement )
                    ForceRecalculate( );

                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_ZoningMarqueeStartSound );
            };
            action.Enable( );           

            action = new InputAction( "DucksInARow_ModeChange" );
            action.AddCompositeBinding( "ButtonWithOneModifier" )
                .With( "Modifier", "<Keyboard>/alt" )
                .With( "Button", "<Keyboard>/x" );
            action.performed += ctx =>
            {
                if ( !_model.LineUpDucks || _toolSystem.activeTool is not ObjectToolSystem || !IsValidPrefab( ) )
                    return;

                var modesCount = Enum.GetValues( typeof( DuckInARowMode ) ).Length;
                var currentMode = ( int ) _model.Mode;

                if ( currentMode + 1 >= modesCount )
                {
                    currentMode = 0;
                }
                else
                    currentMode++;

                _model.Mode = ( DuckInARowMode ) currentMode;
                UpdateCurrentSolver( );

                if ( _model.Mode == DuckInARowMode.Curve && ( _model.IsWaitingThirdPlacement ) )
                {
                    _model.IsWaitingThirdPlacement = false;
                    _model.IsWaitingSecondPlacement = true;
                }

                Clear( true );

                UnityEngine.Debug.Log( "DucksInARow: Mode changed to " + _model.Mode + "." );

                if ( _model.IsWaitingSecondPlacement || _model.IsWaitingThirdPlacement )
                    ForceRecalculate( );

                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_ZoningMarqueeStartSound );
            };
            action.Enable( );

            action = new InputAction( "DucksInARow_AdultChange" );
            action.AddCompositeBinding( "ButtonWithOneModifier" )
                .With( "Modifier", "<Keyboard>/alt" )
                .With( "Button", "<Keyboard>/a" );
            action.performed += ctx =>
            {
                if ( !_model.LineUpDucks || _toolSystem.activeTool is not ObjectToolSystem || !IsValidPrefab() )
                    return;

                _model.SpawnTreesAsAdult = !_model.SpawnTreesAsAdult;
                UnityEngine.Debug.Log( "DucksInARow: Tree spawnas adult " + _model.SpawnTreesAsAdult + "." );
                ForceRecalculate( );
                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_ZoningMarqueeStartSound );
            };
            action.Enable( );
        }

        private bool IsValidPrefab( )
        {
            var prefabEntity = GetPrefabEntity( );

            return EntityManager.HasComponent<PlantData>( prefabEntity );
        }

        protected override void OnUpdate( )
        {
            UpdateQueues( );

            if ( _toolSystem.activeTool != _objectToolSystem || !_model.LineUpDucks || _toolSystem.activeTool is not ObjectToolSystem || !IsValidPrefab( ) )
            {
                _model.IsWaitingSecondPlacement = false;
                _model.IsWaitingThirdPlacement = false;
                Clear( true );
            }

            if ( _model.IsWaitingSecondPlacement || _model.IsWaitingSecondPlacement )
            {
                var controlPoint = ( ControlPoint ) m_LastRaycastPoint.GetValue( _objectToolSystem );
                Model.CurrentPosition = controlPoint.m_Position;

                // Only update if the raycast position changed
                if ( UnityEngine.Time.time >= _nextUpdateTime )
                {
                    CheckForRecalculation( controlPoint.m_Position );
                    _nextUpdateTime = UnityEngine.Time.time + 0.02f;
                }

                RenderOverlay( controlPoint.m_Position );
            }
        }

        private void RenderOverlay( float3 currentPosition )
        {
            foreach ( var duck in Ducks )
            {
                if ( EntityManager.TryGetComponent<Transform>( duck, out var transform ) )
                    OverlayBuffer.DrawCircle( BLIP_COLOUR, transform.m_Position, 0.4f );
            }

            if ( _model.Mode == DuckInARowMode.Straight )
            {
                OverlayBuffer.DrawDashedLine( LINE_COLOUR, new Line3.Segment
                {
                    a = _model.StartPosition,
                    b = currentPosition
                }, 0.4f, 0.1f, 0.4f );
            }
            else if ( _model.Mode == DuckInARowMode.Curve && _model.IsWaitingSecondPlacement && !_model.IsWaitingThirdPlacement )
            {
                OverlayBuffer.DrawDashedLine( LINE_COLOUR, new Line3.Segment
                {
                    a = _model.StartPosition,
                    b = currentPosition
                }, 0.4f, 0.1f, 0.4f );
            }
            else if ( _model.Mode == DuckInARowMode.Curve && _model.IsWaitingThirdPlacement )
            {
                OverlayBuffer.DrawDashedCurve( LINE_COLOUR, _model.Curves[0], 0.2f, 0.1f, 0.4f );
            }
            else if ( _model.Mode == DuckInARowMode.Circle )
            {
                OverlayBuffer.DrawDashedLine( LINE_COLOUR, new Line3.Segment
                {
                    a = _model.StartPosition,
                    b = currentPosition
                }, 0.4f, 0.4f, 0.8f );
            }
        }

        private void UpdateQueues( )
        {
            // Remove a max of 4 per update to limit performance hits
            if ( _model.HasChanges && _removalQueue.Count > 0 )
            {
                for ( var i = 0; i < math.min( _removalQueue.Count, 4 ); )
                {
                    var duck = _removalQueue.Dequeue( );
                    Ducks.RemoveAll( d => d.Index == duck.Index );
                    EntityManager.AddComponent<Deleted>( duck );
                }
            }

            // Add a max of 4 per update to limit performance hits
            if ( _model.HasChanges && _addQueue.Count > 0 )
            {
                var prefabEntity = GetPrefabEntity( );

                var controlPoint = ( ControlPoint ) m_LastRaycastPoint.GetValue( _objectToolSystem );
                var direction = math.normalize( controlPoint.m_Position - _model.StartPosition );

                for ( var i = 0; i < math.min( _addQueue.Count, 4 ); )
                {
                    var duck = CreatePrefab( prefabEntity, direction, _addQueue.Dequeue( ) );
                    EnforceAdultIfTree( duck );
                    Ducks.Add( duck );
                }
            }

            // We've finished recalculating the changes
            if ( _model.HasChanges && _addQueue.Count == 0 && _removalQueue.Count == 0 )
                _model.HasChanges = false;
        }

        private void CheckForRecalculation( float3 position )
        {
            // If selected prefab is changed force re-calculation
            if ( SelectedPrefab.GetPrefabID( ).GetName( ) != _lastPrefab )
            {
                _lastPrefab = SelectedPrefab.GetPrefabID( ).GetName( );
                Clear( true );
            }

            // Only update if the raycast position changed
            if ( !_model.HasChanges && math.length( position - LastRaycastPosition ) >= 0.01 )
            {
                LastRaycastPosition = position;

                RecalculateDucks( );
            }
        }

        private void Clear( bool remove = false )
        {
            if ( remove )
            {
                foreach ( var duck in Ducks )
                    _removalQueue.Enqueue( duck );
            }

            Ducks.Clear( );
            _model.HasChanges = true;
        }

        private void ForceRecalculate( )
        {
            var controlPoint = ( ControlPoint ) m_LastRaycastPoint.GetValue( _objectToolSystem );
            Model.CurrentPosition = controlPoint.m_Position;
            RecalculateDucks( );
        }

        private void RecalculateDucks(  )
        {
            CurrentSolver.Calculate( );

            var d = Model.CurrentPosition - _model.StartPosition;

            Model.CurrentLength = CurrentSolver.GetLength( );

            var spacing = ( float ) _model.Spacing;
            var flQty = ( Model.CurrentLength / spacing );
            var qty = ( int ) flQty + 1;
            var direction = math.normalize( d );

            if ( qty <= 1 )
                return;

            var updateCount = Ducks.Count;

            // If the quantity changed we need to add/remove entities
            if ( qty != Ducks.Count )
            {
                // We need to add
                if ( qty > Ducks.Count )
                {
                    //UnityEngine.Debug.Log( "Adding ducks!" + "Qty: "+ qty + " duck count: " + Ducks.Count );
                    for ( var i = Ducks.Count - 1; i < qty; i++ )
                    {
                        _addQueue.Enqueue( CurrentSolver.GetPosition( i ) );
                    }
                }
                // We need to remove
                else
                {
                    updateCount = qty;

                    //UnityEngine.Debug.Log( "Removing ducks!" + "Qty: " + qty + " duck count: " + Ducks.Count );
                    for ( var i = qty - 1; i < Ducks.Count; i++ )
                        _removalQueue.Enqueue( Ducks[i] );
                }
            }

            if ( updateCount > 0 )
            {
                for ( var i = 0; i < updateCount; i++ )
                {
                    var duck = Ducks[i];

                    if ( EntityManager.TryGetComponent<Transform>( duck, out var transform ) )
                    {
                        ApplyRotation( duck, ref transform );

                        transform.m_Position = CurrentSolver.GetPosition( i );

                        //ApplyNoise( duck, direction, ref transform );
                        LevelToGround( ref transform );
                        EntityManager.SetComponentData( duck, transform );
                        EnforceAdultIfTree( duck );
                        EntityManager.AddComponent<Updated>( duck );
                    }
                }
            }
            _model.HasChanges = true;
        }

        private void EnforceAdultIfTree( Entity duck )
        {
            if ( _model.SpawnTreesAsAdult && EntityManager.TryGetComponent<Tree>( duck, out var tree ) )
            {
                tree.m_State = TreeState.Adult;
                tree.m_Growth = byte.MaxValue / 2;
                EntityManager.SetComponentData( duck, tree );
            }
        }

        public bool Cancel( )
        {
            if ( !_model.IsWaitingSecondPlacement && !_model.IsWaitingThirdPlacement )
                return false;

            // If we're placing the third one go to middle placement
            if ( _model.IsWaitingThirdPlacement )
            {
                _model.IsWaitingSecondPlacement = true;
                _model.IsWaitingThirdPlacement = false;

            }
            // We're on the first so cancel outright
            else
            {
                _model.IsWaitingSecondPlacement = false;
                _model.IsWaitingThirdPlacement = false;

                // Clear out the active ducks and remove them
                Clear( true );
            }

            UnityEngine.Debug.Log( "DucksInARow: Cancel!" );
            AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_NetCancelSound );
            return true;
        }

        public bool CheckForLinePlacement( float3 hitPosition )
        {
            if ( !_model.LineUpDucks || _toolSystem.activeTool is not ObjectToolSystem || !IsValidPrefab( ) )
                return true;

            if ( !_model.IsWaitingSecondPlacement )
            {
                _model.StartPosition = hitPosition;
                _model.IsWaitingSecondPlacement = true;
                UnityEngine.Debug.Log( "DucksInARow: Start click!" );

                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_NetExpandSound );
            }
            else if ( _model.Mode == DuckInARowMode.Curve && _model.IsWaitingSecondPlacement && !_model.IsWaitingThirdPlacement )
            {
                _model.MiddlePosition = hitPosition;
                _model.IsWaitingThirdPlacement = true;

                UnityEngine.Debug.Log( "DucksInARow: Middle click!" );

                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_NetExpandSound );
            }
            else if ( _model.Mode != DuckInARowMode.Curve && _model.IsWaitingSecondPlacement ||
                _model.Mode == DuckInARowMode.Curve && _model.IsWaitingThirdPlacement )
            {
                _model.EndPosition = hitPosition;

                _model.IsWaitingSecondPlacement = false;
                _model.IsWaitingThirdPlacement = false;

                // Clear out the active ducks now they're placed!
                Clear( );
                UnityEngine.Debug.Log( "DucksInARow: End click!" );
                AudioManager.instance.PlayUISound( _soundQuery.GetSingleton<ToolUXSoundSettingsData>( ).m_PlacePropSound );

                // Go into repeat mode, user has to right click to cancel
                if ( _model.Mode != DuckInARowMode.Circle )
                {
                    _model.StartPosition = _model.EndPosition;
                    _model.IsWaitingSecondPlacement = true;
                }
            }
            return false;
        }

        private Entity GetPrefabEntity( )
        {
            var prefabEntity = _prefabSystem.GetEntity( SelectedPrefab );

            if ( !EntityManager.HasComponent<PlantData>( prefabEntity ) )
                return Entity.Null;

            return prefabEntity;
        }

        private Entity CreatePrefab( Entity prefabEntity, float3 direction, float3 position )
        {
            var objectData = EntityManager.GetComponentData<ObjectData>( prefabEntity );
            var newDuck = EntityManager.CreateEntity( objectData.m_Archetype );

            var transform = new Transform();
            ApplyRotation( newDuck, ref transform );
            transform.m_Position = position;
            LevelToGround( ref transform );

            EntityManager.SetComponentData( newDuck, new PrefabRef( prefabEntity ) );
            EntityManager.SetComponentData( newDuck, transform );

            return newDuck;
        }

        private void ApplyRotation( Entity duck, ref Transform transform )
        {
            _model.Random = new System.Random( ( int ) DateTime.Now.Ticks + duck.Index );
            transform.m_Rotation = quaternion.RotateY( ( float ) ( _model.Random.NextDouble() * 6.28318548f ) );
        }

        private void LevelToGround( ref Transform transform )
        {
            var heightData = _terrainSystem.GetHeightData( true );
            transform.m_Position.y = TerrainUtils.SampleHeight( ref heightData, transform.m_Position );
        }
    }
}
