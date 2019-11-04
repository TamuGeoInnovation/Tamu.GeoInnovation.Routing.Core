using Microsoft.SqlServer.Types;
using SQLSpatialTools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using USC.GISResearchLab.Routing.Algorithms;
using USC.GISResearchLab.Routing.DataStructures;

namespace USC.GISResearchLab.Routing.Core
{
    public class SplitedGeometry
    {
        public double Distance1 = 0.0;
        public double Distance2 = 0.0;
        public double CenterX = 0.0;
        public double CenterY = 0.0;
        public double WalkingDist = double.MaxValue;
    }

    public class RoutingUtils
    {
        // create evacuation routes
        public static void GenereteUserRoutes(List<UserLocation> userLocations, Dictionary<int, Route> ret, List<Region> regions, string ConnString, string RoadTable)
        {
            SqlGeography route = null, center = null;
            SnapPoint sp = null;
            Point[] routePoints = null;
            var safeZones = regions.FindAll(a => a.ZoneType == ZoneTypeEnum.Safe);
            IList<Region> unsafeRegions = regions.FindAll(a => a.ZoneType != ZoneTypeEnum.Safe);
            var endPoints = new Dictionary<long, Point>();
            Dictionary<long, Vertex> temp1 = null, temp2 = new Dictionary<long, Vertex>();
            Dijkstra dj = null;
            Graph<Vertex, long, Edge, int> g = null;
            int errCount = 0;
            Exception lastEx = null;

            try
            {
                // graph preperation
                g = Graph<Vertex, long, Edge, int>.LoadFromDB(ConnString, RoadTable, false);
                AddTurnRestrictionsToGraph(g, unsafeRegions);
                dj = new Dijkstra(g, null);

                if (safeZones != null && safeZones.Count > 0)
                {
                    foreach (var safeZone in safeZones)
                    {
                        temp1 = GetVerticesInRegion(g, safeZone);
                        foreach (var t in temp1)
                            if (!endPoints.ContainsKey(t.Key)) endPoints.Add(t.Key, t.Value);

                        if (endPoints.Count == 0)
                        {
                            center = safeZone.Shape.EnvelopeCenter();
                            sp = SnapPointToGraph(new Point(center.STStartPoint().Long.Value, center.STStartPoint().Lat.Value), g);
                            endPoints.Add(sp.AssociatedEdge.VertexFrom.UID, sp.AssociatedEdge.VertexFrom);
                            endPoints.Add(sp.AssociatedEdge.VertexTo.UID, sp.AssociatedEdge.VertexTo);
                        }
                    }
                }
                else
                {
                    foreach (var z in unsafeRegions)
                    {
                        // create virtual safe zone
                        temp1 = GetVerticesInRegion(g, z);
                        foreach (var t in temp1)
                            if (!temp2.ContainsKey(t.Key)) temp2.Add(t.Key, t.Value);
                    }

                    foreach (var v in g.Vertices) if (!temp2.ContainsKey(v.Key)) endPoints.Add(v.Key, v.Value);
                }

                foreach (var u in userLocations)
                {
                    try
                    {
                        sp = SnapPointToGraph(u, g);
                        g.RefreshVertices(false);
                        routePoints = dj.Execute(sp, endPoints);

                        if (routePoints == null) routePoints = new Point[1] { new Point(u.Lng, u.Lat) };

                        route = GetSqlGeographyFromPoints(routePoints);
                        ret[u.UserId].Shape = route;
                    }
                    catch (Exception ex)
                    {
                        errCount++;
                        lastEx = ex;
                    }
                }

                if (errCount > 0) throw new Exception(errCount + " Error(s) in userlocation dijkstra execution loop", lastEx);

            }
            catch (Exception ex)
            {
                string msg = "Error GenereteUserRoutes: " + ex.Message;
                throw new Exception(msg, ex);
            }
        }

        private static SqlGeography GetSqlGeographyFromPoints(Point[] routePoints)
        {
            string wkt = null;
            SqlGeography route = SqlGeography.Null;

            if (routePoints.Length > 0)
            {
                if (routePoints.Length > 1)
                {
                    wkt = string.Empty;
                    foreach (var r in routePoints) wkt += "," + r.X + " " + r.Y;
                    wkt = "linestring (" + wkt.Substring(1) + ")";
                }
                else wkt = "point (" + routePoints[0].X + " " + routePoints[0].Y + ")";

                route = SqlGeography.STGeomFromText(new SqlChars(wkt), 4326);
            }
            return route;
        }
        public static SnapPoint SnapPointToGraph(UserLocation p, Graph<Vertex, long, Edge, int> graph)
        {
            return SnapPointToGraph(new Point(p.Lng, p.Lat), graph);
        }

        public static SnapPoint SnapPointToGraph(Point p, Graph<Vertex, long, Edge, int> graph)
        {
            // use SQL spatial query to find the nearest and then snap it.
            SnapPoint s = null;
            int edgeID = 0;
            SqlGeography shape = null;

            var con = new SqlConnection(graph.ConnectionString);
            string cmdStr = "SELECT top 1 [uid], [shapeGeog] FROM [" + graph.TableName + "] order by shapeGeog.STDistance(geography::STPointFromText('POINT (" + p.X + " " + p.Y + ")', 4326))";
            var cmd = new SqlCommand(cmdStr, con);
            con.Open();
            var read = cmd.ExecuteReader();

            if (read.Read())
            {
                edgeID = int.Parse(read[0].ToString());
                shape = (SqlGeography)(read[1]);

                s = new SnapPoint(p);
                s.AssociatedEdge = graph.GetEdge(edgeID);
                s.PositionAlongEdge = SplitGeometry(shape, p).Distance1;
            }

            read.Close();
            con.Close();

            return s;
        }

        public static void AddTurnRestrictionsToGraph(Graph<Vertex, long, Edge, int> graph, IList<Region> regions)
        {
            var addedEdges = new Dictionary<int, Edge>();
            Edge e = null;
            int edgeID = 0;
            var con = new SqlConnection(graph.ConnectionString);
            string cmdStr = "SELECT [uid] FROM [" + graph.TableName + "] where shapeGeog.STIntersects(@g) = 1";
            string shapeText = string.Empty;
            SqlDataReader read = null;
            SqlCommand cmd = null;
            SqlParameter p = null;

            foreach (var r in regions)
            {
                cmd = new SqlCommand(cmdStr, con);
                p = new SqlParameter("@g", SqlDbType.Udt);
                p.UdtTypeName = "geography";

                shapeText = r.Shape.STAsText().ToSqlString().Value;
                shapeText = shapeText.ToLower();
                shapeText = shapeText.Replace("polygon((", "polygon ((");
                shapeText = shapeText.Replace("polygon ((", "linestring (");
                shapeText = shapeText.Replace("))", ")");
                p.Value = SqlGeography.STLineFromText(new SqlChars(shapeText), r.Shape.STSrid.Value);

                cmd.Parameters.Add(p);
                con.Open();
                read = cmd.ExecuteReader();

                while (read.Read())
                {
                    edgeID = int.Parse(read[0].ToString());
                    e = graph.GetEdge(edgeID);
                    if (e != null) addTurnRestrictionToGraph(graph, addedEdges, e, r);

                    e = graph.GetEdge(-edgeID);
                    if (e != null) addTurnRestrictionToGraph(graph, addedEdges, e, r);
                }

                read.Close();
                con.Close();
            }
        }

        private static void addTurnRestrictionToGraph(Graph<Vertex, long, Edge, int> graph, Dictionary<int, Edge> addedEdges, Edge e, Region r)
        {

            if (!addedEdges.ContainsKey(e.DBID))
            {
                SqlGeography fromPoint, toPoint;
                fromPoint = SqlGeography.STPointFromText(new SqlChars("POINT(" + e.VertexFrom.X + " " + e.VertexFrom.Y + ")"), 4326);
                toPoint = SqlGeography.STPointFromText(new SqlChars("POINT(" + e.VertexTo.X + " " + e.VertexTo.Y + ")"), 4326);

                if (r.Shape.STIntersects(toPoint).IsTrue && r.Shape.STIntersects(fromPoint).IsFalse)
                {
                    addedEdges.Add(e.DBID, e);
                    foreach (var edgeFrom in e.VertexFrom.IncomingEdges)
                        graph.InsertTurnRestriction(e.VertexFrom.UID, e.VertexFrom, edgeFrom, e);
                }
            }
        }

        public static Dictionary<long, Vertex> GetVerticesInRegion(Graph<Vertex, long, Edge, int> graph, Region region)
        {
            var l = new Dictionary<long, Vertex>();

            foreach (var v in graph.Vertices.Values)
            {
                if (region.Shape.STIntersects(SqlGeography.STPointFromText(new SqlChars("POINT(" + v.X + " " + v.Y + ")"), 4326)))
                {
                    l.Add(v.UID, v);
                }
            }

            return l;
        }

        private static SplitedGeometry SplitGeometry(SqlGeography line, Point point)
        {
            SqlGeometry lgem = null, pgem = null;
            SqlGeography p = null;

            p = SqlGeography.STPointFromText(new SqlChars("POINT(" + point.X + " " + point.Y + ")"), 4326);
            var prj = SqlProjection.Mercator(point.X);
            pgem = prj.Project(p);
            lgem = prj.Project(line);

            return SplitGeometry(lgem, pgem);
        }

        private static SplitedGeometry SplitGeometry(SqlGeometry line, SqlGeometry point)
        {
            int count = line.STNumPoints().Value;
            double te1 = 0.0, te2 = 0.0, l1 = 0.0, l2 = 0.0, p1inp = 0.0, travel = 0.0, p1p2 = 0.0,
                len = line.STLength().Value, slope = 0.0, cx1 = 0.0, cx2 = 0.0;
            var ret = new SplitedGeometry();

            for (int i = 2; i <= count; i++)
            {
                te1 = (point.STX.Value - line.STPointN(i - 1).STX.Value) * (line.STPointN(i).STX.Value - line.STPointN(i - 1).STX.Value) +
                    (point.STY.Value - line.STPointN(i - 1).STY.Value) * (line.STPointN(i).STY.Value - line.STPointN(i - 1).STY.Value);
                te2 = (point.STX.Value - line.STPointN(i).STX.Value) * (line.STPointN(i - 1).STX.Value - line.STPointN(i).STX.Value) +
                    (point.STY.Value - line.STPointN(i).STY.Value) * (line.STPointN(i - 1).STY.Value - line.STPointN(i).STY.Value);
                p1p2 = EuclideanDistance(line.STPointN(i - 1).STX.Value, line.STPointN(i - 1).STY.Value, line.STPointN(i).STX.Value, line.STPointN(i).STY.Value);
                if (te1 * te2 >= 0.0)
                {
                    p1inp = EuclideanDistance(line.STPointN(i - 1).STX.Value, line.STPointN(i - 1).STY.Value, point.STX.Value, point.STY.Value);
                    l1 = (p1inp * p1p2) /
                        (p1inp + EuclideanDistance(line.STPointN(i).STX.Value, line.STPointN(i).STY.Value, point.STX.Value, point.STY.Value));
                    l2 = Math.Sqrt((p1inp * p1inp) - (l1 * l1));
                    if (l2 < ret.WalkingDist)
                    {
                        ret.WalkingDist = l2;
                        ret.Distance1 = l1 + travel;
                        ret.Distance2 = len - travel - l1;
                        slope = (line.STPointN(i - 1).STY.Value - line.STPointN(i).STY.Value) / (line.STPointN(i - 1).STX.Value - line.STPointN(i).STX.Value);
                        cx1 = line.STPointN(i - 1).STX.Value - Math.Sqrt((l1 * l1) / (1 + slope * slope));
                        cx2 = line.STPointN(i - 1).STX.Value + Math.Sqrt((l1 * l1) / (1 + slope * slope));
                        if (Math.Abs(cx1 - line.STPointN(i).STX.Value) <= Math.Abs(cx2 - line.STPointN(i).STX.Value)) ret.CenterX = cx1;
                        else ret.CenterX = cx2;
                        ret.CenterY = line.STPointN(i - 1).STY.Value + slope * (ret.CenterX - line.STPointN(i - 1).STX.Value);
                    }
                }
                travel += p1p2;
            }

            if (ret.Distance1 == 0.0)
            {
                te1 = EuclideanDistance(line.STPointN(1).STX.Value, line.STPointN(1).STY.Value, point.STX.Value, point.STY.Value);
                te2 = EuclideanDistance(line.STPointN(count).STX.Value, line.STPointN(count).STY.Value, point.STX.Value, point.STY.Value);
                if (te1 <= te2)
                {
                    ret.Distance2 = len;
                    ret.WalkingDist = te1;
                    ret.CenterX = line.STPointN(1).STX.Value;
                    ret.CenterY = line.STPointN(1).STY.Value;
                }
                else
                {
                    ret.Distance1 = len;
                    ret.WalkingDist = te2;
                    ret.CenterX = line.STPointN(count).STX.Value;
                    ret.CenterY = line.STPointN(count).STY.Value;
                }
            }
            return ret;
        }

        public static double EuclideanDistance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(((x1 - x2) * (x1 - x2)) + ((y1 - y2) * (y1 - y2)));
        }

        public static double EuclideanDistance(Point p1, Point p2)
        {
            return EuclideanDistance(p1.X, p1.Y, p2.X, p2.Y);
        }
    }
}