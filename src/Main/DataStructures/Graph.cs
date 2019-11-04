using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace USC.GISResearchLab.Routing.DataStructures
{
    public class TurnRestriction<V, E>
        where V : IEquatable<V>, IRefreshable
        where E : IEquatable<E>, IEdge<V>
    {
        public V Intersection;
        public E EdgeFrom;
        public E EdgeTo;

        public TurnRestriction(V intersection, E edgeFrom, E edgeTo)
        {
            Intersection = intersection;
            EdgeFrom = edgeFrom;
            EdgeTo = edgeTo;
        }

        public TurnRestriction() : this(default(V), default(E), default(E)) { }
    }

    /// <summary>
    /// A Graph strcuture for vertices and IDs
    /// </summary>
    /// <typeparam name="V">Type of Vertices</typeparam>
    /// <typeparam name="IV">Type of Vertix UIDs</typeparam>
    public sealed class Graph<V, IV, E, IE>
        where V : IEquatable<V>, IRefreshable
        where E : IEquatable<E>, IEdge<V>
        where IV : IComparable<IV>
        where IE : IComparable<IE>
    {
        Dictionary<IV, V> vertices;
        Dictionary<IE, E> edges;
        Dictionary<IV, List<TurnRestriction<V, E>>> turnRestrictions;

        public bool Reverse;
        public string ConnectionString;
        public string TableName;

        public Dictionary<IE, E> Edges
        {
            get { return edges; }
        }

        public Dictionary<IV, V> Vertices
        {
            get { return vertices; }
        }

        public Dictionary<IV, List<TurnRestriction<V, E>>> TurnRestrictions
        {
            get { return turnRestrictions; }
        }

        public List<TurnRestriction<V, E>> GetTurnRestriction(IV VertixUID)
        {
            List<TurnRestriction<V, E>> ret = null;
            if (turnRestrictions.ContainsKey(VertixUID)) ret = turnRestrictions[VertixUID];
            return ret;
        }

        public void InsertTurnRestriction(IV VertixUID, V intersect, E edgeFrom, E edgeTo)
        {
            if (edgeFrom.VertexTo.Equals(intersect) && edgeTo.VertexFrom.Equals(intersect))
            {
                if (!turnRestrictions.ContainsKey(VertixUID)) turnRestrictions.Add(VertixUID, new List<TurnRestriction<V, E>>());
                turnRestrictions[VertixUID].Add(new TurnRestriction<V, E>(intersect, edgeFrom, edgeTo));
            }
            else throw new ArgumentException("Invalid set of edges and vertex.");
        }

        public V GetVertix(IV UID)
        {
            V a = default(V);
            if (vertices.TryGetValue(UID, out a)) return a; else return default(V);
        }

        public E GetEdge(IE UID)
        {
            E a = default(E);
            if (edges.TryGetValue(UID, out a)) return a; else return default(E);
        }

        public int EdgeCount
        {
            get { return edges.Count; }
        }

        public int VertixCount
        {
            get { return vertices.Count; }
        }

        public Graph() : this(0, 0, false) { }

        public Graph(Int32 VertixCapacity, int EdgeCapacity, bool reverse)
        {
            vertices = new Dictionary<IV, V>(VertixCapacity);
            edges = new Dictionary<IE, E>(EdgeCapacity);
            Reverse = reverse;
            turnRestrictions = new Dictionary<IV, List<TurnRestriction<V, E>>>();
        }

        public void AddEdge(IE UID, E edge)
        {
            if (!edges.ContainsKey(UID)) edges.Add(UID, edge);
            else throw new Exception("Duplicate edge/UID cannot be added");
        }

        public void AddVertix(IV UID, V vertex)
        {
            if (!vertices.ContainsKey(UID)) vertices.Add(UID, vertex);
            else throw new Exception("Duplicate vertex/UID cannot be added");
        }

        public bool ContainsVertix(IV UID)
        {
            return (vertices.ContainsKey(UID));
        }

        public bool ContainsEdge(IE UID)
        {
            return (edges.ContainsKey(UID));
        }

        public V InsertOrUpdateVertex(V vertex, IV UID)
        {
            if (ContainsVertix(UID)) return GetVertix(UID);
            else
            {
                AddVertix(UID, vertex);
                return vertex;
            }
        }

        public void RefreshVertices(bool resetCapacities)
        {
            foreach (var o in vertices.Values)
            {
                o.Refresh(resetCapacities);
            }
        }

        public static Graph<Vertex, long, Edge, int> LoadFromDB(string connStr, string tableName, bool reverse)
        {
            int DID = 0;
            double Seg_len = 0.0, cap = 0.0;
            string oneway = string.Empty;
            SqlGeography shape = null;
            Vertex f = null, t = null;
            Edge eft = null, etf = null;
            RoadProperties prop = null;

            var g = new Graph<Vertex, long, Edge, int>();
            g.Reverse = reverse;
            g.ConnectionString = connStr;
            g.TableName = tableName;

            var con = new SqlConnection(connStr);
            var cmd = new SqlCommand("select * from [" + tableName + "] order by [UID]", con);
            con.Open();

            var read = cmd.ExecuteReader();

            while (read.Read())
            {
                DID = read.GetInt32(5);
                Seg_len = Convert.ToDouble(read.GetDecimal(1));
                oneway = read.GetString(3);
                shape = (SqlGeography)(read.GetSqlValue(4));
                cap = Convert.ToDouble(read.GetDecimal(2));

                f = new Vertex(shape.STStartPoint().Long.Value, shape.STStartPoint().Lat.Value);
                t = new Vertex(shape.STEndPoint().Long.Value, shape.STEndPoint().Lat.Value);

                f = g.InsertOrUpdateVertex(f, f.UID);
                t = g.InsertOrUpdateVertex(t, t.UID);
                prop = new RoadProperties(cap, oneway[0], Seg_len, shape);
                eft = new Edge(DID, f, t, prop);
                etf = new Edge(-DID, t, f, prop);

                if (reverse)
                {
                    if (oneway == "F") oneway = "T";
                    else if (oneway == "T") oneway = "F";
                }

                switch (oneway)
                {
                    case "B":
                        f.AddEdge(eft);
                        f.AddIncomingEdge(etf);
                        t.AddEdge(etf);
                        t.AddIncomingEdge(eft);
                        g.AddEdge(DID, eft);
                        g.AddEdge(-DID, etf);
                        break;
                    case "F":
                        f.AddEdge(eft);
                        t.AddIncomingEdge(eft);
                        g.AddEdge(DID, eft);
                        break;
                    case "T":
                        t.AddEdge(etf);
                        f.AddIncomingEdge(etf);
                        g.AddEdge(DID, etf);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            read.Close();
            con.Close();
            return g;
        }
    }
}