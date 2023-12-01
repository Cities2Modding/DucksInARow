using Colossal.Mathematics;
using DucksInARow.Models;
using Game.Net;
using Unity.Mathematics;

namespace DucksInARow.PlacementSolvers
{
    public class StraightPlacementSolver : IPlacementSolver
    {
        public virtual DuckInARowMode Mode => DuckInARowMode.Straight;

        public DucksInARowModel Model
        {
            get;
            private set;
        }

        public StraightPlacementSolver( DucksInARowModel model )
        {
            Model = model;
        }

        public virtual void Calculate( )
        {
            Model.Curves[0] = NetUtils.StraightCurve( Model.StartPosition, Model.CurrentPosition );
        }

        public virtual float GetLength( )
        {
            var d = Model.CurrentPosition - Model.StartPosition;
            return math.length( d );
        }

        public virtual float3 GetPosition( int index )
        {
            var spacing = ( float ) Model.Spacing;
            var direction = math.normalize( Model.CurrentPosition - Model.StartPosition );
            return Model.StartPosition + ( direction * spacing * index );
        }
    }
}
