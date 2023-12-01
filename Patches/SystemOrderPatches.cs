using Game;
using HarmonyLib;
using Game.Common;
using DucksInARow.Systems;

namespace DucksInARow.Patches
{
    [HarmonyPatch( typeof( SystemOrder ), "Initialize" )]
    class SystemOrder_InitializePatch
    {
        static void Postfix( UpdateSystem updateSystem )
        {
            updateSystem.UpdateAt<DucksInARowSystem>( SystemUpdatePhase.ToolUpdate );
        }
    }    
}
