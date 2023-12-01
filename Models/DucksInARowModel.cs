using Colossal.Mathematics;
using System.Collections.Generic;
using Unity.Mathematics;

namespace DucksInARow.Models
{
    public class DucksInARowModel
    {
        public decimal Spacing
        {
            get;
            set;
        } = 9m;

        public bool LineUpDucks
        {
            get;
            set;
        }

        public bool IsWaitingSecondPlacement
        {
            get;
            set;
        } = false;

        public bool IsWaitingThirdPlacement
        {
            get;
            set;
        } = false;

        public bool HasChanges
        {
            get;
            set;
        }

        public bool SpawnTreesAsAdult
        {
            get;
            set;
        }

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

        public float3 MiddlePosition
        {
            get;
            set;
        }

        public float3 CurrentPosition
        {
            get;
            set;
        }

        public float CurrentLength
        {
            get;
            set;
        }

        public System.Random Random
        {
            get;
            set;
        }

        public DuckInARowMode Mode
        {
            get;
            set;
        } = DuckInARowMode.Straight;

        public Bezier4x3[] Curves
        {
            get;
            set;
        } = new Bezier4x3[4];
    }
}
