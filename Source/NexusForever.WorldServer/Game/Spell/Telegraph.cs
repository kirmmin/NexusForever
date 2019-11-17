using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Shared;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity;
using NexusForever.WorldServer.Game.Map.Search;
using NexusForever.WorldServer.Game.Spell.Static;
using NLog;

namespace NexusForever.WorldServer.Game.Spell
{
    public class Telegraph
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        public UnitEntity Caster { get; }
        public Vector3 Position { get; }
        public Vector3 Rotation { get; }
        public TelegraphDamageEntry TelegraphDamage { get; }

        public Telegraph(TelegraphDamageEntry telegraphDamageEntry, UnitEntity caster, Vector3 position, Vector3 rotation)
        {
            TelegraphDamage = telegraphDamageEntry;
            Caster          = caster;
            Position        = position;
            Rotation        = rotation;
        }

        /// <summary>
        /// Returns any <see cref="UnitEntity"/> inside the <see cref="Telegraph"/>.
        /// </summary>
        public IEnumerable<UnitEntity> GetTargets()
        {
            Caster.Map.Search(Position, GridSearchSize(), new SearchCheckTelegraph(this, Caster), out List<GridEntity> targets);
            return targets.Select(t => t as UnitEntity);
        }

        /// <summary>
        /// Returns whether the supplied <see cref="Vector3"/> is inside the telegraph.
        /// </summary>
        public bool InsideTelegraph(Vector3 position)
        {
            switch ((DamageShape)TelegraphDamage.DamageShapeEnum)
            {
                case DamageShape.Circle:
                    return Vector3.Distance(Position, position) < TelegraphDamage.Param00;
                case DamageShape.Cone:
                case DamageShape.LongCone:
                {
                    float angleRadian = Position.GetAngle(position);
                    angleRadian -= Rotation.X;
                    angleRadian = angleRadian.NormaliseRadians();

                    float angleDegrees = MathF.Abs(angleRadian.ToDegrees());
                    if (angleDegrees > TelegraphDamage.Param02 / 2f)
                        return false;

                    return Vector3.Distance(Position, position) < TelegraphDamage.Param01;
                }
                case DamageShape.Quadrilateral:
                {
                    float telegraphLength = TelegraphDamage.Param01;
                    float telegraphHeight = TelegraphDamage.Param02;

                    // Calculate angles and origin used in calculations
                    var XDegrees = Rotation.X.ToDegrees() < 0f ? Rotation.X.ToDegrees() + 360f : Rotation.X.ToDegrees();
                    Vector3 PositionWithOffset = new Vector3(Position.X, Position.Y, Position.Z);
                    if (TelegraphDamage.ZPositionOffset != 0f) // Move the telegraph's origin forward based on ZPositionOffset
                        PositionWithOffset = GetPointPositionOnPlane(Position.X, Position.Z, XDegrees + 90f, TelegraphDamage.ZPositionOffset * telegraphLength / 2f);

                    List<Vector3> points = new List<Vector3>
                    {
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees, telegraphLength / 2f), // Right
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees + 90f, telegraphLength / 2f), // Front
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees + 180f, telegraphLength / 2f), // Left
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees - 90f, telegraphLength / 2f) // Behind
                    };

                    return PointInPolygon(points.ToArray(), position.X, position.Z) && position.Y <= Position.Y + telegraphHeight && position.Y >= Position.Y - telegraphHeight;
                }
                case DamageShape.Rectangle:
                {
                    float telegraphWidth = TelegraphDamage.Param00;
                    float telegraphLength = TelegraphDamage.Param01;
                    float telegraphHeight = TelegraphDamage.Param02;

                    // Calculate angle between origin and corners of rectangle
                    float oppositeSide = telegraphLength / 2f;
                    float adjacentSide = telegraphWidth / 2f;
                    float hypotenuse = MathF.Sqrt((oppositeSide * oppositeSide) + (adjacentSide * adjacentSide));
                    double theta = Math.Atan(oppositeSide / adjacentSide) * (180 / Math.PI);
                    theta = Math.Acos(adjacentSide / hypotenuse) * (180 / Math.PI);
                    theta = Math.Asin(oppositeSide / hypotenuse) * (180 / Math.PI);

                    // Calculate angles and origin used in calculations
                    var XDegrees = Rotation.X.ToDegrees() < 0f ? Rotation.X.ToDegrees() + 360f : Rotation.X.ToDegrees();
                    Vector3 PositionWithOffset = new Vector3(Position.X, Position.Y, Position.Z);
                    if (TelegraphDamage.ZPositionOffset != 0f) // Move the telegraph's origin forward based on ZPositionOffset
                        PositionWithOffset = GetPointPositionOnPlane(Position.X, Position.Z, XDegrees + 90f, TelegraphDamage.ZPositionOffset * telegraphLength / 2f);

                    List<Vector3> points = new List<Vector3>
                    {
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees + (float)theta, telegraphLength / 2f), // Top Left
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees + 180f - (float)theta, telegraphLength / 2f), // Top Right
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees - (float)theta, telegraphLength / 2f), // Bottom Left
                        GetPointPositionOnPlane(PositionWithOffset.X, PositionWithOffset.Z, XDegrees - 180f + (float)theta, telegraphLength / 2f) // Bottom Right
                    };

                    return PointInPolygon(points.ToArray(), position.X, position.Z) && position.Y <= Position.Y + telegraphHeight && position.Y >= Position.Y - telegraphHeight;
                }
                default:
                    log.Warn($"Unhandled telegraph shape {(DamageShape)TelegraphDamage.DamageShapeEnum}.");
                    return false;
            }
        }

        private float GridSearchSize()
        {
            switch ((DamageShape)TelegraphDamage.DamageShapeEnum)
            {
                case DamageShape.Circle:
                    return TelegraphDamage.Param00;
                case DamageShape.Cone:
                case DamageShape.LongCone:
                    return TelegraphDamage.Param01;
                case DamageShape.Quadrilateral:
                case DamageShape.Rectangle:
                    return TelegraphDamage.Param01 / 2f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Calculate a point for a Telegraph vertes based on coordinates, angle in degrees, and distance
        /// </summary>
        private Vector3 GetPointPositionOnPlane(float xCoord, float zCoord, float angleInDegrees, float distance)
        {
            Vector3 result = new Vector3
            {
                Y = Position.Y
            };
            angleInDegrees = angleInDegrees * MathF.PI / -180f;
            result.X = distance * MathF.Cos(angleInDegrees) + xCoord;
            result.Z = distance * MathF.Sin(angleInDegrees) + zCoord;
            return result;
        }

        /// <summary>
        /// Returns a boolean whether a point sits in a horizontal plane of points
        /// </summary>
        private bool PointInPolygon(Vector3[] Points, float X, float Z)
        {
            // Get the angle between the point and the
            // first and last vertices.
            int maxPoint = Points.Length - 1;
            float total_angle = GetAngle(
                Points[maxPoint].X, Points[maxPoint].Z,
                X, Z,
                Points[0].X, Points[0].Z);

            // Add the angles from the point to each other pair of vertices.
            for (int i = 0; i < maxPoint; i++)
            {
                total_angle += GetAngle(
                    Points[i].X, Points[i].Z,
                    X, Z,
                    Points[i + 1].X, Points[i + 1].Z);
            }

            // The total angle should be 2 * PI or -2 * PI if the point is in the polygon and close to zero if the point is outside the polygon.
            return (Math.Abs(total_angle) > 0.000001);
        }

        #region "Cross and Dot Products"
        /// <summary>
        /// Get the total angle between 3 vertices
        /// </summary>
        private float GetAngle(float Ax, float Ay, float Bx, float By, float Cx, float Cy)
        {
            // Get the dot product.
            float dot_product = DotProduct(Ax, Ay, Bx, By, Cx, Cy);

            // Get the cross product.
            float cross_product = CrossProductLength(Ax, Ay, Bx, By, Cx, Cy);

            // Calculate the angle.
            return (float)Math.Atan2(cross_product, dot_product);
        }

        // Return the cross product AB x BC.
        // The cross product is a vector perpendicular to AB
        // and BC having length |AB| * |BC| * Sin(theta) and
        // with direction given by the right-hand rule.
        // For two vectors in the X-Y plane, the result is a
        // vector with X and Y components 0 so the Z component
        // gives the vector's length and direction.
        private float CrossProductLength(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the vectors' coordinates.
            float BAx = Ax - Bx;
            float BAy = Ay - By;
            float BCx = Cx - Bx;
            float BCy = Cy - By;

            // Calculate the Z coordinate of the cross product.
            return (BAx * BCy - BAy * BCx);
        }

        // Return the dot product AB · BC.
        // Note that AB · BC = |AB| * |BC| * Cos(theta).
        private float DotProduct(float Ax, float Ay,
            float Bx, float By, float Cx, float Cy)
        {
            // Get the vectors' coordinates.
            float BAx = Ax - Bx;
            float BAy = Ay - By;
            float BCx = Cx - Bx;
            float BCy = Cy - By;

            // Calculate the dot product.
            return (BAx * BCx + BAy * BCy);
        }
        #endregion // Cross and Dot Products
    }
}
