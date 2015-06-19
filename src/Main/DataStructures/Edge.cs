using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.SqlServer.Types;

namespace USC.GISResearchLab.Routing.DataStructures
{
    public class RoadProperties
    {
        public double Capacity;
        public char Direction;
        public double Len;
        public double CapacityUsed;
        public Point[] ShapePoints;

        public RoadProperties() : this(0.0, char.MinValue, 0.0, SqlGeography.Null) { }

        public RoadProperties(double capacity, char dir, double len, SqlGeography shape)
        {
            Capacity = capacity;
            Direction = dir;
            Len = len;
            CapacityUsed = 0.0;
            if (shape != null && shape != SqlGeography.Null)
            {
                ShapePoints = new Point[shape.STNumPoints().Value];
                SqlGeography s = null;

                for (int i = 0; i < ShapePoints.Length; i++)
                {
                    s = shape.STPointN(i + 1); 
                    ShapePoints[i] = new Point(s.Long.Value, s.Lat.Value);
                }
            }
            else ShapePoints = new Point[0];
        }
    }

    public class ProtectedEdgeDictionary : Dictionary<int, Edge>
    {
        public void AddIfNotExist(Edge value)
        {
            if (!this.ContainsKey(value.DBID)) this.Add(value.DBID, value);
        }
    }

    public interface IEdge<V>
    {
         V VertexFrom { get; set; } // Vertex one
         V VertexTo { get; set; } // Vertex two
    }

	public class Edge: IEquatable<Edge>, IEdge<Vertex>
	{
		public int DBID;
        private RoadProperties properties;
        public Vertex VertexFrom { get; set; } // Vertex one
        public Vertex VertexTo { get; set; } // Vertex two

        public double Capacity
        {
            get { return properties.Capacity; }
        }
        public double CapacityLeft
        {
            get { return properties.Capacity - properties.CapacityUsed; }
        }
        public double Len
        {
            get { return  properties.Len ; }
        }
        public char Direction
        {
            get { return properties.Direction; }
        }
		
		// Contructor
        public Edge(int DID, Vertex vertexFrom, Vertex vertexTo, RoadProperties prop)
		{
			VertexFrom = vertexFrom;
			VertexTo = vertexTo;
			DBID = DID;
            properties = prop != null ? prop : new RoadProperties();
		}

		public override string ToString()
		{
			return this.DBID.ToString();
		}

		public bool Equals(Edge other)
		{
			return (this.DBID == other.DBID);
		}

        internal void ResetCapacityUsed()
        {
            properties.CapacityUsed = 0.0;
        }

        public void ReserveCapacity(double reserve)
        {
            properties.CapacityUsed += reserve;
        }

        internal Point[] GetShapePoints()
        {
            return properties.ShapePoints;
        }
    }
}