using Colossal.Mathematics;
using DucksInARow.Models;
using Game.Net;
using Unity.Mathematics;

namespace DucksInARow.PlacementSolvers
{
    public class CirclePlacementSolver : IPlacementSolver
    {
        public DucksInARowModel Model
        {
            get;
            private set;
        }

        public CirclePlacementSolver( DucksInARowModel model )
        {
            Model = model;
        }

        public DuckInARowMode Mode =>  DuckInARowMode.Circle;

        public void Calculate( )
        {
            var dist = math.length( Model.CurrentPosition - Model.StartPosition );
            Model.Curves[0] = NetUtils.CircleCurve( Model.StartPosition, -dist, -dist );
            Model.Curves[1] = NetUtils.CircleCurve( Model.StartPosition, dist, -dist );
            Model.Curves[2] = NetUtils.CircleCurve( Model.StartPosition, dist, dist );
            Model.Curves[3] = NetUtils.CircleCurve( Model.StartPosition, -dist, dist );
        }

        public float GetLength( )
        {
            return MathUtils.Length( Model.Curves[0].xz, new Bounds1( 0f, 1f ) ) +
                MathUtils.Length( Model.Curves[1].xz, new Bounds1( 0f, 1f ) ) +
                MathUtils.Length( Model.Curves[2].xz, new Bounds1( 0f, 1f ) ) +
                MathUtils.Length( Model.Curves[3].xz, new Bounds1( 0f, 1f ) );
        }

        public float3 GetPosition( int index )
        {
            var spacing = ( float ) Model.Spacing;
            var flQty = ( Model.CurrentLength / spacing );
            var t = ( index / flQty );
            var curveIndex = 0;

            // Remap T based on the circle quarter
            if ( t <= 0.25f )
            {
                // Scale it to a quarter
                t /= 0.25f;
            }
            else if ( t <= 0.5f )
            {
                // Remove the min value
                t -= 0.25f;

                // Scale it to a quarter
                t /= 0.25f;
                curveIndex = 1;
            }
            else if ( t <= 0.75f )
            {
                // Remove the min value
                t -= 0.5f;

                // Scale it to a quarter
                t /= 0.25f;
                curveIndex = 2;
            }
            else
            {
                // Remove the min value
                t -= 0.75f;

                // Scale it to a quarter
                t /= 0.25f;
                curveIndex = 3;
            }

            NetUtils.ExtendedPositionAndTangent( Model.Curves[curveIndex], t, out var pos, out _ );
            return pos;
        }
    }
}
