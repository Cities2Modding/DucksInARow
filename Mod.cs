using HarmonyLib;
using System.Reflection;
using System.Linq;
using Game.Modding;
using Colossal.Logging;
using Game;
using Unity.Entities;
using DucksInARow.Systems;

namespace DucksInARow
{
    public class Mod : IMod
    {
        private const string HARMONY_ID = "cities2modding_ducksinarow";
        private static ILog _log = LogManager.GetLogger( "Cities2Modding" ).SetShowsErrorsInUI( false );

        public static Harmony _harmony;
        private World _world;

        public void OnLoad( UpdateSystem updateSystem )
        {
            _world = updateSystem.World;

            _harmony = new Harmony( HARMONY_ID );
            _harmony.PatchAll( );

            updateSystem.UpdateAt<DucksInARowSystem>( SystemUpdatePhase.ToolUpdate );

            _log.Info( @"  _____             _        _____                _____               
 |  __ \           | |      |_   _|         /\   |  __ \              
 | |  | |_   _  ___| | _____  | |  _ __    /  \  | |__) |_____      __
 | |  | | | | |/ __| |/ / __| | | | '_ \  / /\ \ |  _  // _ \ \ /\ / /
 | |__| | |_| | (__|   <\__ \_| |_| | | |/ ____ \| | \ \ (_) \ V  V / 
 |_____/ \__,_|\___|_|\_\___/_____|_| |_/_/    \_\_|  \_\___/ \_/\_/  " );
        }


        public void OnDispose( )
        {
            SafelyRemove<DucksInARowSystem>( );
            _harmony.UnpatchAll( HARMONY_ID );
        }

        private void SafelyRemove<T>( )
            where T : GameSystemBase
        {
            var system = _world.GetExistingSystemManaged<T>( );

            if ( system != null )
                _world.DestroySystemManaged( system );
        }
    }
}
