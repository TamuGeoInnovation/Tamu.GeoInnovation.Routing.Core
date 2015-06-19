using System;
using System.Collections.Generic;
using System.Linq;
using USC.GISResearchLab.Routing.DataStructures;

namespace USC.GISResearchLab.Routing.Algorithms
{
	public class Dijkstra
	{
		private BinaryHeap<Vertex, Vertex.CostCompararer> heap;
		private Graph<Vertex, long, Edge, int> g;
		private Dictionary<long, Vertex> closedList;
        private List<Vertex> RestrictedVertices;

		// Constructor
		public Dijkstra(Graph<Vertex, long, Edge, int> graph, List<Vertex> restrictedVertices)
		{
            g = graph;
            heap = new BinaryHeap<Vertex, Vertex.CostCompararer>(g.VertixCount, new Vertex.CostCompararer());
            closedList = new Dictionary<long, Vertex>();
            RestrictedVertices = restrictedVertices;
            if (RestrictedVertices == null) RestrictedVertices = new List<Vertex>();
		}

		// Dijkstra calculation algorithm
		public Point[] Execute(SnapPoint start, Dictionary<long, Point> endList)
		{
			int i = 0;
			double alternativeG = 0.0;
			Edge edge = null, backEdge = null;
			Vertex vertex = null, end = null;
			Vertex[] neighbors = null;
            List<TurnRestriction<Vertex, Edge>> restrictionList = null;

            // input checks
            if (start == null || endList == null) throw new NullReferenceException("input parameters cannot be null.");

            heap = new BinaryHeap<Vertex, Vertex.CostCompararer>(g.VertixCount, new Vertex.CostCompararer());

			edge = start.AssociatedEdge;
			edge.VertexTo.g = edge.Len - start.PositionAlongEdge;
			edge.VertexTo.LeadingEdge = null;
            edge.VertexTo.SetNewBottleneck(edge.CapacityLeft);
			heap.Add(edge.VertexTo);

            edge = g.GetEdge(-edge.DBID);

			if (edge != null)
			{
				edge.VertexTo.g = start.PositionAlongEdge;
                edge.VertexTo.LeadingEdge = null;
                edge.VertexTo.SetNewBottleneck(edge.CapacityLeft);
				heap.Add(edge.VertexTo);
			}
			else
			{
				start.AssociatedEdge.VertexFrom.g = double.MaxValue;
				start.AssociatedEdge.VertexFrom.LeadingEdge = null;
            }

            // prepare the closed list and init it with the restricted nodes
            closedList.Clear();
            foreach (var v in RestrictedVertices) if (!heap.Contains(v)) closedList.Add(v.UID, v);

			while (!heap.IsEmpty)
			{
				// For each smallset g-value Vertex
				vertex = heap.ExtractMin();
				closedList.Add(vertex.UID, vertex);

				// check for end points
                if (endList.ContainsKey(vertex.UID))
                {
                    end = vertex;
                    break;
                }

				// Get the neighbors
                neighbors = vertex.GetNeighbors();
                restrictionList = g.GetTurnRestriction(vertex.UID);

				for (i = 0; i < neighbors.Length; ++i)
				{
					if (closedList.ContainsKey(neighbors[i].UID)) continue;

                    // check turn restrictions
                    if (restrictionList != null)
                    {
                        if (vertex.LeadingEdge != null)
                        {
                            backEdge = vertex.LeadingEdge;
                        }
                        else // this is where the 'vertex' is the start point and needs special care
                        {
                            if (vertex.Equals(start.AssociatedEdge.VertexTo)) backEdge = start.AssociatedEdge;
                            else backEdge = g.GetEdge(-start.AssociatedEdge.DBID);
                        }
                        if (restrictionList.Any(a => a.EdgeFrom.Equals(backEdge) &&
                            a.EdgeTo.Equals(vertex.Edges[i]) && a.Intersection.Equals(vertex))) continue;
                    }

					alternativeG = vertex.g + vertex.Edges[i].Len;

					if (!heap.Contains(neighbors[i]))
					{
						neighbors[i].g = alternativeG;
						neighbors[i].LeadingEdge = vertex.Edges[i];
                        neighbors[i].SetNewBottleneck(vertex.Edges[i].CapacityLeft);
						heap.Add(neighbors[i]);
					}
					else if (heap.Contains(neighbors[i]))
					{
						if (alternativeG < neighbors[i].g)
						{
                            neighbors[i].LeadingEdge = vertex.Edges[i];
                            neighbors[i].SetNewBottleneck(vertex.Edges[i].CapacityLeft);
                            neighbors[i].g = alternativeG;
							heap.DecreaseKey(neighbors[i]);
						}
					}
				}
			} // end while

            return ExtractRouteAsPoints(start, end);
		}

        private Point[] ExtractRouteAsPoints(SnapPoint start, Vertex end)
        {
            // path generation from start to end as an output
            Point[] path = null, miniPath = null;
            int i = 0;

            if (end != null)
            {
                var pathStack = new Stack<Point>();
                var p = end;
                while (p.LeadingEdge != null)
                {
                    p.LeadingEdge.ReserveCapacity(0.25);
                    miniPath = p.LeadingEdge.GetShapePoints();
                    if (p.LeadingEdge.DBID > 0)
                        for (i = miniPath.Length - 1; i > 0; i--) pathStack.Push(miniPath[i]);
                    else
                        for (i = 0; i < miniPath.Length - 1; i++) pathStack.Push(miniPath[i]);
                    p = p.LeadingEdge.VertexFrom;
                }
                if (!start.Equals(p)) pathStack.Push(start);
                path = pathStack.ToArray();
            }
            return path;
        }

		public void ExecutePrecomp(Vertex s, double limit)
		{
			int i = 0;
			double alternativeG = 0.0;
			Vertex vertex = null;
			Vertex[] neighbors = null;

			heap.Clear();
			closedList.Clear();
			s.g = 0;
			s.LeadingEdge = null;

			heap.Add(s);

			while (!heap.IsEmpty)
			{
				// For each smallset g-value Vertex
				vertex = heap.ExtractMin();
				if (vertex.g > limit) continue;
				closedList.Add(vertex.UID, vertex);

				// Get the neighbors
				neighbors = vertex.GetNeighbors();

				for (i = 0; i < neighbors.Length; ++i)
				{
					if (closedList.ContainsKey(neighbors[i].UID)) continue;

					alternativeG = vertex.g + vertex.Edges[i].Len;

					if (!heap.Contains(neighbors[i]))
					{
						neighbors[i].g = alternativeG;
						neighbors[i].LeadingEdge = vertex.Edges[i];
						heap.Add(neighbors[i]);
					}
					else if (heap.Contains(neighbors[i]) && (alternativeG < neighbors[i].g))
					{
						neighbors[i].LeadingEdge = vertex.Edges[i];
                        neighbors[i].g = alternativeG;
						heap.DecreaseKey(neighbors[i]);
					}
				}
			} // end while
		}

		public void ExecutePrecomp(SnapPoint start)
		{
			int i = 0;
			double alternativeG = 0.0;
			Vertex vertex = null;
			Vertex[] neighbors = null;

			heap.Clear();
			closedList.Clear();

			var edge = start.AssociatedEdge;
			if (edge.VertexTo.g > edge.Len - start.PositionAlongEdge)
			{
				edge.VertexTo.g = edge.Len - start.PositionAlongEdge;
				edge.VertexTo.LeadingEdge = null;
				heap.Add(edge.VertexTo);
			}

			edge = g.GetEdge(-edge.DBID);
			if (edge != null)
			{
				if (edge.VertexTo.g > start.PositionAlongEdge)
				{
					edge.VertexTo.g = start.PositionAlongEdge;
					edge.VertexTo.LeadingEdge = null;
					heap.Add(edge.VertexTo);
				}
			}

			while (!heap.IsEmpty)
			{
				// For each smallset g-value Vertex
				vertex = heap.ExtractMin();
				//if (vertex.g > limit) continue;
				closedList.Add(vertex.UID, vertex);

				// Get the neighbors
				neighbors = vertex.GetNeighbors();

				for (i = 0; i < neighbors.Length; ++i)
				{
					if (closedList.ContainsKey(neighbors[i].UID)) continue;

					alternativeG = vertex.g + vertex.Edges[i].Len;

					if (!heap.Contains(neighbors[i]))
					{
						if (neighbors[i].g > alternativeG) // this is the key code line !
						{
							neighbors[i].g = alternativeG;
							neighbors[i].LeadingEdge = vertex.Edges[i];
							heap.Add(neighbors[i]);
						}
					}
					else if (heap.Contains(neighbors[i]) && (alternativeG < neighbors[i].g))
					{
						neighbors[i].LeadingEdge = vertex.Edges[i];
                        neighbors[i].g = alternativeG;
						heap.DecreaseKey(neighbors[i]);
					}
				}
			} // end while
		}
	}
}