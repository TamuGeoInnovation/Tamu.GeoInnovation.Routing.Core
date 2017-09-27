using Microsoft.SqlServer.Types;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using USC.GISResearchLab.Common.Core.KML;
using USC.GISResearchLab.Common.Core.Utils.JSON;
using USC.GISResearchLab.Common.Geometries;

namespace USC.GISResearchLab.Routing.DataStructures
{
    public enum ZoneTypeEnum { Safe, LowRisk, HighRisk }

    public class Region : JSONGeometry
    {
        public ZoneTypeEnum ZoneType;

        [XmlIgnore]
        public SqlGeography Shape;

        public string JsonGeometry
        {
            get
            {
                return base.JsonShape;
            }
            set
            {
                base.JsonShape = value;
            }
        }

        public Region(SqlGeography shape, ZoneTypeEnum zoneType)
        {
            ZoneType = zoneType;
            Shape = shape;
            JsonShape = string.Empty;
        }

        public Region() : this(null, ZoneTypeEnum.Safe) { }
    }

    public class RegionKML
    {
        public ZoneTypeEnum ZoneType;

        [XmlIgnore]
        public SqlGeography Shape;

        public string KML
        {
            get
            {
                var kmlDoc = new KMLDocument("Region");
                kmlDoc.AddSqlGeography(Shape.Reduce(1), "Region", string.Empty, "Type: " + ZoneType);
                return kmlDoc.ToString();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public RegionKML(SqlGeography shape, ZoneTypeEnum zoneType)
        {
            ZoneType = zoneType;
            Shape = shape;
        }

        public RegionKML(Region reg)
        {
            ZoneType = reg.ZoneType;
            Shape = reg.Shape;
        }

        public RegionKML() : this(null, ZoneTypeEnum.Safe) { }
    }

    public class Route
    {
        public int UserId;
        public string Direction;
        public int RouteId;

        [XmlIgnore]
        public SqlGeography Shape;

        public string JsonGeometry
        {
            get
            {
                if (Shape != null && !Shape.IsNull && !Shape.STIsEmpty())
                    return JSONGeometry.GetAsJsonString(Shape);
                return string.Empty;
            }
            set
            {
                throw new NotImplementedException();
            }
        }
        public string WKT
        {
            get
            {
                if (Shape != null && !Shape.IsNull && !Shape.STIsEmpty())
                    return Shape.STAsText().ToSqlString().Value;
                else return string.Empty;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Shape = Geometry.WKT2SqlGeography(4326, value);
                }
            }
        }

        public Route() : this(-1, null, string.Empty, -1) { }

        public Route(int userId, SqlGeography shape, string direction, int routeId)
        {
            UserId = userId;
            Shape = shape;
            Direction = direction;
            RouteId = routeId;
        }
    }

    public class AlertResponse
    {
        public string Message;
        public List<Route> RouteList;

        public AlertResponse()
        {
            Message = string.Empty;
            RouteList = new List<Route>();
        }
    }
}
