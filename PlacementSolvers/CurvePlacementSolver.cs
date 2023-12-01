using Colossal.Mathematics;
using DucksInARow.Models;
using Game.Net;
using Unity.Mathematics;

namespace DucksInARow.PlacementSolvers
{
    public class CurvePlacementSolver : StraightPlacementSolver
    {
        public override DuckInARowMode Mode => DuckInARowMode.Curve;

        public CurvePlacementSolver( DucksInARowModel model ) : base( model )
        {
        }

        public override void Calculate( )
        {
            if ( Model.IsWaitingSecondPlacement && !Model.IsWaitingThirdPlacement )
            {
                base.Calculate( );
            }
            else
            {
                var start = new Line3.Segment( Model.StartPosition, Model.MiddlePosition );
                var end = new Line3.Segment( Model.CurrentPosition, Model.MiddlePosition );
                Model.Curves[0] = NetUtils.FitCurve( start, end );
            }
        }

        public override float GetLength( )
        {
            if ( Model.IsWaitingSecondPlacement && !Model.IsWaitingThirdPlacement )
                return base.GetLength( );
            else
                return MathUtils.Length( Model.Curves[0].xz, new Bounds1( 0f, 1f ) );
        }

        public override float3 GetPosition( int index )
        {
            if ( Model.IsWaitingSecondPlacement && !Model.IsWaitingThirdPlacement )
                return base.GetPosition( index );

            var spacing = ( float ) Model.Spacing;
            var flQty = ( Model.CurrentLength / spacing );
            NetUtils.ExtendedPositionAndTangent( Model.Curves[0], ( index / flQty ), out var pos, out _ );
            return pos;
        }
    }
}
