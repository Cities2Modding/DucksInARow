using DucksInARow.Models;
using Unity.Mathematics;

namespace DucksInARow.PlacementSolvers
{
    internal interface IPlacementSolver
    {
        DuckInARowMode Mode 
        { 
            get; 
        }

        DucksInARowModel Model
        {
            get;
        }

        void Calculate( );
        float GetLength( );
        float3 GetPosition( int index );
    }
}
