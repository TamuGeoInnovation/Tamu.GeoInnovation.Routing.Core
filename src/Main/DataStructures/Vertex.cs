using System;
using System.Collections.Generic;
using System.Linq;

namespace USC.GISResearchLab.Routing.DataStructures
{
	public interface IRefreshable
	{
        void Refresh(bool resetCapacities);
	}

	public class Vertex : Point, IComparable<Vertex>, IEquatable<Vertex>, IRefreshable
	{
		private Edge[] edges;
		public double g; // Cost value
		public Edge LeadingEdge;
        private Edge[] incomingEdges;
        private double bottleneck;

        public Edge[] IncomingEdges
        {
            get { return incomingEdges; }
        }

		public int EdgeCount
		{
			get
			{
				if (edges != null) return edges.Length;
				else return 0;
			}
		}

		public Edge[] Edges
		{ get { return edges; } }

		public Vertex()
			: this(0.0, 0.0)
		{ }

		public Vertex(double _X, double _Y)
		{
			X = _X;
			Y = _Y;
			g = double.MaxValue;
			edges = null;
			LeadingEdge = null;
            incomingEdges = null;
            bottleneck = double.MaxValue;
		}

		public void Refresh(bool resetCapacities)
		{
            g = double.MaxValue;
            bottleneck = double.MaxValue;
			LeadingEdge = null;
            if (resetCapacities && edges != null) foreach (var e in Edges) e.ResetCapacityUsed();
		}

		public Int64 UID
		{
			get
			{
				Int64 h1 = Convert.ToInt64(x - xMin);
				Int64 h2 = Convert.ToInt64(y - yMin);
				return (h1 + h2 * yLen);
			}
		}

		public void AddEdge(Edge n)
		{
			if (edges != null)
			{
				Edge[] nn = new Edge[edges.Length + 1];
				Array.Copy(edges, 0, nn, 1, edges.Length);
				edges = null;
				nn[0] = n;
				edges = nn;
				nn = null;
			}
			else
			{
				edges = new Edge[1];
				edges[0] = n;
			}
		}

        public void AddIncomingEdge(Edge e)
        {
            if (incomingEdges != null)
            {
                if (!incomingEdges.Contains(e))
                {
                    Edge[] nn = new Edge[incomingEdges.Length + 1];
                    Array.Copy(incomingEdges, 0, nn, 1, incomingEdges.Length);
                    incomingEdges = null;
                    nn[0] = e;
                    incomingEdges = nn;
                    nn = null;
                }
            }
            else
            {
                incomingEdges = new Edge[1];
                incomingEdges[0] = e;
            }
        }

		public bool ContainsDuplicateNeighbors()
		{
			int i = 0, j = 0;

			for (i = 0; i < EdgeCount; i++)
				for (j = i + 1; j < EdgeCount; j++)
				{
					if (edges[i] == edges[j])
					{
						return true;
					}
				}
			return false;
		}

		public override string ToString()
		{
			return (X.ToString() + " – " + Y.ToString());
		}

		public override int GetHashCode()
		{
			return UID.GetHashCode();
		}

		public Vertex[] GetNeighbors()
		{
			Vertex[] n = null;
			if (edges != null)
			{
				n = new Vertex[edges.Length];
				for (int i = 0; i < edges.Length; i++) n[i] = edges[i].VertexTo;
			}
			else n = new Vertex[0];
			return n;
		}

        public void SetNewBottleneck(double _bottleneck)
        {
            bottleneck = Math.Min(bottleneck, _bottleneck);
        }

		#region IComparable<Vertex> & IEquatable<Vertex> Members

		public int CompareTo(Vertex other)
		{
			return UID.CompareTo(other.UID);
		}

		public bool Equals(Vertex other)
		{
			return ((x == other.x) && (y == other.y));
		}

		#endregion

		public class CostCompararer : IComparer<Vertex>
		{
			public int Compare(Vertex x, Vertex y)
			{
				int g = x.g.CompareTo(y.g); // minimize the g (traveling cost)
                if (g == 0)
                    g = y.bottleneck.CompareTo(x.bottleneck); // maximize the bottleneck
                return g;
			}
		}
	}
}