using Microsoft.SqlServer.Types;
using System;

namespace USC.GISResearchLab.Routing.DataStructures
{
	public class Point
	{
        protected int y, x;
        private static double PointResolution = 1000000.0;
        public static double XLen = 0.011979;
        public static double YLen = 0.007623;
        public static double XMin = -118.291729;
        public static double YMin = 34.018017;
        public static int xLen = Convert.ToInt32(XLen * PointResolution);
        public static int yLen = Convert.ToInt32(YLen * PointResolution);
        public static int xMin = Convert.ToInt32(XMin * PointResolution);
        public static int yMin = Convert.ToInt32(YMin * PointResolution);

		public double Y
		{
			get { return y / PointResolution; }
			set { y = Convert.ToInt32(value * PointResolution); }
		}

		public double X
		{
			get { return x / PointResolution; }
			set { x = Convert.ToInt32(value * PointResolution); }
		}

		public Point()
			: this(0.0, 0.0)
		{ }

		public Point(double _X, double _Y)
		{
			X = _X;
			Y = _Y;
		}

        public bool Equals(Point p)
        {
            return p.x == x && p.y == y;
        }
	}

    public class SnapPoint : Point
    {
        public Edge AssociatedEdge;
        public double PositionAlongEdge;

        public SnapPoint(Point p) : this(p.X, p.Y, null, 0.0) { }

        public SnapPoint() : this(0.0, 0.0, null, 0.0) { }

        public SnapPoint(double _X, double _Y, Edge associatedEdge, double position)            
        {
            AssociatedEdge = associatedEdge;
            PositionAlongEdge = position;
            X = _X;
            Y = _Y;
        }
    }

    public class UserLocation
    {
        public double Lat;
        public double Lng;

        public int UserId;
        public string Updated;
        public string AlertShownTime;

        public UserLocation() : this(0.0, 0.0, -1, DateTime.MinValue, DateTime.MinValue) { }

        public UserLocation(SqlGeography point, int userId, DateTime updated, DateTime alertShownTime)
            : this(point.Lat.Value, point.Long.Value, userId, updated, alertShownTime) { }

        public UserLocation(double lat, double lng, int userId, DateTime updated, DateTime alertShownTime)
        {
            Lat = lat;
            Lng = lng;
            UserId = userId;
            if (updated != DateTime.MinValue)
            {
                Updated = updated.ToString("u");
                Updated = Updated.Substring(0, Updated.Length - 1);
            }
            else Updated = string.Empty;
            if (alertShownTime != DateTime.MinValue)
            {
                AlertShownTime = alertShownTime.ToString("u");
                AlertShownTime = AlertShownTime.Substring(0, AlertShownTime.Length - 1);
            }
            else AlertShownTime = string.Empty;
        }
    }
}